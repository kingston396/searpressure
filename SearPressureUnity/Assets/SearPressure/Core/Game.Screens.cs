using System;
using System.Collections.Generic;
using System.Linq;

namespace SearPressure
{
    // What the kitchen intro card shows (the web version fills these DOM elements in openIntro).
    public sealed class IntroModel
    {
        public string no, time, name, tip, note, coins, stars, goLabel = "Open kitchen";
        public bool showMenu = true, driveTutButton;
        public List<string> dishes = new List<string>();
        public List<string> helperOffers = new List<string>();
        public int hires;
        public string helperNote;
        public Action onGo;
    }
    // What the results card shows.
    public sealed class ResultsModel
    {
        public string no, over = "Service over", name, l1 = "Orders served", l2 = "Orders missed", served, failed, tips, score, wallet, next, menuLabel = "All kitchens";
        public int stars;
        public bool nextHidden, retryHidden;
        public Action onRetry, onNext;
    }

    public sealed partial class Game
    {
        public IntroModel intro = new IntroModel();
        public ResultsModel results = new ResultsModel();
        public int introIdx;
        public List<string> chosenHelpers = new List<string>();
        string settingsBack = "scr-title";
        string shopTab = "gear", shopConfirm;
        int wardSlot; string wardBack = "scr-title";
        StoryRun storyState;
        sealed class StoryRun { public int stop, line; public Action then; }
        string onlineStatus = "Both phones need an internet connection.";
        string onlineCode = "";
        bool onlineButtonsEnabled = true;

        // ---- small helpers the web version used for DOM text ----
        public void buildLevelList() { }            // the list is drawn live every frame
        void syncMute() { }
        void syncTutorialButton() { }
        string starText(int n) => new string('★', n);

        void BuildScreen(string id)
        {
            switch (id)
            {
                case "scr-title": TitleScreen(); break;
                case "scr-intro": IntroScreen(); break;
                case "scr-results": ResultsScreen(); break;
                case "scr-pause": PauseScreen(); break;
                case "scr-howto": HowtoScreen(); break;
                case "scr-settings": SettingsScreen(); break;
                case "scr-shop": ShopScreen(); break;
                case "scr-wardrobe": WardrobeScreen(); break;
                case "scr-story": StoryScreen(); break;
                case "scr-online": OnlineScreen(); break;
            }
        }

        /* ---------------- title: the road trip, one ticket ---------------- */
        void TitleScreen()
        {
            if (MonoWithLink("Table for two chefs", "btn-settings", "Settings")) openSettings("scr-title");
            // Big two-line title.
            double big = Math.Max(46, Math.Min(68, W * 0.14));
            Custom(big * 0.86 * 2, r =>
            {
                X.font = "700 " + U.S(big) + "px \"Pixelify Sans\""; X.textAlign = "left"; X.textBaseline = "middle";
                X.fillStyle = TOMATO; X.fillText("Sear", r.x, r.y + big * 0.43);
                X.fillStyle = TEXT; X.fillText("Pressure", r.x, r.y + big * 0.43 + big * 0.86);
            });
            Para("One food truck, two chefs and a line out the window. Burgers sizzle, pots boil over and the fryer catches fire on a ten-stop road trip across America. Can you take the heat?");
            Rule();
            if (NET.role == null && Button("btn-tutorial", save.tutorialDone ? "Replay the tutorial" : "New here? Start with the tutorial", save.tutorialDone ? "ghost" : "btn")) startTutorial();
            bool on = NET.role != null && (NET.role == "host" ? NET.guests.Count > 0 : true);
            if (!on && Button("btn-online", "Play with a friend", "alt")) { onlineButtonsEnabled = true; onlineStatus = "Both phones need an internet connection. Once you're connected, the game data goes between your phones through the relay server."; show("scr-online"); }
            if (Button("btn-shop", "Shop", "ghost", fmtCoins(save.wallet) + " coins to spend")) openShop(null);
            var dl = dailyLevel();
            if (Button("btn-daily", "Daily challenge", "ghost", DAILY.twist.name + " at " + dl.baseName + " · " + (questDone() ? "Quest done ✓" : "Quest: " + QUEST_REWARD + " coins"))) { dailyLevel(); openIntro(DAILY_IDX); }
            if (on) OnlineBanner();
            Mono("Pick a kitchen");
            LevelList();
            DriveList();
            var b = BtnRow(("btn-wardrobe", "Wardrobe", "ghost", false), ("btn-howto", "How to play", "ghost", false));
            if (b == "btn-wardrobe") openWardrobe();
            if (b == "btn-howto") show("scr-howto");
            cy -= GAP;
        }

        void OnlineBanner()
        {
            int n = netPlayers() - 1; string friends = n == 1 ? "a friend" : n + " friends";
            string mode = NET.mode == "versus" ? "Versus: everyone races their own copy of the kitchen." : "Co-op: one kitchen, one chef each.";
            string text = NET.role == "host" ? $"Playing with {friends} (room {NET.code}). {mode} Pick a kitchen.{(n < MAX_PLAYERS - 1 ? " More can still join." : "")}" : $"Playing online with {n + 1} players. {mode}";
            var lines = Wrap(text, "600 14px Nunito", cw - 24 - 130);
            double h = Math.Max(lines.Count * 20 + 20, NET.role == "host" ? 90 : 50);
            Custom(h, r =>
            {
                RoundRect(r.x, r.y, r.w, r.h, 8, PAPER2);
                X.font = "600 14px Nunito"; X.fillStyle = TEXT; X.textAlign = "left"; X.textBaseline = "middle";
                for (int i = 0; i < lines.Count; i++) X.fillText(lines[i], r.x + 12, r.y + 10 + 20 * (i + 0.5));
                double bx = r.x + r.w - 12 - 124, by = r.y + 10;
                if (NET.role == "host")
                {
                    var rm = new Rect(bx, by, 124, 32); Hit("btn-mode", rm);
                    BtnVisual(rm, "alt small", NET.mode == "versus" ? "Switch to co-op" : "Switch to versus", null, uiPressed == "btn-mode", false);
                    if (Clicked("btn-mode")) { setMode(NET.mode == "versus" ? "coop" : "versus"); sfx("pick"); }
                    by += 40;
                }
                var rl = new Rect(bx, by, 124, 32); Hit("btn-online-leave", rl);
                BtnVisual(rl, "ghost small", "Leave", null, uiPressed == "btn-online-leave", false);
                if (Clicked("btn-online-leave")) { netLeave(); show("scr-title"); }
            });
        }

