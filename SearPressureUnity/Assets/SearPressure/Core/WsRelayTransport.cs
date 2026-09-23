using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SearPressure
{
    // Online play through the WebSocket relay in Server/relay.js.
    //
    // Wire format (JSON text frames), phone -> relay:
    //   {op:'host'}                     open a room          -> {op:'hosted', code}
    //   {op:'join', code}               join one             -> {op:'joined', id} or {op:'error', reason: 'nocode'|'full'|'bad'}
    //   {op:'send', to?, d}             host: d to guest `to` (every guest when missing); guest: d to the host
    //   {op:'kick', id}                 host: drop a guest
    //   {op:'ping'}                     keep-alive           -> {op:'pong'}
    // relay -> phone: {op:'msg', from?, d}, {op:'peer-join', id}, {op:'peer-leave', id}, {op:'host-left'}.
    //
    // The socket lives on background tasks. Everything they learn goes into a queue that the game
    // empties with Poll on its own thread, so no game state is touched from another thread.
    // (Unity WebGL builds have no ClientWebSocket; give them a JS-backed INetTransport instead.)
    public sealed class WsRelayTransport : INetTransport
    {
        public double connectTimeout = 10, idleTimeout = 30, pingEvery = 10;   // seconds

        readonly Uri url;
        readonly ConcurrentQueue<NetEvent> inbox = new ConcurrentQueue<NetEvent>();
        readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
        readonly SemaphoreSlim outSignal = new SemaphoreSlim(0);
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        ClientWebSocket ws;
        int started, endedFlag;
        volatile bool closing;
        long lastHeard;   // Environment.TickCount64-style ms

        public WsRelayTransport(string url) { this.url = new Uri(url); }

        public void Host() => Start("{\"op\":\"host\"}");
        public void Join(string code) => Start("{\"op\":\"join\",\"code\":" + Json.Write(code) + "}");
        public void Send(int to, string json)
        {
            if (closing) return;
            Enqueue(to >= 0 ? "{\"op\":\"send\",\"to\":" + to + ",\"d\":" + json + "}" : "{\"op\":\"send\",\"d\":" + json + "}");
        }
        public void Kick(int peer) { if (!closing) Enqueue("{\"op\":\"kick\",\"id\":" + peer + "}"); }
        public bool Poll(out NetEvent e)
        {
            if (closing) { e = null; return false; }
            return inbox.TryDequeue(out e);
        }
        public void Close()
        {
            if (closing) return;
            closing = true;
            outSignal.Release();   // the send loop flushes, then closes the socket
            if (Volatile.Read(ref started) == 0) { cts.Cancel(); return; }
            Task.Delay(3000).ContinueWith(_ => { try { cts.Cancel(); ws?.Abort(); } catch (Exception) { } });
        }

        void Enqueue(string s) { outbox.Enqueue(s); outSignal.Release(); }
        static long Now() => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

        void Start(string hello)
        {
            if (Interlocked.Exchange(ref started, 1) == 1 || closing) return;
            Enqueue(hello);
            Task.Run(Run);
        }

        void Emit(NetEvent e) { if (!closing) inbox.Enqueue(e); }
        // The line went down: tell the game once (unless it hung up itself).
        void Ended(string reason)
        {
            if (Interlocked.Exchange(ref endedFlag, 1) == 1) return;
            Emit(new NetEvent { type = "closed", reason = reason });
        }

        async Task Run()
        {
            ws = new ClientWebSocket();
            ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            try
            {
                using (var ct = CancellationTokenSource.CreateLinkedTokenSource(cts.Token))
                {
                    ct.CancelAfter(TimeSpan.FromSeconds(connectTimeout));
                    await ws.ConnectAsync(url, ct.Token).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                Interlocked.Exchange(ref endedFlag, 1);
                Emit(new NetEvent { type = "error", reason = "network" });
                try { ws.Dispose(); } catch (Exception) { }
                return;
            }
            lastHeard = Now();
            var sending = Task.Run(SendLoop);
            await ReceiveLoop().ConfigureAwait(false);
            try { cts.Cancel(); } catch (Exception) { }
            try { await sending.ConfigureAwait(false); } catch (Exception) { }
            try { ws.Dispose(); } catch (Exception) { }
        }

        async Task SendLoop()
        {
            var token = cts.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool woke = await outSignal.WaitAsync(TimeSpan.FromSeconds(pingEvery), token).ConfigureAwait(false);
                    if (!woke) outbox.Enqueue("{\"op\":\"ping\"}");
                    while (outbox.TryDequeue(out var s))
                    {
                        var bytes = Encoding.UTF8.GetBytes(s);
                        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
                    }
                    if (closing)
                    {
                        using (var ct = new CancellationTokenSource(2000))
                            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", ct.Token).ConfigureAwait(false);
                        return;
                    }
                    // Nothing from the relay for a long while (not even pongs): the line is dead.
                    if (Now() - Interlocked.Read(ref lastHeard) > idleTimeout * 1000) { Ended("timeout"); ws.Abort(); return; }
                }
            }
            catch (Exception) { Ended("network"); try { ws.Abort(); } catch (Exception) { } }
        }

        async Task ReceiveLoop()
        {
            var buf = new byte[16 * 1024];
            var msg = new List<byte>(64 * 1024);
            var token = cts.Token;
            try
            {
                while (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseSent)
                {
                    var r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), token).ConfigureAwait(false);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    for (int i = 0; i < r.Count; i++) msg.Add(buf[i]);
                    if (!r.EndOfMessage) continue;
                    Interlocked.Exchange(ref lastHeard, Now());
                    string text = Encoding.UTF8.GetString(msg.ToArray());
                    msg.Clear();
                    Handle(text);
                }
            }
            catch (Exception) { }
            Ended("network");
        }

        // Relay envelope -> game event. Parsing here keeps the work off the game's frame.
        void Handle(string text)
        {
            object o;
            try { o = Json.Parse(text); } catch (Exception) { return; }
            switch (J.Str(o, "op"))
            {
                case "hosted": Emit(new NetEvent { type = "hosted", code = J.Str(o, "code") }); break;
                case "joined": Emit(new NetEvent { type = "joined", peer = (int)J.Num(o, "id") }); break;
                case "peer-join": Emit(new NetEvent { type = "peer-join", peer = (int)J.Num(o, "id") }); break;
                case "peer-leave": Emit(new NetEvent { type = "peer-leave", peer = (int)J.Num(o, "id") }); break;
                case "msg": Emit(new NetEvent { type = "msg", peer = (int)J.Num(o, "from"), msg = J.Get(o, "d") }); break;
                case "error": Interlocked.Exchange(ref endedFlag, 1); Emit(new NetEvent { type = "error", reason = J.Str(o, "reason") ?? "bad" }); break;
                case "host-left": Ended("host-left"); break;
            }
        }
    }
}
