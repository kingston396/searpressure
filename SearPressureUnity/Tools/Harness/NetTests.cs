using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using SearPressure;

// Online scenarios: several Game instances in one process, talking through a local relay (Server/relay.js).
//   net    host + guests: co-op start, snapshots, guest input, pause, a guest leaving, busy rejection, versus
//   relay  three guests join, a fifth player is turned away as full, an unknown code is rejected
sealed class NetPlatform : IPlatform
{
    public string relay, saveJson;
    public List<string> sounds = new List<string>();
    public string LoadSave() => saveJson;
    public void WriteSave(string json) { saveJson = json; }
    public void Vibrate(int ms) { }
    public void PlaySound(string name) { sounds.Add(name); }
    public void SetMusic(string key, bool rush) { }
    public DateTime UtcNow => new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
    public bool UnlockAll => true;
    public bool Calm => true;
    public INetTransport CreateTransport() => new WsRelayTransport(relay);
    public void OpenKeyboard(string text, int maxLength) { }
}

static partial class Tests
{
    static int netPass, netFail;
    static void NCheck(bool ok, string what)
    {
        if (ok) netPass++; else netFail++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + what);
    }

    static partial void RunNet(string which, ref bool handled)
    {
        if (which != "net" && which != "relay") return;
        handled = true;
        int port = FreePort();
        using (var relay = StartRelay(port))
        {
            try
            {
                if (which == "net") NetScenario("ws://127.0.0.1:" + port);
                else RelayScenario("ws://127.0.0.1:" + port);
            }
            finally { try { relay.Kill(); } catch (Exception) { } }
        }
        Console.WriteLine($"{which}: {netPass} passed, {netFail} failed");
        if (netFail > 0) Environment.ExitCode = 1;
    }

    static int FreePort()
    {
        var l = new TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start(); int p = ((System.Net.IPEndPoint)l.LocalEndpoint).Port; l.Stop();
        return p;
    }

    static Process StartRelay(int port)
    {
        string dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Server"));
        var psi = new ProcessStartInfo("node", "relay.js") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.Environment["PORT"] = port.ToString();
        var p = Process.Start(psi);
        p.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine("relay! " + e.Data); };
        p.BeginErrorReadLine();
        string line = p.StandardOutput.ReadLine();   // "Sear Pressure relay on :PORT"
        Console.WriteLine("relay: " + line);
        return p;
    }

    // Games step in turn, a frame each, with a little real time between so the sockets keep up.
    static void Step(Game[] gs, double seconds)
    {
        for (double t = 0; t < seconds; t += 1 / 60.0) { foreach (var g in gs) g.Frame(1 / 60.0); Thread.Sleep(1); }
    }
    static bool WaitFor(Game[] gs, Func<bool> cond, double wallSeconds = 5)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < wallSeconds)
        {
            if (cond()) return true;
            foreach (var g in gs) g.Frame(1 / 60.0);
            Thread.Sleep(2);
        }
        return cond();
    }
    static void Type(Game g, string code) { g.typed = code; }

    static Game[] Games(string url, int n)
    {
        var list = new List<Game>();
        for (int i = 0; i < n; i++)
        {
            var p = new NetPlatform { relay = url };
            Sprites.Reset();
            var g = new Game(p, new Canvas { Fonts = Program.Fonts });
            g.Resize(390, 844, 2, 0, 0, 0, 0);
            g.Frame(1 / 60.0);   // boot to the title screen
            list.Add(g);
        }
        return list.ToArray();
    }

    // Walk the guest's own chef to a floor tile next to a crate, face the crate and press grab.
    static bool WalkToCrateAndGrab(Game guest, Game[] all)
    {
        var G = guest.G;
        var me = G.chefs[G.active];
        var dirs = new[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
        var spots = new List<(Tile crate, int fx, int fy, double sx, double sy)>();
        foreach (var t in G.tiles.Where(t => t.type == "crate"))
            foreach (var (dx, dy) in dirs)
            {
                var f = G.TileAt(t.x + dx, t.y + dy);
                if (f != null && f.type == "floor") spots.Add((t, -dx, -dy, f.x + 0.5, f.y + 0.5));
            }
        var spot = spots.OrderBy(s => U.Hypot(s.sx - me.x, s.sy - me.y)).First();
        Console.WriteLine($"  guest walks from ({me.x:0.00},{me.y:0.00}) to ({spot.sx},{spot.sy}) for the {spot.crate.kind} crate");
        var keys = guest.input.keys;
        for (int i = 0; i < 600; i++)
        {
            double ex = spot.sx - me.x, ey = spot.sy - me.y;
            keys["KeyD"] = ex > 0.06; keys["KeyA"] = ex < -0.06; keys["KeyS"] = ey > 0.06; keys["KeyW"] = ey < -0.06;
            if (Math.Abs(ex) <= 0.06 && Math.Abs(ey) <= 0.06) break;
            Step(all, 1 / 60.0);
        }
        keys.Clear();
        // Face the crate: a tap towards it (the counter stops us).
        string face = spot.fx > 0 ? "KeyD" : spot.fx < 0 ? "KeyA" : spot.fy > 0 ? "KeyS" : "KeyW";
        keys[face] = true; Step(all, 0.1); keys.Clear(); Step(all, 0.1);
        Console.WriteLine($"  guest target: {(me.target != null ? me.target.type + " " + me.target.kind : "none")}");
        // Press the grab button like a finger would.
        guest.PointerDown(7, guest.L.btnA.x, guest.L.btnA.y); guest.Frame(1 / 60.0); guest.PointerUp(7, guest.L.btnA.x, guest.L.btnA.y);
        return me.target == spot.crate;
    }

    static void NetScenario(string url)
    {
        var gs = Games(url, 4);
        Game host = gs[0], guest = gs[1], guest2 = gs[2], late = gs[3];
        var all = new[] { host, guest, guest2, late };

        // ---- hosting and joining ----
        host.hostGame();
        NCheck(WaitFor(all, () => host.NET.code.Length == 4), "host gets a room code: " + host.NET.code + " / " + host.OnlineStatus + " / " + host.NET.role);
        NCheck(host.NET.code.All(c => Game.CODE_CHARS.IndexOf(c) >= 0), "room code uses CODE_CHARS");
        Type(guest, host.NET.code); guest.joinGame();
        NCheck(WaitFor(all, () => host.NET.guests.Count == 1 && guest.NET.joined), "guest joins with the code");
        NCheck(WaitFor(all, () => host.NET.guests.Count == 1 && host.NET.fits.Count == 2 && guest.NET.fits.Count == 2), "fits go round (host and guest see 2 chefs)");
        NCheck(host.screen == "scr-title" && guest.screen == "scr-online", $"host on title, guest waits on the online screen ({host.screen}/{guest.screen})");
        NCheck(guest.OnlineStatus.StartsWith("Connected!"), "guest status: " + guest.OnlineStatus);
        Type(guest2, host.NET.code); guest2.joinGame();
        NCheck(WaitFor(all, () => host.NET.guests.Count == 2 && guest2.NET.joined && guest.NET.fits.Count == 3 && guest2.NET.fits.Count == 3), "second guest joins; everyone sees 3 chefs");
        Step(all, 0.3);
        Program.Shot(host, "net_host_lobby.png");
        Program.Shot(guest, "net_guest_lobby.png");

        // ---- co-op: the host picks kitchen 0 ----
        host.play(0);
        NCheck(host.G != null && host.G.chefs.Count == 3, "host kitchen has 3 chefs");
        NCheck(WaitFor(all, () => guest.G != null && guest2.G != null), "guests receive 'start'");
        NCheck(guest.G != null && guest.G.active == 1 && guest2.G.active == 2, $"guests drive their own chef (slots {guest.G?.active}, {guest2.G?.active})");
        NCheck(guest.G.lvIdx == 0 && guest.G.transposed == host.G.transposed && guest.G.cols == host.G.cols, "guest kitchen matches the host's layout");

        // A friend can't join halfway through a service.
        Type(late, host.NET.code); late.joinGame();
        NCheck(WaitFor(all, () => late.NET.role == null && late.OnlineStatus.Contains("middle of a service")), "late joiner turned away: " + late.OnlineStatus);
        NCheck(host.NET.guests.Count == 2, "host still has 2 guests");

        NCheck(WaitFor(all, () => host.G.phase == "play" && guest.G.phase == "play", 10), "countdown ends on host and guest");
        host.spawnOrder(); host.spawnOrder();
        host.G.score = 42;
        Step(all, 0.5);
        NCheck(WaitFor(all, () => guest.G.score == 42 && guest.G.orders.Count == host.G.orders.Count && guest.G.orders.Select(o => o.id).SequenceEqual(host.G.orders.Select(o => o.id))),
            $"snapshots: guest score {guest.G.score} / host {host.G.score}, orders {string.Join(",", guest.G.orders.Select(o => o.recipe))} / {string.Join(",", host.G.orders.Select(o => o.recipe))}");
        NCheck(Math.Abs(guest.G.time - host.G.time) < 0.5, $"clock in step (guest {guest.G.time:0.0}, host {host.G.time:0.0})");

        // ---- the guest walks and grabs; the host's copy of their chef follows ----
        var hc = host.G.chefs[1];
        double x0 = hc.x, y0 = hc.y;
        bool facing = WalkToCrateAndGrab(guest, all);
        NCheck(facing, "guest's chef faces a crate");
        NCheck(WaitFor(all, () => U.Hypot(hc.x - x0, hc.y - y0) > 0.5), $"host sees the guest chef move ({x0:0.00},{y0:0.00}) -> ({hc.x:0.00},{hc.y:0.00})");
        NCheck(WaitFor(all, () => hc.held != null), "host: guest chef picked up " + (hc.held != null ? hc.held.type + " " + hc.held.kind : "nothing"));
        NCheck(WaitFor(all, () => guest.G.chefs[1].held != null && guest.G.chefs[1].held.kind == hc.held?.kind), "guest's snapshot shows what they hold");
        var gc = guest.G.chefs[1];
        NCheck(U.Hypot(gc.x - hc.x, gc.y - hc.y) < 0.3, $"guest prediction close to host ({gc.x:0.00},{gc.y:0.00}) vs ({hc.x:0.00},{hc.y:0.00})");
        // Guest 2 sees chef 1 through snapshots too.
        NCheck(WaitFor(all, () => U.Hypot(guest2.G.chefs[1].x - hc.x, guest2.G.chefs[1].y - hc.y) < 0.3), "other guest sees chef 1 where the host has it");
        Step(all, 0.3);
        Program.Shot(host, "net_host_coop.png");
        Program.Shot(guest, "net_guest_coop.png");

        // ---- pause propagation ----
        guest.pauseGame();
        NCheck(guest.paused && guest.screen == "scr-pause", "guest pauses");
        NCheck(WaitFor(all, () => host.paused && host.screen == "scr-pause" && guest2.paused), "host and other guest pause too");
        double tPaused = host.G.time;
        Step(all, 0.5);
        NCheck(host.G.time == tPaused, "host clock stops while paused");
        Program.Shot(guest2, "net_guest2_paused.png");
        host.resumeGame();
        NCheck(WaitFor(all, () => !guest.paused && !guest2.paused && guest.screen == null), "host resumes everyone");
        Step(all, 0.3);
        NCheck(host.G.time < tPaused, "host clock runs again");

        // ---- a guest leaves mid-service: the host plays on ----
        guest2.pauseGame();
        NCheck(WaitFor(all, () => host.paused), "leaving guest pauses everyone on the way out (like the web)");
        guest2.toMenu();   // "Leave" on the pause card
        NCheck(guest2.NET.role == null && guest2.G == null, "leaving guest is back offline");
        NCheck(WaitFor(all, () => host.NET.guests.Count == 1 && !host.paused), "host drops them and unpauses");
        NCheck(host.G != null && host.G.phase == "play", "host keeps playing");
        NCheck(host.G.floats.Any(f => f.text == "A friend left"), "host shows 'A friend left'");
        double gScore = guest.G.time;
        Step(all, 0.5);
        NCheck(guest.G != null && guest.G.time < gScore && !guest.paused, "remaining guest keeps getting snapshots");
        Program.Shot(host, "net_host_friend_left.png");

        // ---- versus ----
        host.toMenu();   // "Pick another kitchen"
        NCheck(WaitFor(all, () => guest.G == null && guest.screen == "scr-online" && host.screen == "scr-title"), "back to the lobby for everyone");
        host.setMode("versus");
        NCheck(WaitFor(all, () => guest.NET.mode == "versus"), "guest hears the mode switch");
        Program.Shot(host, "net_host_lobby_versus.png");
        host.play(0);
        NCheck(WaitFor(all, () => guest.G != null && guest.G.versus), "guest gets a versus kitchen");
        NCheck(host.G.versus && host.G.chefs.Count == 2 && guest.G.chefs.Count == 2 && guest.G.active == 0, "each phone runs its own two-chef kitchen");
        NCheck(WaitFor(all, () => host.G.phase == "play" && guest.G.phase == "play", 10), "both kitchens start");
        // Same seed: same tickets.
        Step(all, 3);
        NCheck(host.G.orders.Select(o => o.recipe).Take(1).SequenceEqual(guest.G.orders.Select(o => o.recipe).Take(1)) && host.G.orders.Count > 0, $"same first ticket ({host.G.orders.FirstOrDefault()?.recipe} / {guest.G.orders.FirstOrDefault()?.recipe})");
        // Guest input stays on the guest's phone.
        var hostC = host.G.chefs[0]; double hx = hostC.x, hy = hostC.y;
        guest.input.keys["KeyA"] = true; Step(all, 0.5); guest.input.keys.Clear();
        NCheck(hostC.x == hx && hostC.y == hy, "guest walking doesn't move the host's chefs");
        host.G.score = 30; guest.G.score = 55;
        NCheck(WaitFor(all, () => guest.NET.vs.TryGetValue(0, out var r) && r.score == 30 && host.NET.vs.TryGetValue(1, out var r1) && r1.score == 55), "vs board: scores go round");
        Step(all, 0.3);
        Console.WriteLine("  host summary: " + host.vsSummary());
        Console.WriteLine("  guest summary: " + guest.vsSummary());
        NCheck(guest.vsSummary().Contains("Waiting") && guest.vsSummary().Contains("You 55"), "guest summary while racing");
        Program.Shot(host, "net_host_versus.png");
        Program.Shot(guest, "net_guest_versus.png");
        // Finish both: results with a winner.
        host.G.time = 0.01; guest.G.time = 0.01;
        NCheck(WaitFor(all, () => host.screen == "scr-results" && guest.screen == "scr-results" && host.vsSummary().StartsWith("You came 2nd") && guest.vsSummary().StartsWith("You win!"), 10), "versus results: " + guest.vsSummary());
        Step(all, 0.2);
        Program.Shot(guest, "net_guest_versus_results.png");

        // ---- the host leaves: the guest is told ----
        host.netLeave(); host.show("scr-title");
        NCheck(WaitFor(all, () => guest.NET.role == null && guest.screen == "scr-online"), "guest is dropped when the host leaves: " + guest.OnlineStatus);
    }

    static void RelayScenario(string url)
    {
        var gs = Games(url, 6);
        var host = gs[0];
        host.hostGame();
        NCheck(WaitFor(gs, () => host.NET.code.Length == 4), "host gets code " + host.NET.code);
        for (int i = 1; i <= 3; i++)
        {
            Type(gs[i], host.NET.code); gs[i].joinGame();
            int n = i;
            NCheck(WaitFor(gs, () => host.NET.guests.Count == n && gs[n].NET.joined), $"guest {i} joins");
        }
        NCheck(WaitFor(gs, () => gs.Take(4).All(g => g.netPlayers() == 4)), "everyone counts 4 players");
        Type(gs[4], host.NET.code); gs[4].joinGame();
        NCheck(WaitFor(gs, () => gs[4].NET.role == null && gs[4].OnlineStatus.Contains("full")), "5th player rejected: " + gs[4].OnlineStatus);
        NCheck(host.NET.guests.Count == 3, "host still has 3 guests");
        string bad = host.NET.code == "ZZZZ" ? "YYYY" : "ZZZZ";
        Type(gs[5], bad); gs[5].joinGame();
        NCheck(WaitFor(gs, () => gs[5].NET.role == null && gs[5].OnlineStatus.StartsWith("No kitchen found")), "unknown code rejected: " + gs[5].OnlineStatus);
        // Co-op with four: every guest gets a chef.
        host.play(0);
        NCheck(WaitFor(gs, () => gs.Skip(1).Take(3).All(g => g.G != null)), "all three guests start");
        NCheck(host.G.chefs.Count == 4 && gs[3].G.active == 3, "4 chefs, the last guest drives chef 3");
        Step(gs, 4);
        Program.Shot(gs[3], "relay_guest3_coop4.png");
        // One guest drops without a goodbye (socket just closes): the host plays on.
        var tr = gs[2].NET.transport; gs[2].NET.transport = null; tr.Close();
        NCheck(WaitFor(gs, () => host.NET.guests.Count == 2), "a guest's socket closing tells the host");
        NCheck(host.G != null, "host keeps playing");
    }
}