        void LevelList()
        {
            for (int si = 0; si < Data.STOPS.Count; si++)
            {
                if (!stopVisible(si)) continue;
                var stop = Data.STOPS[si];
                bool open = Enumerable.Range(0, Data.LEVELS.Count).Any(i => Data.LEVELS[i].stop == si && unlocked(i));
                string need = stop.extra ? $"{stop.blurb} You have {totalStars()} star{(totalStars() == 1 ? "" : "s")}." : si > 0 && !open ? (stop.bonus ? $"Earn a star on {Data.LEVELS[Data.LEVELS.FindIndex(l => l.stop == si) - 1].name} to earn a seat." : $"Collect {stop.needStars} stars in {Data.STOPS[si - 1].name} to get here.") : stop.blurb;
                StopHead(stop.bonus ? "Bonus challenge" : stop.extra ? "Bonus kitchens" : "Stop " + si, stop.name, need);
                for (int i = 0; i < Data.LEVELS.Count; i++)
                {
                    var lv = Data.LEVELS[i];
                    if (lv.stop != si || hiddenBonus(i)) continue;
                    bool ok = unlocked(i);
                    int li = i;
                    if (LevelRow("lvl-" + i, "No." + U.Pad2(i + 1), lv.name, ok ? bestStars(i) : -1, ok ? null : (lv.needTotal > 0 ? "★ " + lv.needTotal : "Locked"), lv.recipes, save.best.Has(i) ? "Best " + U.S(save.best[i]) : null, !ok, null))
                    { if (!maybeStory(li)) openIntro(li); }
                }
            }
        }
        void StopHead(string mono, string title, string blurb)
        {
            cy += 0;
            Mono(mono, null, false);
            cy += 2;
            H2(title, 24, TOMATO); cy -= GAP - 2;
            Para(blurb, "small", null, false);
            cy += 4;
        }
        // One kitchen in the list: number, name, stars, the dishes it serves.
        bool LevelRow(string id, string no, string name, int stars, string lockText, string[] dishes, string best, bool disabled, string dishText)
        {
            double h = 12 + 22 + 4 + 26 + 12;
            bool clicked = false;
            if (uiDraw)
            {
                var r = new Rect(cx0, cy, cw, h);
                if (!disabled) Hit(id, r);
                if (uiPressed == id) { X.fillStyle = PAPER2; X.fillRect(r.x, r.y, r.w, r.h); }
                X.globalAlpha = disabled ? 0.45 : 1;
                X.font = "500 13px \"DM Mono\""; X.fillStyle = MUTED; X.textAlign = "left"; X.textBaseline = "middle";
                X.fillText(no, r.x + 2, r.y + h / 2);
                X.font = "400 22px \"Pixelify Sans\""; X.fillStyle = TEXT;
                X.fillText(name, r.x + 56, r.y + 12 + 11);
                if (dishes != null) for (int d = 0; d < dishes.Length; d++) dishIcon(dishes[d], r.x + 56 + d * 28, r.y + 12 + 22 + 4, 26);
                if (dishText != null) { X.font = "600 13px Nunito"; X.fillStyle = MUTED; X.fillText(dishText, r.x + 56, r.y + 12 + 22 + 4 + 13); }
                if (stars >= 0) StarsAt(r.x + r.w - 4, r.y + h / 2 - (best != null ? 6 : 0), 20, stars, "right");
                else { X.font = "600 18px Nunito"; X.fillStyle = LINE; X.textAlign = "right"; X.fillText(lockText, r.x + r.w - 4, r.y + h / 2); }
                if (best != null) { X.font = "500 11px \"DM Mono\""; X.fillStyle = MUTED; X.textAlign = "right"; X.fillText(best, r.x + r.w - 4, r.y + h / 2 + 16); }
                X.globalAlpha = 1;
                // Dashed divider.
                X.fillStyle = LINE; for (double x = r.x; x < r.x + r.w; x += 9) X.fillRect(x, r.y + h - 2, Math.Min(5, r.x + r.w - x), 2);
                clicked = !disabled && Clicked(id);
            }
            cy += h;
            return clicked;
        }
        // Three stars, the first `n` gold.
        void StarsAt(double x, double y, double size, int n, string align = "center")
        {
            X.font = "600 " + U.S(size) + "px Nunito"; X.textBaseline = "middle"; X.textAlign = "left";
            double sw = X.measureText("★").width + size * 0.05, total = sw * 3;
            double x0 = align == "right" ? x - total : align == "center" ? x - total / 2 : x;
            for (int i = 0; i < 3; i++)
            {
                if (i < n) { X.fillStyle = MUSTARD_DK; X.fillText("★", x0 + i * sw, y + size * 0.05); X.fillStyle = MUSTARD; }
                else X.fillStyle = LINE;
                X.fillText("★", x0 + i * sw, y);
            }
        }

        // The secret Delivery Runs: only listed once one has been discovered.
        void DriveList()
        {
            if (NET.role != null || !Enumerable.Range(0, Data.DRIVE_RUNS.Count).Any(driveOpen)) return;
            StopHead("Secret bonus", "Delivery Runs", "Drive the orders yourself.");
            for (int k = 0; k < Data.DRIVE_RUNS.Count; k++)
            {
                if (!driveOpen(k)) continue;
                var run = Data.DRIVE_RUNS[k];
                double best = save.drive[k];
                int kk = k;
                if (LevelRow("drv-" + k, "Run " + (k + 1), run.name, best > 0 ? driveStars(k, best) : 0, null, null, best > 0 ? "Best " + U.S(best) : null, false, "Delivery driving")) openDriveIntro(kk);
            }
            cy += GAP;
        }

        /* ---------------- kitchen intro ---------------- */
        public void openIntro(int i)
        {
            introIdx = i;
            var lv = lvOf(i);
            var m = new IntroModel();
            m.no = lv.daily ? "Daily challenge · " + dailyDate() : Data.STOPS[lv.stop].name + " · No." + U.Pad2(i + 1);
            m.time = U.FmtTime(lv.time) + " service";
            m.name = lv.name;
            m.tip = lv.daily ? $"Daily quest ({QUEST_REWARD} coins{(questDone() ? ", done today" : "")}): score {fmtCoins(lv.stars[2])} or more AND {DAILY.quest.text.ToLowerInvariant()}. Today’s twist: {DAILY.twist.name}. {DAILY.twist.blurb} {lv.tip}" : lv.tip;
            m.dishes = lv.recipes.ToList();
            m.coins = lv.daily ? "" : coinNote(levelKey(i), starTargets(i, NET.mode == "versus" ? 1 : netPlayers())[2]);
            m.note = lv.note;
            int humans = NET.mode == "versus" ? 1 : netPlayers();
            m.stars = "Stars at " + string.Join(" / ", starTargets(i, humans).Select(U.S)) + " coins" + (lv.daily ? " · same tickets for everyone today" : "") + (NET.role != null && NET.mode == "versus" ? " · versus: same kitchen and tickets for everyone, highest score wins" : "") + (humans > 1 ? $" · co-op: {U.Round(COOP_STEP * (humans - 1) * 100)}% busier service" : "");
            // Solo players at the Judge's Table hire helpers from three random offers.
            chosenHelpers = new List<string>();
            m.helperOffers = NET.role == null && lv.judge ? helperChoices(lv) : new List<string>();
            m.hires = lv.judge ? helperHires(i) : 0;
            m.onGo = () => play(i);
            intro = m;
            show("scr-intro");
        }

