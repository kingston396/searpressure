using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SearPressure
{
    /* =========================================================================
       Online co-op (through a relay). The host's phone runs the kitchen; the
       guests send inputs and draw snapshots. The web version finds the other
       phones with PeerJS/WebRTC; here a small WebSocket relay server
       (Server/relay.js) hands out room codes and passes messages along:
       guest -> host, and host -> one guest or every guest. The game-level
       messages (t: fit, fits, in, act, pause, vs, vsboard, start, snap, lobby,
       mode, bye, full) are the web version's.
       ========================================================================= */

    // What the transport tells the game. Polled on the game's own thread, once a frame.
    //   hosted     (host)  the room is open; `code` is the room code
    //   joined     (guest) we're in the room
    //   peer-join  (host)  guest `peer` arrived
    //   peer-leave (host)  guest `peer` left or dropped
    //   msg        a game message `msg` (parsed JSON); on the host, `peer` says which guest sent it
    //   error      couldn't host or join: `reason` is nocode, full, network or bad
    //   closed     the connection ended: `reason` is host-left, network or timeout
    public sealed class NetEvent
    {
        public string type, code, reason;
        public int peer;
        public object msg;
    }

    // A line to the other phones. Every call is made from the game's thread; implementations
    // may do their networking elsewhere but only hand events over through Poll.
    public interface INetTransport
    {
        void Host();                      // open a room (answers hosted or error)
        void Join(string code);           // join a room (answers joined or error)
        void Send(int to, string json);   // host: to guest `to`, or -1 for every guest. Guest: to the host (`to` ignored)
        void Kick(int peer);              // host: drop a guest (after sending them 'full')
        bool Poll(out NetEvent e);        // next event, if any
        void Close();                     // sends anything still queued, then hangs up. No more events after this.
    }

    public sealed class RemoteInput { public double mx, my; public bool held, grab, chop; }
    // On the host: one per friend, in join order.
    public sealed class Guest
    {
        public int id;                    // the relay's id for this friend
        public int slot;                  // their chef, fixed for a whole service
        public RemoteInput remote = new RemoteInput();
        public string fit = "classic";
        public List<string> gear = new List<string>();
        public bool left;
    }
    public sealed class VsRow { public double score; public int served; public bool done, left; }

    public sealed class NetState
    {
        public string role;               // null offline, 'host' or 'guest'
        public string mode = "coop";      // 'coop' (one shared kitchen) or 'versus' (a copy of the kitchen each, racing)
        public string code = "";
        public INetTransport transport;
        public bool joined;               // guest: the relay let us in
        public int slot = 1;              // on a guest: which chef is ours
        public List<string> fits = new List<string>();          // every chef's outfit
        public List<List<string>> gears = new List<List<string>>();
        public List<Guest> guests = new List<Guest>();          // on the host
        public Dictionary<int, VsRow> vs = new Dictionary<int, VsRow>();   // versus scoreboard by player slot
        public double vsT;
        public List<object[]> events = new List<object[]>();    // sounds, floats and puffs since the last snapshot
        public double snapT, sendT;
        public string lastSent = "", lastRole;
    }

    public sealed partial class Game
    {
        public readonly NetState NET = new NetState();
        public const string CODE_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const double SNAP_RATE = 1 / 15.0;
        static readonly CultureInfo INV = CultureInfo.InvariantCulture;

        static Dictionary<string, object> Msg(string t, params (string k, object v)[] kv)
        {
            var d = new Dictionary<string, object> { ["t"] = t };
            foreach (var (k, v) in kv) d[k] = v;
            return d;
        }
        static double fix(double v, int n) => Math.Round(v, n, MidpointRounding.AwayFromZero);   // JS +v.toFixed(n)
        static double num(object o) => o is double d ? d : o is bool b ? (b ? 1 : 0) : o is string s && double.TryParse(s, NumberStyles.Float, INV, out var p) ? p : 0;
        static bool truthy(object o) => o is bool b ? b : o is double d ? d != 0 && !double.IsNaN(d) : o is string s ? s.Length > 0 : o != null;
        static object at(List<object> a, int i) => a != null && i < a.Count ? a[i] : null;

        void netFail(string reason)
        {
            string msg =
                reason == "nolib" ? "Online play isn't available on this device." :
                reason == "nocode" ? "No kitchen found with that code. Check the code with your friend. Their game needs to be open on the \"Host\" screen." :
                reason == "full" ? $"That kitchen is full ({MAX_PLAYERS} chefs)." :
                reason == "network" || reason == "timeout" ? "Couldn't reach the online server. Check your internet connection and try again." :
                "Something went wrong connecting. Try again.";
            netLeave(false);
            show("scr-online");
            netStatus(msg);
            setOnlineButtons(true);
        }

        public void hostGame()
        {
            if (NET.role != null) return;
            setOnlineButtons(false);
            netStatus("Setting up your kitchen…");
            INetTransport tr = null;
            try { tr = platform.CreateTransport(); } catch (Exception) { }
            if (tr == null) { netFail("nolib"); return; }
            NET.transport = tr; NET.role = "host"; NET.code = ""; NET.joined = false;
            tr.Host();
        }

        public void joinGame()
        {
            if (NET.role != null) return;
            string code = (typed ?? "").Trim().ToUpperInvariant();
            if (code.Length != 4 || code.Any(ch => ch < 'A' || ch > 'Z')) { netStatus("Enter the 4-letter code from your friend’s screen."); return; }
            setOnlineButtons(false);
            netStatus("Joining " + code + "…");
            INetTransport tr = null;
            try { tr = platform.CreateTransport(); } catch (Exception) { }
            if (tr == null) { netFail("nolib"); return; }
            NET.transport = tr; NET.role = "guest"; NET.code = code; NET.joined = false;
            tr.Join(code);
        }

        // Everything the relay said since last frame. Runs from netTick, on the game's thread.
        void netPoll()
        {
            var tr = NET.transport;
            while (tr != null && tr == NET.transport && tr.Poll(out var e))
            {
                if (NET.role == "host") hostEvent(e);
                else if (NET.role == "guest") guestEvent(e);
            }
        }

        void hostEvent(NetEvent e)
        {
            switch (e.type)
            {
                case "hosted":
                    NET.code = e.code ?? "";
                    netStatus($"Tell your friends this code (up to {MAX_PLAYERS - 1} can join). On their phones: Play with a friend → Join.", NET.code);
                    break;
                case "peer-join":
                    // Room for three friends, and nobody joins halfway through a service.
                    if (NET.guests.Count >= MAX_PLAYERS - 1 || G != null)
                    {
                        NET.transport.Send(e.peer, Json.Write(Msg("full", ("busy", G != null))));
                        NET.transport.Kick(e.peer);
                        return;
                    }
                    NET.guests.Add(new Guest { id = e.peer });
                    sfx("order"); enterOnlineMenu();
                    break;
                case "peer-leave": { var g = NET.guests.Find(x => x.id == e.peer); if (g != null) guestLeft(g); break; }
                case "msg": { var g = NET.guests.Find(x => x.id == e.peer); onNetMessage(e.msg, g); break; }
                case "error": if (NET.guests.Count == 0) netFail(e.reason); break;
                case "closed": if (NET.guests.Count > 0 || G != null) netDropped(); else netFail(e.reason); break;
            }
        }

        void guestEvent(NetEvent e)
        {
            switch (e.type)
            {
                case "joined":
                    NET.joined = true;
                    sfx("order");
                    netSend(Msg("fit", ("o", save.outfits[0] ?? "classic"), ("g", myLoadout(0))));
                    netStatus("Connected! Waiting for the host to pick a kitchen.");
                    break;
                case "msg": onNetMessage(e.msg, null); break;
                case "error": if (!NET.joined) netFail(e.reason); else netDropped(); break;
                case "closed": if (!NET.joined) netFail(e.reason); else netDropped(); break;
            }
        }

        // Host: to every friend. Guest: to the host.
        void netSend(object msg)
        {
            if (!netOpen()) return;
            NET.transport.Send(-1, Json.Write(msg));
        }
        bool netOpen() => NET.transport != null && (NET.role == "host" ? NET.guests.Count > 0 : NET.role == "guest" && NET.joined);
        public int netPlayers() => NET.role == "host" ? 1 + NET.guests.Count : NET.role != null ? (NET.fits.Count > 0 ? NET.fits.Count : 2) : 1;

        // Host: everyone's outfits by chef slot (host first, then friends in join order).
        void sendFits()
        {
            NET.fits = new List<string> { save.outfits[0] ?? "classic" }.Concat(NET.guests.Select(g => g.fit)).ToList();
            NET.gears = new List<List<string>> { myLoadout(0) }.Concat(NET.guests.Select(g => g.gear ?? new List<string>())).ToList();
            netSend(Msg("fits", ("fits", NET.fits), ("gears", NET.gears)));
            if (G != null) for (int i = 0; i < G.chefs.Count; i++) { var c = G.chefs[i]; if (c.ai == null) { c.fit = chefFit(i); c.gear = chefGear(i, G.fair); } }
            syncOnlineBanner();
        }

        // Host: one friend left. The rest play on; with nobody left it's back to the online screen.
        void guestLeft(Guest g)
        {
            int i = NET.guests.IndexOf(g);
            if (i < 0 || NET.role != "host") return;
            NET.guests.RemoveAt(i);
            try { NET.transport?.Kick(g.id); } catch (Exception) { }
            if (NET.guests.Count == 0) { netDropped(); return; }
            // Their phone paused everyone on the way out; carry on without them.
            if (G != null && G.versus && NET.vs.TryGetValue(g.slot, out var row)) row.left = true;
            if (G != null) { g.left = true; addFloat("hud", 0, 0, "A friend left", "#ff6b57"); if (paused) setPaused(false, true); }
            else { sendFits(); enterOnlineMenu(); }
        }

        public void netLeave(bool notify = true)
        {
            if (notify) netSend(Msg("bye"));
            var tr = NET.transport;
            NET.role = null; NET.transport = null; NET.joined = false; NET.events.Clear(); NET.guests = new List<Guest>(); NET.fits = new List<string>(); NET.mode = "coop"; NET.vs = new Dictionary<int, VsRow>();
            try { tr?.Close(); } catch (Exception) { }
            syncOnlineBanner();
        }

        void netDropped()
        {
            if (NET.role == null) return;
            NET.lastRole = NET.role;
            bool wasPlaying = G != null;
            netLeave(false);
            G = null; paused = false;
            buildLevelList();
            show("scr-online");
            setOnlineButtons(true);
            netStatus(wasPlaying ? (NET.lastRole == "host" ? "Everyone left the kitchen, or the connection dropped." : "The host left the kitchen, or the connection dropped.") : "The connection closed.");
        }

        public string OnlineStatus => onlineStatus;   // what the online screen says (for tests and the platform)

        // Closing the app says goodbye, so friends aren't left waiting on a dead connection.
        public void AppQuit() { if (NET.role != null) netLeave(); }

        // Host: after a friend joins (and between rounds) the host picks the kitchen.
        void enterOnlineMenu()
        {
            G = null; paused = false;
            buildLevelList();
            syncOnlineBanner();
            if (NET.role == "host") { show("scr-title"); netSend(Msg("lobby")); netSend(Msg("mode", ("m", NET.mode))); sendFits(); }
            else { show("scr-online"); netStatus("Connected! Waiting for the host to pick a kitchen."); setOnlineButtons(false); }
        }
        // The banner on the title screen (OnlineBanner) is drawn live from NET every frame.
        void syncOnlineBanner() { syncTutorialButton(); }

        /* ---------- messages ---------- */

        void onNetMessage(object mo, Guest from)
        {
            if (!(mo is Dictionary<string, object> m)) return;
            string t = J.Str(m, "t");
            if (NET.role == "host")
            {
                if (from == null || !NET.guests.Contains(from)) return;   // ignore stragglers from a friend who already left
                var r = from.remote;
                if (t == "bye") guestLeft(from);
                else if (t == "fit") { from.fit = J.Get(m, "o") is string o && o.Length > 0 ? o : "classic"; from.gear = cleanGear(J.Get(m, "g")); sendFits(); }
                else if (t == "in") { r.mx = U.Clamp(num(J.Get(m, "mx")), -1, 1); r.my = U.Clamp(num(J.Get(m, "my")), -1, 1); r.held = truthy(J.Get(m, "held")); }
                else if (t == "act") { var a = J.Str(m, "a"); if (a == "grab") r.grab = true; if (a == "chop") { r.chop = true; r.held = true; } }
                else if (t == "pause") setPaused(truthy(J.Get(m, "on")), true);   // and tell everyone else
                else if (t == "vs" && G != null && G.versus) NET.vs[from.slot] = new VsRow { score = num(J.Get(m, "score")), served = (int)num(J.Get(m, "served")), done = truthy(J.Get(m, "done")) };
                return;
            }
            // guest
            if (t == "bye") { netDropped(); return; }
            if (t == "full") { bool busy = truthy(J.Get(m, "busy")); netLeave(false); show("scr-online"); setOnlineButtons(true); netStatus(busy ? "That kitchen is in the middle of a service. Try again when they are back on the menu." : $"That kitchen is full ({MAX_PLAYERS} chefs)."); return; }
            if (t == "mode") { setMode(J.Str(m, "m")); return; }
            if (t == "vsboard")
            {
                if (J.Get(m, "b") is Dictionary<string, object> b)
                    foreach (var kv in b)
                    {
                        if (!int.TryParse(kv.Key, NumberStyles.Integer, INV, out int k) || k == NET.slot) continue;
                        NET.vs[k] = new VsRow { score = num(J.Get(kv.Value, "score")), served = (int)num(J.Get(kv.Value, "served")), done = truthy(J.Get(kv.Value, "done")), left = truthy(J.Get(kv.Value, "left")) };
                    }
                return;
            }
            if (t == "fits")
            {
                NET.fits = J.Get(m, "fits") is List<object> fl ? fl.Select(x => x as string ?? "classic").ToList() : new List<string>();
                NET.gears = J.Get(m, "gears") is List<object> gl ? gl.Select(x => cleanGear(x)).ToList() : new List<List<string>>();
                if (G != null) for (int i = 0; i < G.chefs.Count; i++) { var c = G.chefs[i]; if (c.ai == null) { c.gear = chefGear(i, G.fair); c.fit = chefFit(i); } }
                syncOnlineBanner();
                return;
            }
            if (t == "start")
            {
                paused = false; show(null);
                NET.slot = (int)Math.Max(1, Math.Min(MAX_PLAYERS - 1, J.Get(m, "me") is double me0 ? me0 : 1));
                if (J.Get(m, "fits") is List<object> fl) NET.fits = fl.Select(x => x as string ?? "classic").ToList();
                if (J.Get(m, "gears") is List<object> gl) NET.gears = gl.Select(x => cleanGear(x)).ToList();
                bool versus = J.Has(m, "vs");
                string dk = J.Str(m, "dk");
                startLevel((int)num(J.Get(m, "lv")), new StartOpts
                {
                    transpose = J.Get(m, "tr") is bool tr ? tr : (bool?)null,
                    dailyKey = dk, dailyBase = dk != null && J.Get(m, "db") is double db ? (int)db : (int?)null,
                    players = (int)num(J.Get(m, "n")), versus = versus, seed = versus ? num(J.Get(m, "vs")) : (double?)null,
                    vary = J.Get(m, "vary") is double vy ? vy : (double?)null, events = truthy(J.Get(m, "events")),
                });
                NET.vs = new Dictionary<int, VsRow>(); NET.vsT = 0;
                G.active = versus ? 0 : Math.Min(NET.slot, G.chefs.Count - 1);
                foreach (var c in G.chefs) { c.tx = c.x; c.ty = c.y; }
            }
            else if (t == "snap") applySnapshot(m);
            else if (t == "lobby") enterOnlineMenu();
            else if (t == "pause") setPaused(truthy(J.Get(m, "on")), false);
        }

        void setPaused(bool on, bool tell)
        {
            if (G == null || (G.phase == "over" && on)) return;
            if (tell) netSend(Msg("pause", ("on", on)));
            paused = on;
            if (on) { input.joy.id = null; syncPauseButtons(); show("scr-pause"); } else show(null);
        }

        /* ---------- snapshots ---------- */

        Dictionary<string, object> snapshot()
        {
            int ci(Tile t) => t != null ? G.tiles.IndexOf(t) : -1;
            return new Dictionary<string, object>
            {
                ["t"] = "snap",
                ["tl"] = G.netTiles.Select(t => (object)new List<object> { t.item?.ToJson(), fix(t.fire, 2), t.count, t.dirty, fix(t.wash, 2), fix(t.brew, 2), t.brewing, t.jar, fix(t.blend, 2), t.blending, fix(t.@out, 1) }).ToList(),
                ["xe"] = G.evt != null ? new List<object> { G.evt.kind, G.evt.name, fix(G.evt.t, 1) } : null,
                ["ch"] = G.chefs.Select(c => (object)new List<object> { fix(c.x, 3), fix(c.y, 3), fix(c.fx, 2), fix(c.fy, 2), c.held?.ToJson(), c.task != null ? new List<object> { c.task.kind, ci(c.task.tile) } : null, c.moving, fix(c.walk, 2), c.toast, fix(c.toastT, 2), c.spraying }).ToList(),
                ["or"] = G.orders.Select(o => (object)new List<object> { o.id, o.recipe, o.total, fix(o.timeLeft, 2), orderItems(o), orderReward(o), o.delivery ? (o.late ? 2 : 1) : 0, o.vip }).ToList(),
                ["g"] = new List<object> { G.score, G.tips, G.served, G.failed, fix(G.time, 2), G.phase, fix(G.countdown, 2), fix(G.endT, 2), G.missLoss },
                ["ev"] = spliceEvents(),
                ["jd"] = new List<object> { G.strikes, G.judge?.text, G.judge != null ? fix(G.judge.t, 1) : 0, G.judge != null ? G.judge.mood : 0, G.kicked, fix(G.clock, 1), G.judge?.who },
            };
        }
        List<object[]> spliceEvents() { var e = NET.events; NET.events = new List<object[]>(); return e; }

        void applySnapshot(Dictionary<string, object> m)
        {
            if (G == null) return;
            if (J.Get(m, "tl") is List<object> tl)
                for (int i = 0; i < tl.Count && i < G.netTiles.Count; i++)
                {
                    if (!(tl[i] is List<object> a)) continue;
                    var t = G.netTiles[i];
                    t.item = Item.FromJson(at(a, 0)); t.fire = num(at(a, 1)); t.count = (int)num(at(a, 2)); t.dirty = (int)num(at(a, 3));
                    t.wash = num(at(a, 4)); t.brew = num(at(a, 5)); t.brewing = truthy(at(a, 6));
                    t.jar = at(a, 7) is List<object> jar ? J.Strs(jar) : null;
                    t.blend = num(at(a, 8)); t.blending = truthy(at(a, 9)); t.@out = num(at(a, 10));
                }
            if (J.Get(m, "ch") is List<object> ch)
                for (int i = 0; i < ch.Count && i < G.chefs.Count; i++)
                {
                    if (!(ch[i] is List<object> a)) continue;
                    var c = G.chefs[i];
                    double x = num(at(a, 0)), y = num(at(a, 1));
                    c.held = Item.FromJson(at(a, 4));
                    c.task = at(a, 5) is List<object> tk ? new ChefTask { kind = at(tk, 0) as string, tile = (int)num(at(tk, 1)) is int ti && ti >= 0 && ti < G.tiles.Count ? G.tiles[ti] : null } : null;
                    c.toast = at(a, 8) as string; c.toastT = num(at(a, 9)); c.spraying = truthy(at(a, 10));
                    if (i == G.active)
                    {
                        // Our own chef is predicted locally; pull it gently towards the host's copy.
                        double ex = x - c.x, ey = y - c.y, err = U.Hypot(ex, ey);
                        if (err > 0.9) { c.x = x; c.y = y; }
                        else if (!c.moving) { c.x += ex * 0.3; c.y += ey * 0.3; }
                    }
                    else
                    {
                        c.tx = x; c.ty = y; c.fx = num(at(a, 2)); c.fy = num(at(a, 3)); c.moving = truthy(at(a, 6)); c.walk = num(at(a, 7));
                    }
                }
            G.evt = J.Get(m, "xe") is List<object> xe ? new Evt { kind = at(xe, 0) as string, name = at(xe, 1) as string, t = num(at(xe, 2)) } : null;
            if (J.Get(m, "or") is List<object> or)
                G.orders = or.OfType<List<object>>().Select(a =>
                {
                    int id = (int)num(at(a, 0));
                    var old = G.orders.Find(o => o.id == id);
                    double dl = num(at(a, 6));
                    return new Order { id = id, recipe = at(a, 1) as string, total = num(at(a, 2)), timeLeft = num(at(a, 3)), items = J.Strs(at(a, 4)), reward = num(at(a, 5)), delivery = dl > 0, late = dl == 2, vip = truthy(at(a, 7)), x = old?.x };
                }).Where(o => o.recipe != null && RECIPES.ContainsKey(o.recipe)).ToList();
            if (J.Get(m, "jd") is List<object> jd)
            {
                int strikes = (int)num(at(jd, 0));
                if (strikes > G.strikes) G.shake = 0.4;
                G.strikes = strikes; G.kicked = truthy(at(jd, 4)); G.clock = num(at(jd, 5));
                G.judge = at(jd, 1) is string text ? new JudgeState { text = text, t = num(at(jd, 2)), mood = (int)num(at(jd, 3)), who = at(jd, 6) as string } : null;
            }
            string prevPhase = G.phase;
            if (J.Get(m, "g") is List<object> g)
            {
                G.score = num(at(g, 0)); G.tips = num(at(g, 1)); G.served = (int)num(at(g, 2)); G.failed = (int)num(at(g, 3)); G.time = num(at(g, 4));
                G.phase = at(g, 5) as string ?? G.phase; G.countdown = num(at(g, 6)); G.endT = num(at(g, 7)); G.missLoss = num(at(g, 8));
            }
            if (G.phase == "over" && prevPhase != "over") foreach (var c in G.chefs) c.task = null;
            if (J.Get(m, "ev") is List<object> ev)
                foreach (var eo in ev)
                {
                    if (!(eo is List<object> e)) continue;
                    string k = at(e, 0) as string;
                    if (k == "s") { if (at(e, 1) is string n) sfx(n); }
                    else if (k == "f") G.floats.Add(new Float { at = at(e, 1) as string, x = num(at(e, 2)), y = num(at(e, 3)), dy = 0, text = at(e, 4) as string ?? "", color = at(e, 5) as string ?? "#fff", sub = at(e, 6) as string, life = 1.3 });
                    else if (k == "p") G.puffs.Add(new Puff { x = num(at(e, 1)), y = num(at(e, 2)), vx = num(at(e, 3)), vy = num(at(e, 4)), color = at(e, 5) as string ?? "#fff", life = num(at(e, 6)), max = num(at(e, 6)), kind = at(e, 7) as string });
                }
        }

        // Runs every frame while online, playing or not.
        void netTick(double dt)
        {
            netPoll();
            if (!netOpen() || G == null) return;
            if (G.versus) { vsTick(dt); return; }
            if (NET.role == "host")
            {
                NET.snapT -= dt;
                if (NET.snapT <= 0) { NET.snapT = SNAP_RATE; netSend(snapshot()); }
            }
        }

        /* ---------- versus: everyone races their own copy of the kitchen ---------- */

        static readonly string[] VS_NAMES = { "Red", "Blue", "Yellow", "Green" };
        int vsMe() => NET.role == "host" ? 0 : NET.slot;
        string vsLabel(int slot) => slot == vsMe() ? "You" : slot >= 0 && slot < VS_NAMES.Length ? VS_NAMES[slot] : "Friend";
        List<(int slot, VsRow r)> vsRows() => NET.vs.OrderBy(kv => kv.Key).OrderByDescending(kv => kv.Value.score).Select(kv => (kv.Key, kv.Value)).ToList();
        // Scores go round a few times a second: guests tell the host, the host tells everyone.
        void vsTick(double dt)
        {
            NET.vsT -= dt;
            if (NET.vsT > 0) return;
            NET.vsT = 0.3;
            var mine = new VsRow { score = G.score, served = G.served, done = G.phase == "over" };
            NET.vs[vsMe()] = mine;
            if (NET.role == "host")
                netSend(Msg("vsboard", ("b", NET.vs.ToDictionary(kv => kv.Key.ToString(INV), kv => (object)new Dictionary<string, object> { ["score"] = kv.Value.score, ["served"] = kv.Value.served, ["done"] = kv.Value.done, ["left"] = kv.Value.left }))));
            else netSend(Msg("vs", ("score", mine.score), ("served", mine.served), ("done", mine.done)));
            // The results card redraws vsSummary() every frame.
        }
        static string ordinal(int n) { int k = n % 100 > 10 && n % 100 < 14 ? 0 : n % 10; return n + (k < 4 ? new[] { "th", "st", "nd", "rd" }[k] : "th"); }
        public string vsSummary()
        {
            var rows = vsRows();
            int rank = rows.FindIndex(r => r.slot == vsMe()) + 1;
            bool all = rows.All(r => r.r.done || r.r.left);
            string list = string.Join(" · ", rows.Select(r => $"{vsLabel(r.slot)} {U.S(r.r.score)}{(r.r.left ? " (left)" : "")}"));
            bool tie = rows.Count > 1 && rows[0].r.score == rows[1].r.score && rank >= 1 && rank <= 2 && rows[rank - 1].r.score == rows[0].r.score;
            string head = !all ? "Waiting for everyone to finish… " : tie ? "A tie for first! " : rank == 1 ? "You win! " : $"You came {ordinal(rank)}. ";
            return head + list + (NET.role == "guest" && all ? ". The host picks the next kitchen." : "");
        }
        // The live race board, under the tickets.
        void drawVsBoard()
        {
            var rows = vsRows();
            if (rows.Count < 2) return;
            double x = L.orders.x, y = L.orders.y + L.orders.h + 8;
            X.font = "700 13px \"Pixelify Sans\", \"Courier New\", monospace"; X.textBaseline = "middle"; X.textAlign = "left";
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                string text = $"{i + 1}. {vsLabel(r.slot)} {U.S(r.r.score)}";
                double w = X.measureText(text).width + 24;
                X.fillStyle = "rgba(0,0,0,.35)"; X.fillRect(x, y, w, 20);
                X.fillStyle = r.slot >= 0 && r.slot < Data.CHEF_COLORS.Length ? Data.CHEF_COLORS[r.slot] : "#fff"; X.fillRect(x + 5, y + 5, 10, 10);
                X.fillStyle = r.r.left ? "#8a8f9c" : "#fff5de"; X.fillText(text, x + 19, y + 11);
                x += w + 6;
            }
        }
        public void setMode(string m)
        {
            NET.mode = m == "versus" ? "versus" : "coop";
            if (NET.role == "host") netSend(Msg("mode", ("m", NET.mode)));
            syncOnlineBanner();
        }

        // Host, from play(): a fresh scoreboard for a versus race.
        void netStartVersus()
        {
            NET.vs = new Dictionary<int, VsRow> { [0] = new VsRow() };
            for (int k = 0; k < NET.guests.Count; k++) NET.vs[k + 1] = new VsRow();
            NET.vsT = 0;
        }
        // Host, from play(): every friend gets the kitchen, the seeds and their chef.
        void netStartGuests(int i, bool versus, double seed)
        {
            // Chef slots are fixed for the whole service, so a friend leaving doesn't reshuffle anyone.
            for (int k = 0; k < NET.guests.Count; k++) { var g = NET.guests[k]; g.slot = k + 1; g.remote = new RemoteInput(); }
            foreach (var g in NET.guests)
                NET.transport?.Send(g.id, Json.Write(Msg("start", ("lv", i), ("tr", G.transposed), ("dk", DAILY?.key), ("db", DAILY != null ? (object)DAILY.@base : null), ("n", G.chefs.Count), ("me", g.slot),
                    ("fits", NET.fits), ("gears", NET.gears), ("vs", versus ? (object)seed : null), ("vary", G.vary), ("events", G.eventsOn))));
            NET.snapT = 0;
        }
        // Your outfit or gear changed in the wardrobe or shop: tell the others.
        void netFitChanged()
        {
            if (NET.role == "guest") netSend(Msg("fit", ("o", save.outfits[0] ?? "classic"), ("g", myLoadout(0))));
            else if (NET.role == "host") sendFits();
        }

        // The guest's frame: predict our own chef, smooth the others, send inputs.
        void updateGuest(double dt)
        {
            updateFx(dt);
            var me = G.chefs[G.active];
            double mx = 0, my = 0;
            if (G.phase == "play")
            {
                var mv = readMove(); mx = mv[0]; my = mv[1];
                moveChef(me, mx, my, dt);
                me.target = findTarget(me);
            }
            else me.moving = false;
            foreach (var c in G.chefs)
            {
                if (c == me) continue;
                if (c.tx != null) { double k = Math.Min(1, dt * 14); c.x += (c.tx.Value - c.x) * k; c.y += (c.ty.Value - c.y) * k; }
                c.target = findTarget(c);
            }
            foreach (var c in G.chefs) if (c.spraying) sprayPuffs(c);
            if (input.grab) netSend(Msg("act", ("a", "grab")));
            if (input.chop) netSend(Msg("act", ("a", "chop")));
            input.grab = input.chop = input.swap = false;
            NET.sendT -= dt;
            string state = mx.ToString("0.00", INV) + "," + my.ToString("0.00", INV) + "," + (input.chopHeld ? 1 : 0);
            if (state != NET.lastSent || NET.sendT <= 0)
            {
                NET.lastSent = state; NET.sendT = 0.25;
                netSend(Msg("in", ("mx", fix(mx, 2)), ("my", fix(my, 2)), ("held", input.chopHeld)));
            }
            if (G.phase == "over" && G.endT > 1.6 && !G.shown) { G.shown = true; showResults(); }
        }
    }
}