        void IntroScreen()
        {
            var m = intro;
            Mono(m.no, m.time);
            H2(m.name);
            Rule();
            if (m.showMenu && m.dishes.Count > 0)
            {
                Mono("On the menu");
                Grid(m.dishes.Count, 96, 10, (i, w) => 64 + 4 + Wrap(RECIPES[m.dishes[i]].name, "800 13px Nunito", w).Count * 15 + 16, (i, r) =>
                {
                    var R = RECIPES[m.dishes[i]];
                    dishIcon(m.dishes[i], r.x + r.w / 2 - 32, r.y, 64);
                    var ln = Wrap(R.name, "800 13px Nunito", r.w);
                    X.font = "800 13px Nunito"; X.fillStyle = TEXT; X.textAlign = "center"; X.textBaseline = "middle";
                    for (int k = 0; k < ln.Count; k++) X.fillText(ln[k], r.x + r.w / 2, r.y + 68 + 15 * (k + 0.5));
                    X.font = "500 11px \"DM Mono\""; X.fillStyle = MUTED;
                    X.fillText(R.extras != null ? U.S(R.reward) + "+ · your way" : U.S(R.reward) + " + tip", r.x + r.w / 2, r.y + 68 + ln.Count * 15 + 8);
                });
            }
            Para(m.tip, "tip");
            if (!string.IsNullOrEmpty(m.note)) Para(m.note, "small");
            if (!string.IsNullOrEmpty(m.coins)) Para(m.coins, "wallet");
            if (m.helperOffers.Count > 0)
            {
                Mono($"Hire up to {m.hires} helper{(m.hires > 1 ? "s" : "")} for this shift");
                Grid(m.helperOffers.Count, 96, 8, (i, w) => 8 + 40 + 4 + 18 + Wrap(Data.HelperById(m.helperOffers[i]).blurb, "600 12px Nunito", w - 12).Count * 15 + 8, (i, r) =>
                {
                    var hd = Data.HelperById(m.helperOffers[i]);
                    string id = "helper-" + hd.id; Hit(id, r);
                    bool on = chosenHelpers.Contains(hd.id);
                    RoundRect(r.x + 1, r.y + 1, r.w - 2, r.h - 2, 10, on ? "#f3ecff" : "#ffffff", on ? "#8a5ad4" : LINE, on ? 3 : 2);
                    X.fillStyle = "#e9e0f8"; X.fillRect(r.x + r.w / 2 - 20, r.y + 8, 40, 40);
                    spr(hd.icon.name, r.x + r.w / 2, r.y + 28, 3, hd.icon.sw);
                    X.font = "700 15px \"Pixelify Sans\""; X.fillStyle = TEXT; X.textAlign = "center"; X.textBaseline = "middle";
                    X.fillText(hd.name, r.x + r.w / 2, r.y + 8 + 40 + 4 + 9);
                    var ln = Wrap(hd.blurb, "600 12px Nunito", r.w - 12);
                    X.font = "600 12px Nunito"; X.fillStyle = MUTED;
                    for (int k = 0; k < ln.Count; k++) X.fillText(ln[k], r.x + r.w / 2, r.y + 70 + 15 * (k + 0.5));
                    if (Clicked(id))
                    {
                        if (chosenHelpers.Contains(hd.id)) chosenHelpers.Remove(hd.id);
                        else { chosenHelpers.Add(hd.id); if (chosenHelpers.Count > m.hires) chosenHelpers.RemoveAt(0); }
                        sfx("pick");
                    }
                });
                string baseNote = $"You earned {m.hires} with {m.hires} star{(m.hires > 1 ? "s" : "")} on {Data.LEVELS[introIdx - 1].name}{(m.hires < 3 ? ". More stars there means more help" : "")}. Each helper does one job.";
                Para(chosenHelpers.Count > 0 ? $"Hired: {string.Join(", ", chosenHelpers.Select(k => Data.HelperById(k).name))} ({chosenHelpers.Count} of {m.hires}). Tap to let one go." : baseNote, "small");
            }
            Mono(m.stars);
            var b = BtnRow(("btn-intro-back", "Back", "ghost", false), ("btn-intro-go", m.goLabel, "btn", false));
            if (b == "btn-intro-back") show("scr-title");
            if (b == "btn-intro-go") m.onGo?.Invoke();
            if (m.driveTutButton && Button("btn-drive-tut", "Replay the driving lesson", "ghost small")) startDrive(0, true);
            cy -= GAP;
        }

        public void play(int i)
        {
            if (NET.role == "guest") return;
            bool versus = NET.role == "host" && NET.mode == "versus";
            double seed = Math.Floor(Random() * 2147483647);
            paused = false; show(null);
            startLevel(i, new StartOpts { players = netPlayers(), versus = versus, seed = versus ? seed : (double?)null });
            if (versus) netStartVersus();
            if (NET.role == null && lvOf(i).judge) foreach (var k in chosenHelpers.Take(helperHires(i)).ToList()) spawnHelper(k);
            if (NET.role == "host") netStartGuests(i, versus, seed);
        }

        void syncPauseButtons() { }
        public void pauseGame()
        {
            if (D != null) { if (D.phase == "over") return; paused = true; input.joy.id = null; D.hand = false; show("scr-pause"); return; }
            if (G == null || G.phase == "over") return;
            if (NET.role != null) { setPaused(true, true); return; }
            paused = true; input.joy.id = null; show("scr-pause");
        }
        public void resumeGame() { if (NET.role != null) setPaused(false, true); else { paused = false; show(null); } }
        public void toMenu()
        {
            if (NET.role == "host") { enterOnlineMenu(); return; }
            if (NET.role == "guest") netLeave();
            if (D != null) { engineStop(); D = null; }
            G = null; paused = false; tutHidden = true; buildLevelList(); show("scr-title");
        }

        void PauseScreen()
        {
            bool guest = NET.role == "guest";
            Mono("Service paused");
            H2("Paused");
            if (Button("btn-resume", "Resume")) resumeGame();
            var b = BtnRow(("btn-restart", "Restart", "alt", guest), ("btn-quit", guest ? "Leave" : "Kitchens", "ghost", false));
            if (b == "btn-restart") { if (D != null) { engineStop(); startDrive(D.k); } else if (G.tut != null) startTutorial(); else play(G.lvIdx); }
            if (b == "btn-quit") toMenu();
            if (Button("btn-pause-settings", "Settings", "ghost small")) openSettings("scr-pause");
            cy -= GAP;
        }

        void HowtoScreen()
        {
            Mono("Kitchen rules");
            H2("How to play");
            var rows = new[] {
                ("Stick", INK2, "#fff", "Drag anywhere on the left half of the screen to walk. You act on the counter you're facing."),
                ("Grab", MUSTARD, TEXT, "Pick up and put down. Take from crates, drop on counters, add food to plates and pots, serve at the bell."),
                ("Chop", TOMATO, "#fff", "Chop raw food on a board, or wash dirty plates at the sink. Your chef keeps going until they're done or you walk away. While holding the extinguisher, hold this button to spray."),
                ("Swap", BASIL, "#fff", "Switch chefs. Start one chopping, then swap and cook with the other."),
            };
            foreach (var (key, bg, fg, text) in rows)
            {
                var lines = Wrap(text, "600 16px Nunito", cw - 68);
                double h = Math.Max(28, lines.Count * 22.4);
                Custom(h, r =>
                {
                    RoundRect(r.x, r.y, 58, 28, 14, bg);
                    X.font = "400 15px \"Pixelify Sans\""; X.fillStyle = fg; X.textAlign = "center"; X.textBaseline = "middle"; X.fillText(key, r.x + 29, r.y + 14);
                    X.font = "600 16px Nunito"; X.fillStyle = TEXT; X.textAlign = "left";
                    for (int i = 0; i < lines.Count; i++) X.fillText(lines[i], r.x + 68, r.y + 22.4 * (i + 0.5));
                }, false);
                cy += 10;
            }
            cy += GAP - 10;
            Para("Every order has a timer, and faster service earns bigger tips. If a ticket runs out, you lose coins. Food left on the heat burns and starts a fire that spreads along the counters, so grab the extinguisher. Put anything in the bin to empty it.", "tip");
            Para("Keyboard: WASD or arrow keys to move, Space to grab, E to chop, wash or spray (hold), Q to swap, Esc to pause.", "small");
            if (Button("btn-howto-close", "Got it")) show(G != null ? "scr-pause" : "scr-title");
            cy -= GAP;
        }

        /* ---------------- settings ---------------- */
        void openSettings(string back) { settingsBack = back; show("scr-settings"); }
        void SettingsScreen()
        {
            Mono("Settings");
            H2("Settings");
            var rows = new (string id, string label, string sub, Func<bool> get, Action<bool> set)[]
            {
                ("btn-mute", "Sound effects", null, () => !save.muted, v => save.muted = !v),
                ("set-music", "Music", null, () => save.music, v => save.music = v),
                ("set-buzz", "Vibration", null, () => save.buzz, v => { save.buzz = v; if (v) buzz(40); }),
                ("set-shake", "Screen shake", null, () => save.shake, v => save.shake = v),
                ("set-lefty", "Left-handed controls", "Stick on the right, buttons on the left", () => save.lefty, v => { save.lefty = v; layout(); }),
            };
            foreach (var row in rows)
            {
                double h = row.sub != null ? 58 : 44;
                Custom(h, r =>
                {
                    RoundRect(r.x, r.y, r.w, r.h, 8, PAPER2);
                    X.font = F_BOLD; X.fillStyle = TEXT; X.textAlign = "left"; X.textBaseline = "middle";
                    X.fillText(row.label, r.x + 12, r.y + (row.sub != null ? 18 : r.h / 2));
                    if (row.sub != null) { X.font = "600 13px Nunito"; X.fillStyle = MUTED; X.fillText(row.sub, r.x + 12, r.y + 40); }
                    bool on = row.get();
                    var br = new Rect(r.x + r.w - 10 - 72, r.y + r.h / 2 - 18, 72, 36);
                    Hit(row.id, br);
                    RateBtn(br, on ? "On" : "Off", on, uiPressed == row.id, BASIL, "#eef8e8");
                    if (Clicked(row.id)) { row.set(!on); persist(); sfx("pick"); }
                }, false);
                cy += 8;
            }
            cy += GAP - 8;
            if (Button("btn-settings-close", "Done")) show(settingsBack);
            cy -= GAP;
        }

        /* ---------------- story ---------------- */
        bool maybeStory(int i)
        {
            var lv = Data.LEVELS[i];
            if (NET.role != null || !firstOfStop(i) || !Data.STORY.ContainsKey(lv.stop) || save.story.ContainsKey(lv.stop.ToString())) return false;
            storyState = new StoryRun { stop = lv.stop, line = 0, then = () => openIntro(i) };
            show("scr-story");
            return true;
        }
        void storyNext(bool skip)
        {
            var st = Data.STORY[storyState.stop];
            if (!skip && storyState.line < st.lines.Count - 1) { storyState.line++; sfx("pick"); return; }
            save.story[storyState.stop.ToString()] = true; persist();
            var then = storyState.then; storyState = null; then();
        }
        void StoryScreen()
        {
            if (storyState == null) return;
            var st = Data.STORY[storyState.stop]; var line = st.lines[storyState.line];
            Mono(storyState.stop == 10 ? "Bonus challenge" : "Stop " + storyState.stop);
            H2(Data.STOPS[storyState.stop].name);
            Custom(U.Round(cw * 3 / 8), r => drawStoryArt(st, r));
            string who = line[0], text = line[1];
            var lines = Wrap(text, "600 16px Nunito", cw - 24 - 68);
            double h = Math.Max(84, 20 + 16 + 4 + lines.Count * 23.2);
            Custom(h, r =>
            {
                RoundRect(r.x, r.y, r.w, r.h, 8, PAPER2);
                var c = Data.CAST[who];
                X.fillStyle = "#f3ecdc"; X.fillRect(r.x + 12, r.y + 10, 56, 56);
                chefIcon(c.hat, c.hc, c.jacket, c.scarf[0], c.scarf[1], r.x + 12, r.y + 10, 56);
                X.font = F_MONO; X.fillStyle = MUTED; X.textBaseline = "middle"; SpacedText(who.ToUpperInvariant(), r.x + 80, r.y + 18, 0.96);
                X.font = "600 16px Nunito"; X.fillStyle = TEXT; X.textAlign = "left";
                for (int i = 0; i < lines.Count; i++) X.fillText(lines[i], r.x + 80, r.y + 32 + 23.2 * (i + 0.5));
            });
            var b = BtnRow(("btn-story-skip", "Skip", "ghost", false), ("btn-story-next", storyState.line == st.lines.Count - 1 ? "Let’s go" : "Next", "btn", false));
            if (b == "btn-story-skip") storyNext(true);
            else if (b == "btn-story-next") storyNext(false);
            cy -= GAP;
        }
        void drawStoryArt(StoryScene st, Rect r)
        {
            double k = r.w / 320;
            X.save(); X.translate(r.x, r.y); X.scale(k, k);
            // Sky in bands, sun, ground and road.
            for (int i = 0; i < 8; i++) { X.fillStyle = Css.Mix(st.sky[0], st.sky[1], i / 7.0); X.fillRect(0, i * 10, 320, 10); }
            X.fillStyle = "#fff1a8"; X.fillRect(250, 18, 24, 24); X.fillStyle = "#f5d33e"; X.fillRect(254, 22, 16, 16);
            X.fillStyle = Css.Mix(st.sky[1], "#6a8a4a", 0.6); X.fillRect(0, 76, 320, 10);
            X.fillStyle = "#3a3d48"; X.fillRect(0, 86, 320, 34);
            X.fillStyle = "#f5d33e"; for (int x = 6; x < 320; x += 36) X.fillRect(x, 102, 18, 3);
            spr("truck", 110, 72, 3);
            X.restore();
        }

        /* ---------------- shop ---------------- */
        bool vanShopOpen() => Enumerable.Range(0, Data.DRIVE_RUNS.Count).Any(driveOpen);
        public void openShop(string tab) { if (tab != null) shopTab = tab; show("scr-shop"); }
        void ShopScreen()
        {
            if (shopTab == "van" && !vanShopOpen()) shopTab = "gear";
            Mono("Shop", fmtCoins(save.wallet) + " coins");
            H2("Shop");
            var t = RateRow(("shop-t-gear", "Chef gear", shopTab == "gear", false), ("shop-t-kitchen", "Kitchen", shopTab == "kitchen", false), ("shop-t-van", "Delivery van", shopTab == "van", !vanShopOpen()));
            if (t != null) { shopTab = t.Substring(7); shopConfirm = null; }
            string note = shopTab == "gear" ? "Gear is personal: each chef wears one item per slot (pick in Wear gear), and bonuses from different slots add up."
                : shopTab == "kitchen" ? "Kitchen upgrades are always on once bought (not in versus or the daily quest). Paired upgrades are a choice: you can only own one of the two."
                : "Van upgrades help on the secret Delivery Runs. Engine or tyres: you can only own one.";
            note += shopTab == "gear" ? " Gear is yours for good: no refunds." : " A paired upgrade can be sold back for 25% of its price if you want to switch.";
            Para(note, "small");
            if (shopTab == "gear")
            {
                foreach (var slot in Data.GEAR_SLOTS)
                {
                    Mono(slot[1] + "s", null, false); cy += 8;
                    foreach (var g in Data.GEAR.Where(q => q.slot == slot[0])) ShopRow(g.id, g.name, g.text, g.price, null, g, null);
                }
            }
            else if (shopTab == "kitchen")
            {
                foreach (var eq in Data.EQUIP)
                {
                    if (!equipOpen(eq.chars)) continue;
                    Mono(eq.label, null, false); cy += 8;
                    foreach (var id in eq.ids) { var u = Data.UPGRADES.First(q => q.id == id); ShopRow(u.id, u.name, u.text, u.price, u, null, u.icon); }
                }
                Para("More upgrades appear as you unlock kitchens with new equipment.", "small");
            }
            else foreach (var u in Data.UPGRADES.Where(q => q.group == shopTab)) ShopRow(u.id, u.name, u.text, u.price, u, null, u.icon);
            var b = BtnRow(("btn-shop-wear", "Wear gear", "ghost", false), ("btn-shop-close", "Done", "btn", false));
            if (b == "btn-shop-wear") { openWardrobe(); wardBack = "scr-shop"; }
            if (b == "btn-shop-close") show("scr-title");
            cy -= GAP;
        }
        void ShopRow(string id, string name, string text, double price, Upgrade up, GearItem gear, Icon icon)
        {
            bool own = owns(id);
            var rival = up != null && !own ? forkTaken(up) : null;
            double shortBy = price - save.wallet;
            bool confirming = shopConfirm == id && !own && rival == null && shortBy <= 0;
            bool selling = own && up != null && up.fork != null;
            string btn = selling ? (shopConfirm == "sell:" + id ? "Confirm" : "Sell " + fmtCoins(sellPrice(up))) : own ? "Owned" : rival != null ? "Locked" : confirming ? "Confirm" : shortBy > 0 ? fmtCoins(price) : "Buy " + fmtCoins(price);
            bool disabled = selling ? false : own || rival != null || shortBy > 0;
            string extra = null; bool extraRed = false;
            if (selling) { var other = Data.UPGRADES.First(u => u.fork == up.fork && u.id != up.id); extra = shopConfirm == "sell:" + id ? $"Tap Confirm to sell for {fmtCoins(sellPrice(up))} coins. {other.name} opens up again." : $"Sell back for 25% to switch to {other.name}."; extraRed = true; }
            else if (rival != null) extra = $"You chose {rival.name}. Sell it back to switch.";
            else if (up != null && up.fork != null && !own) { var other = Data.UPGRADES.First(u => u.fork == up.fork && u.id != up.id); extra = confirming ? $"Tap Confirm to buy. {other.name} will be locked unless you sell this back (25% refund)." : $"Or {other.name}: pick one."; extraRed = true; }
            X.font = "400 16px \"Pixelify Sans\""; double bw = X.measureText(btn).width + 22;
            double tw = cw - 20 - 44 - 10 - 10 - bw;
            var nameL = Wrap(name, "700 16px \"Pixelify Sans\"", tw); var textL = Wrap(text, "600 13px Nunito", tw); var extraL = extra != null ? Wrap(extra, "600 13px Nunito", tw) : new List<string>();
            double h = Math.Max(44, nameL.Count * 17.6 + (textL.Count + extraL.Count) * 16.25) + 20;
            string bid = "buy-" + id;
            Custom(h, r =>
            {
                double a = !own && rival != null ? 0.45 : 1;
                X.globalAlpha = a;
                RoundRect(r.x + 1, r.y + 1, r.w - 2, r.h - 2, 10, "#ffffff", own ? BASIL : LINE, 2);
                X.fillStyle = "#f3ecdc"; X.fillRect(r.x + 10, r.y + r.h / 2 - 22, 44, 44);
                if (gear != null) iconAt("gear" + gear.slot, S("1", gear.col, "2", Css.Mix(gear.col, "#000000", 0.35)), r.x + 12, r.y + r.h / 2 - 20, 40);
                else if (icon != null) iconAt(icon.name, icon.sw, r.x + 12, r.y + r.h / 2 - 20, 40);
                double tx = r.x + 10 + 44 + 10, ty = r.y + 10;
                X.textAlign = "left"; X.textBaseline = "middle";
                X.font = "700 16px \"Pixelify Sans\""; X.fillStyle = TEXT;
                foreach (var l in nameL) { X.fillText(l, tx, ty + 8.8); ty += 17.6; }
                X.font = "600 13px Nunito"; X.fillStyle = MUTED;
                foreach (var l in textL) { X.fillText(l, tx, ty + 8); ty += 16.25; }
                X.fillStyle = extraRed ? "#a8301d" : MUTED;
                foreach (var l in extraL) { X.fillText(l, tx, ty + 8); ty += 16.25; }
                var br = new Rect(r.x + r.w - 10 - bw, r.y + r.h / 2 - 17, bw, 34);
                if (!disabled) Hit(bid, br);
                bool down = uiPressed == bid;
                string face = disabled ? PAPER2 : shopConfirm == id || shopConfirm == "sell:" + id ? TOMATO : selling ? "#ffffff" : MUSTARD;
                string sh = disabled ? null : shopConfirm == id || shopConfirm == "sell:" + id ? TOMATO_DK : selling ? LINE : MUSTARD_DK;
                if (sh != null && !down) RoundRect(br.x, br.y + 3, br.w, br.h, 8, sh);
                RoundRect(br.x, br.y + (down ? 2 : 0), br.w, br.h, 8, face, selling && shopConfirm != "sell:" + id ? LINE : null, 2);
                X.font = "400 16px \"Pixelify Sans\""; X.fillStyle = disabled ? MUTED : face == TOMATO ? "#fff" : TEXT; X.textAlign = "center";
                X.fillText(btn, br.x + br.w / 2, br.y + br.h / 2 + (down ? 2 : 0));
                X.globalAlpha = 1;
                if (Clicked(bid)) { if (selling) sellBack(up); else buy(id, price, up, gear); }
            }, false);
            cy += 8;
        }
        void sellBack(Upgrade it)
        {
            if (!owns(it.id) || it.fork == null) return;
            if (shopConfirm != "sell:" + it.id) { shopConfirm = "sell:" + it.id; sfx("warn"); return; }
            shopConfirm = null;
            save.owned.Remove(it.id);
            save.wallet += sellPrice(it);
            persist(); sfx("pick"); buzz(25);
        }
        void buy(string id, double price, Upgrade up, GearItem gear)
        {
            if (owns(id) || save.wallet < price || (up != null && forkTaken(up) != null)) return;
            // Forks ask twice: the second tap confirms.
            if (up != null && up.fork != null && shopConfirm != id) { shopConfirm = id; sfx("warn"); return; }
            shopConfirm = null;
            save.wallet -= price;
            save.owned[id] = true;
            // New gear goes straight on chef 1 if that slot is empty.
            if (gear != null)
            {
                int si = Data.GEAR_SLOTS.FindIndex(q => q[0] == gear.slot);
                var lo = save.Gear(0);
                while (lo.Count <= si) lo.Add(null);
                if (lo[si] == null) lo[si] = id;
            }
            persist(); sfx("serve"); buzz(25);
            netFitChanged();
        }

        /* ---------------- wardrobe ---------------- */
        void openWardrobe() { wardSlot = 0; wardBack = "scr-title"; show("scr-wardrobe"); }
        void WardrobeScreen()
        {
            Mono("Wardrobe");
            H2("Chef outfits");
            int total = totalStars();
            Para($"You have {total} star{(total == 1 ? "" : "s")}. New outfits unlock as you collect more.", "small");
            var t = RateRow(("ward-c0", NET.role != null ? "Your chef" : "Chef 1", wardSlot == 0, false), ("ward-c1", "Chef 2", wardSlot == 1, NET.role != null));
            if (t == "ward-c0") wardSlot = 0; if (t == "ward-c1") wardSlot = 1;
            var list = Data.OUTFITS.Where(o => outfitUnlocked(o) || !(o.judge && hiddenBonus(Data.LEVELS.FindIndex(l => l.judge)))).ToList();
            Grid(list.Count, 96, 8, (i, w) => 8 + 48 + 4 + Wrap(list[i].name, "700 15px \"Pixelify Sans\"", w - 12).Count * 16.5 + 18 + 8, (i, r) =>
            {
                var o = list[i]; bool ok = outfitUnlocked(o); string id = "fit-" + o.id;
                bool on = save.outfits[wardSlot] == o.id;
                if (ok) Hit(id, r);
                X.globalAlpha = ok ? 1 : 0.8;
                RoundRect(r.x + 1, r.y + 1, r.w - 2, r.h - 2, 10, on ? "#f3ecff" : "#ffffff", on ? "#8a5ad4" : LINE, on ? 3 : 2);
                X.fillStyle = "#f3ecdc"; X.fillRect(r.x + r.w / 2 - 24, r.y + 8, 48, 48);
                X.globalAlpha = ok ? 1 : 0.35;
                chefIcon(o.hat, o.hc, o.jacket, Data.CHEF_COLORS[wardSlot], Data.CHEF_DARK[wardSlot], r.x + r.w / 2 - 24, r.y + 8, 48);
                X.globalAlpha = ok ? 1 : 0.8;
                var ln = Wrap(o.name, "700 15px \"Pixelify Sans\"", r.w - 12);
                X.font = "700 15px \"Pixelify Sans\""; X.fillStyle = TEXT; X.textAlign = "center"; X.textBaseline = "middle";
                for (int k = 0; k < ln.Count; k++) X.fillText(ln[k], r.x + r.w / 2, r.y + 60 + 16.5 * (k + 0.5));
                X.font = "600 12px Nunito"; X.fillStyle = MUTED;
                X.fillText(ok ? (on ? "Wearing" : "Tap to wear") : o.judge ? "Pass the Judge's Table" : "★ " + o.need, r.x + r.w / 2, r.y + 60 + ln.Count * 16.5 + 9);
                X.globalAlpha = 1;
                if (Clicked(id)) { save.outfits[wardSlot] = o.id; persist(); sfx("pick"); if (wardSlot == 0) netFitChanged(); }
            });
            Mono("Gear");
            bool any = Data.GEAR.Any(g => owns(g.id));
            Para(any ? $"{(NET.role != null ? "Your chef" : "Chef " + (wardSlot + 1))}: {statsText(gearStats(myLoadout(wardSlot)))}." : "Buy hats, aprons, gloves and shoes in the Shop, then pick one per slot here.", "small");
            var lo = save.Gear(wardSlot);
            for (int si = 0; si < Data.GEAR_SLOTS.Count; si++)
            {
                var slot = Data.GEAR_SLOTS[si];
                var mine = Data.GEAR.Where(g => g.slot == slot[0] && owns(g.id)).ToList();
                if (mine.Count == 0) continue;
                var chips = new List<GearItem> { null }.Concat(mine).ToList();
                // Chips flow left to right and wrap.
                double x = 58 + 6, rowY = 0; var pos = new List<(double x, double y, double w)>();
                foreach (var g in chips)
                {
                    X.font = "600 13px Nunito"; double w = X.measureText(g != null ? g.name : "None").width + 24;
                    if (x + w > cw && x > 64) { x = 64; rowY += 34; }
                    pos.Add((x, rowY, w)); x += w + 6;
                }
                int s2 = si;
                Custom(rowY + 30, r =>
                {
                    X.font = "800 13px Nunito"; X.fillStyle = TEXT; X.textAlign = "left"; X.textBaseline = "middle";
                    X.fillText(slot[1], r.x, r.y + 15);
                    for (int k = 0; k < chips.Count; k++)
                    {
                        var g = chips[k]; var p = pos[k]; string id = "gear-" + s2 + "-" + (g?.id ?? "none");
                        var cr = new Rect(r.x + p.x, r.y + p.y, p.w, 30); Hit(id, cr);
                        bool on = (s2 < lo.Count ? lo[s2] : null) == g?.id;
                        RoundRect(cr.x + 1, cr.y + 1, cr.w - 2, cr.h - 2, 15, on ? "#eef8e8" : "#ffffff", on ? BASIL : LINE, 2);
                        X.font = "600 13px Nunito"; X.fillStyle = TEXT; X.textAlign = "center"; X.fillText(g != null ? g.name : "None", cr.x + cr.w / 2, cr.y + 15);
                        if (Clicked(id)) { while (lo.Count <= s2) lo.Add(null); lo[s2] = g?.id; persist(); sfx("pick"); if (wardSlot == 0) netFitChanged(); }
                    }
                }, false);
                cy += 6;
            }
            cy += GAP - 6;
            if (Button("btn-ward-close", "Done")) { var back = wardBack; wardBack = "scr-title"; show(back); }
            cy -= GAP;
        }

        /* ---------------- results ---------------- */
        public void showResults()
        {
            int i = G.lvIdx; var lv = G.lv;
            int stars = G.kicked ? 0 : starsFor(i, G.score, G.humans);
            double need = starTargets(i, G.humans)[0];
            var wasHidden = Enumerable.Range(0, Data.LEVELS.Count).Select(hiddenBonus).ToList();
            if (stars > 0 && !lv.daily && i >= 0) { if (stars > save.stars[i]) { save.stars[i] = stars; persist(); } }
            var pay = lv.daily ? settleQuest() : settle(G.attempt, G.score, stars, starTargets(i, G.humans)[2]);
            var m = new ResultsModel { wallet = pay.text };
            if (!lv.daily && !G.kicked && G.humans == 1 && i >= 0 && (!save.best.Has(i) || G.score > save.best[i])) { save.best[i] = G.score; persist(); }
            m.no = "Kitchen " + U.Pad2(i + 1);
            m.name = lv.name; m.stars = stars;
            m.served = G.served.ToString(); m.failed = G.failed + (G.missLoss > 0 ? $" (−{U.S(G.missLoss)})" : "");
            m.tips = U.S(G.tips); m.score = U.S(G.score);
            bool nextOk = !lv.daily && i >= 0 && i + 1 < Data.LEVELS.Count && unlocked(i + 1);
            bool guest = NET.role == "guest";
            m.nextHidden = !nextOk || guest; m.retryHidden = guest;
            m.menuLabel = guest ? "Leave" : NET.role != null ? "Pick another kitchen" : "All kitchens";
            var next = i >= 0 && i + 1 < Data.LEVELS.Count ? Data.LEVELS[i + 1] : null;
            if (next == null || (hiddenBonus(i + 1) && !lv.judge)) m.next = stars > 0 || lv.finale ? (lv.finale ? "You made it to Thanksgiving. The road trip is complete! Go back for three stars everywhere." : "You cleared every kitchen on the road so far. Go for three stars.") : "Earn " + U.S(need) + " coins for your first star.";
            else if (nextOk) m.next = "";
            else if (next.stop != lv.stop) m.next = $"Collect {Data.STOPS[next.stop].needStars} stars in {Data.STOPS[lv.stop].name} to drive on to {Data.STOPS[next.stop].name}. You have {stopStars(lv.stop)}.";
            else m.next = "Earn " + U.S(need) + " coins to unlock " + next.name + ".";
            if (lv.finale && stars > 0 && nextOk) m.next = "You made it to Thanksgiving. The road trip is complete! A bonus challenge has opened: The Judge's Table.";
            if (lv.judge) m.next = G.kicked ? $"Yourdone Ramsey threw you out after {MAX_STRIKES} strikes. \"{(G.judge != null ? G.judge.text : "")}\"" : stars > 0 ? "Yourdone Ramsey grunts, Richard Braise nods and Nyesha Garnishton smiles. You passed the Judge's Table!" : $"Not enough. The judges want {U.S(need)} coins with no more than two strikes.";
            // Newly opened bonus kitchens are announced here, the first time only.
            var opened = Enumerable.Range(0, Data.LEVELS.Count).Where(k => wasHidden[k] && !hiddenBonus(k) && !Data.STOPS[Data.LEVELS[k].stop].bonus).ToList();
            if (opened.Count > 0 && !lv.daily && NET.role == null) m.next = $"Bonus kitchen unlocked: {string.Join(" and ", opened.Select(k => Data.LEVELS[k].name))}! Find it under Roadside Specials. " + m.next;
            if (lv.daily)
            {
                if (save.daily == null || save.daily.key != DAILY.key) save.daily = new DailySave { key = DAILY.key };
                var d = save.daily;
                bool isBest = G.score > d.best;
                d.plays++; if (isBest) d.best = G.score;
                persist();
                m.no = "Daily challenge · " + dailyDate();
                m.next = (isBest && G.score > 0 ? "New best for today! " : $"Today’s best: {U.S(d.best)}. ") + "A new kitchen and twist arrive tomorrow.";
            }
            if (guest) m.next = "The host is picking what to play next.";
            if (G.versus) m.next = vsSummary();
            var found = NET.role != null ? new List<int>() : newDriveRuns();
            if (found.Count > 0) m.next = $"Secret unlocked: Delivery Run “{Data.DRIVE_RUNS[found[0]].name}”! Drive the orders yourself. Find it at the bottom of the kitchen list. " + m.next;
            int li = i;
            m.onRetry = () => play(li);
            m.onNext = () => { if (!maybeStory(li + 1)) openIntro(li + 1); };
            results = m;
            show("scr-results");
        }

        void ResultsScreen()
        {
            var m = results;
            if (G != null && G.versus && G.shown) m.next = vsSummary();
            Mono(m.no, m.over);
            H2(m.name);
            double ss = H < 520 ? 34 : 56;
            Custom(ss, r => StarsAt(r.x + r.w / 2, r.y + r.h / 2, ss, m.stars));
            Rule();
            var rows = new[] { (m.l1, m.served, false), (m.l2, m.failed, false), ("Tips", m.tips, false), ("Total", m.score, true) };
            foreach (var (l, v, total) in rows)
            {
                double h = total ? 30 : 21;
                Custom(h, r =>
                {
                    X.font = "500 15px \"DM Mono\""; X.fillStyle = TEXT; X.textAlign = "left"; X.textBaseline = "middle";
                    X.fillText(l, r.x, r.y + (total ? 12 : r.h / 2));
                    X.textAlign = "right";
                    if (total) { X.font = "400 26px \"Pixelify Sans\""; X.fillStyle = TOMATO; }
                    X.fillText(v, r.x + r.w, r.y + r.h / 2);
                }, false);
                cy += 6;
            }
            cy += GAP - 6;
            if (!string.IsNullOrEmpty(m.wallet)) Para(m.wallet, "wallet");
            if (!string.IsNullOrEmpty(m.next)) Para(m.next, "small");
            var b = BtnRow(("btn-retry", "Retry", "alt", m.retryHidden), ("btn-next", "Next kitchen", "btn", m.nextHidden));
            if (b == "btn-retry") m.onRetry?.Invoke();
            if (b == "btn-next") m.onNext?.Invoke();
            if (Button("btn-res-menu", m.menuLabel, "ghost")) toMenu();
            cy -= GAP;
        }

        /* ---------------- online ---------------- */
        void OnlineScreen()
        {
            Mono("Up to four phones, one kitchen");
            H2("Play with a friend");
            Para("One of you hosts and gets a room code. Up to three friends join with that code. You each run one chef, and the host picks the kitchens.");
            if (Button("btn-host", "Host a kitchen", "btn", null, !onlineButtonsEnabled)) hostGame();
            Rule();
            Mono("Join with a code");
            double fh = 50;
            X.font = "400 20px \"Pixelify Sans\""; double jw = X.measureText("Join").width + 36;
            Custom(fh, r =>
            {
                var fr = new Rect(r.x, r.y, r.w - jw - 10, fh);
                Hit("join-code", fr);
                bool focus = focusField == "join-code";
                RoundRect(fr.x, fr.y, fr.w, fr.h, 12, "#ffffff", focus ? MUSTARD : LINE, focus ? 3 : 2);
                X.font = "700 26px \"Pixelify Sans\""; X.textAlign = "center"; X.textBaseline = "middle";
                string shown = typed.Length > 0 ? string.Join(" ", typed.ToCharArray()) : "A B C D";
                X.fillStyle = typed.Length > 0 ? TEXT : LINE;
                X.fillText(shown + (focus && Math.Floor(T * 2) % 2 == 0 ? "_" : ""), fr.x + fr.w / 2, fr.y + fr.h / 2);
                if (Clicked("join-code") && onlineButtonsEnabled) { focusField = "join-code"; platform.OpenKeyboard(typed, 4); }
                var jr = new Rect(r.x + r.w - jw, r.y, jw, fh);
                if (onlineButtonsEnabled) Hit("btn-join", jr);
                BtnVisual(jr, "alt", "Join", null, uiPressed == "btn-join", !onlineButtonsEnabled);
                if (Clicked("btn-join")) joinGame();
            });
            if (!string.IsNullOrEmpty(onlineCode))
                Custom(92, r =>
                {
                    RoundRect(r.x, r.y, r.w, r.h, 8, PAPER2);
                    X.font = F_MONO; X.fillStyle = MUTED; X.textAlign = "center"; X.textBaseline = "middle";
                    X.fillText("ROOM CODE", r.x + r.w / 2, r.y + 18);
                    X.font = "700 52px \"Pixelify Sans\""; X.fillStyle = TOMATO;
                    X.fillText(string.Join(" ", onlineCode.ToCharArray()), r.x + r.w / 2, r.y + 58);
                });
            Para(onlineStatus, "tip");
            if (Button("btn-online-back", "Back", "ghost")) { netLeave(); show("scr-title"); }
            cy -= GAP;
        }
        public void netStatus(string text, string code = null) { onlineStatus = text; onlineCode = code ?? ""; }
        public void setOnlineButtons(bool enabled) { onlineButtonsEnabled = enabled; }

        // ---- music: which tune should be playing ----
        string wantedTune()
        {
            if (paused) return null;
            if (D != null) return D.phase == "over" ? null : "drive";
            if (G != null) return G.phase == "over" ? null : G.lv.tutorial ? "0" : G.lv.stop.ToString();
            return screen == "scr-title" || screen == "scr-intro" || screen == "scr-story" || screen == "scr-wardrobe" ? "menu" : null;
        }
        string musicKey; bool musicRush;
        void musicTick()
        {
            string key = save.muted || !save.music ? null : wantedTune();
            bool rush = (G != null && G.phase == "play" && !G.lv.tutorial && G.time <= 30) || (D != null && D.phase == "play" && driveTimeLeft() <= 10);
            if (key != musicKey || rush != musicRush) { musicKey = key; musicRush = rush; platform.SetMusic(key, rush); }
        }
    }
}
