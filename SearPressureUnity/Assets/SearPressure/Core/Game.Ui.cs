using System;
using System.Collections.Generic;
using System.Linq;

namespace SearPressure
{
    // Immediate-mode menus drawn with the same canvas as the game. Each screen is a "ticket" card
    // (the web version's HTML screens), laid out top to bottom. Every frame the card is laid out
    // twice: once to measure it, once to draw it centred (and scrolled when it's taller than the screen).
    public sealed partial class Game
    {
        // ---- colours from the web version's CSS ----
        const string INK = "#1c2133", INK2 = "#2a3150", PAPER = "#fff5de", PAPER2 = "#f1e2bd", LINE = "#d9c79c", TEXT = "#2b2230", MUTED = "#7d6c57";
        const string TOMATO = "#e8472f", TOMATO_DK = "#a8301d", MUSTARD = "#f5b82e", MUSTARD_DK = "#b9821a", BASIL = "#3f9b45";
        const string F_MONO = "500 12px \"DM Mono\"", F_BODY = "600 16px Nunito", F_BOLD = "800 16px Nunito";

        public string screen;                 // which card is showing (null: none)
        public bool tutHidden = true;         // the tutorial card over the kitchen
        readonly Dictionary<string, double> scrollOf = new Dictionary<string, double>();

        // Pointer state for the menus.
        int? uiPtr; double uiDownX, uiDownY, uiLastY; bool uiDragging; string uiPressed, uiClick;
        double uiVel;
        readonly List<(string id, Rect r)> uiHits = new List<(string, Rect)>();
        readonly List<(string id, Rect r)> uiHitsNext = new List<(string, Rect)>();
        bool uiDraw;               // false while measuring
        double cx0, cw, cy;        // current content column and cursor
        string focusField; public string typed = "";
        Rect tutCardRect; double tutCardH;

        public void show(string id)
        {
            screen = id;
            if (id != null) scrollOf[id] = 0;
            uiPressed = null; uiClick = null;
        }
        bool Clicked(string id) { if (uiClick == id && uiDraw) { uiClick = null; return true; } return false; }

        // ---- input ----
        bool UiCapturing() => screen != null;
        bool UiPointerDown(int id, double x, double y)
        {
            bool onTut = G != null && G.tut != null && !tutHidden && screen == null && tutCardRect != null && x >= tutCardRect.x && x <= tutCardRect.x + tutCardRect.w && y >= tutCardRect.y && y <= tutCardRect.y + tutCardRect.h;
            if (!UiCapturing() && !onTut) return false;
            if (uiPtr != null) return true;
            uiPtr = id; uiDownX = x; uiDownY = y; uiLastY = y; uiDragging = false; uiVel = 0;
            uiPressed = HitAt(x, y);
            if (uiPressed == null) focusField = null;
            return true;
        }
        void UiPointerMove(int id, double x, double y)
        {
            if (uiPtr != id) return;
            if (!uiDragging && Math.Abs(y - uiDownY) > 8 && screen != null) { uiDragging = true; uiPressed = null; }
            if (uiDragging && screen != null)
            {
                double dy = y - uiLastY;
                scrollOf[screen] = (scrollOf.TryGetValue(screen, out var s) ? s : 0) - dy;
                uiVel = -dy;
            }
            uiLastY = y;
            if (!uiDragging && uiPressed != null && HitAt(x, y) != uiPressed) uiPressed = null;
        }
        void UiPointerUp(int id, double x, double y)
        {
            if (uiPtr != id) return;
            uiPtr = null;
            if (!uiDragging && uiPressed != null && HitAt(x, y) == uiPressed) { uiClick = uiPressed; }
            uiPressed = null; uiDragging = false;
        }
        public void Scroll(double dy) { if (screen != null) scrollOf[screen] = (scrollOf.TryGetValue(screen, out var s) ? s : 0) + dy; }
        string HitAt(double x, double y)
        {
            for (int i = uiHits.Count - 1; i >= 0; i--) { var r = uiHits[i].r; if (x >= r.x && x <= r.x + r.w && y >= r.y && y <= r.y + r.h) return uiHits[i].id; }
            return null;
        }
        bool UiKeyDown(string code)
        {
            if (focusField != null)
            {
                if (code == "Backspace") { if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1); return true; }
                if (code == "Enter") { focusField = null; if (screen == "scr-online") joinGame(); return true; }
                if (code == "Escape") { focusField = null; return true; }
                return true;
            }
            if (screen == null) return false;
            if (code == "Escape")
            {
                if (screen == "scr-pause") resumeGame();
                else if (screen == "scr-howto") show(G != null ? "scr-pause" : "scr-title");
                else if (screen == "scr-settings") show(settingsBack);
                else if (screen == "scr-intro" || screen == "scr-shop" || screen == "scr-wardrobe") show("scr-title");
                return true;
            }
            if (code == "ArrowDown") { Scroll(40); return true; }
            if (code == "ArrowUp") { Scroll(-40); return true; }
            return screen != null && code != "KeyP";
        }
        // Text typed on a device keyboard (Unity passes it in).
        public void TextInput(string s)
        {
            if (focusField == null) return;
            foreach (char ch in s.ToUpperInvariant()) if (ch >= 'A' && ch <= 'Z' && typed.Length < 4) typed += ch;
        }

        // ---- the frame ----
        void UiFrame(double dt)
        {
            X.setTransform(DPR, 0, 0, DPR, 0, 0);
            uiHitsNext.Clear();
            if (G != null && G.tut != null && !tutHidden && screen == null) DrawTutorialCard();
            else tutCardRect = null;
            if (screen != null) DrawScreen(screen, dt);
            uiHits.Clear(); uiHits.AddRange(uiHitsNext);
            // Inertia after a flick.
            if (screen != null && uiPtr == null && Math.Abs(uiVel) > 0.5) { scrollOf[screen] += uiVel; uiVel *= 0.9; }
        }

        void DrawScreen(string id, double dt)
        {
            bool dim = id != "scr-title";
            X.fillStyle = dim ? "rgba(16,19,32,.8)" : "rgba(16,19,32,.5)"; X.fillRect(0, 0, W, H);
            double maxW = Math.Min(400, W - Math.Max(16, safeL) - Math.Max(16, safeR));
            double padX = 20, padTop = H < 520 ? 14 : 22, padBot = H < 520 ? 14 : 20;
            cw = maxW - padX * 2;
            // Measure.
            uiDraw = false; cy = 0; cx0 = 0;
            BuildScreen(id);
            double contentH = cy;
            double cardH = contentH + padTop + padBot;
            double top = Math.Max(24, safeT), bot = Math.Max(24, safeB);
            double avail = H - top - bot;
            double scroll = scrollOf.TryGetValue(id, out var s0) ? s0 : 0;
            double maxScroll = Math.Max(0, cardH - avail);
            if (uiPtr == null) scroll = Math.Max(0, Math.Min(maxScroll, scroll));
            else scroll = Math.Max(-60, Math.Min(maxScroll + 60, scroll));
            scrollOf[id] = scroll;
            double cardY = (cardH < avail ? top + (avail - cardH) / 2 : top) - scroll;
            double cardX = U.Round(W / 2 - maxW / 2);
            // Card with the ticket's zigzag edges.
            X.fillStyle = "rgba(0,0,0,.35)"; X.fillRect(cardX + 4, cardY + 10, maxW, cardH);
            X.fillStyle = PAPER; X.fillRect(cardX, cardY, maxW, cardH);
            Zigzag(cardX, cardY, maxW, -1); Zigzag(cardX, cardY + cardH, maxW, 1);
            // Draw.
            uiDraw = true; cy = cardY + padTop; cx0 = cardX + padX;
            BuildScreen(id);
        }
        void Zigzag(double x, double y, double w, int dir)
        {
            X.fillStyle = PAPER;
            X.beginPath();
            for (double i = 0; i < w - 0.1; i += 16)
            {
                double seg = Math.Min(16, w - i);
                X.moveTo(x + i, y); X.lineTo(x + i + seg / 2, y + dir * 8); X.lineTo(x + i + seg, y); X.closePath();
            }
            X.fill();
        }

        // ---- layout pieces ----
        const double GAP = 14;
        void Gap(double g = GAP) { cy += g; }

        public List<string> Wrap(string text, string font, double maxW)
        {
            X.font = font;
            var outL = new List<string>();
            foreach (var para in (text ?? "").Split('\n'))
            {
                var words = para.Split(' ');
                string line = "";
                foreach (var w in words)
                {
                    string t = line.Length > 0 ? line + " " + w : w;
                    if (line.Length > 0 && X.measureText(t).width > maxW) { outL.Add(line); line = w; }
                    else line = t;
                }
                outL.Add(line);
            }
            return outL;
        }
        // Text with extra spacing between letters (CSS letter-spacing), for the mono labels.
        double SpacedWidth(string s, double spacing) { double w = 0; foreach (char ch in s) w += X.measureText(ch.ToString()).width + spacing; return w - (s.Length > 0 ? spacing : 0); }
        void SpacedText(string s, double x, double y, double spacing)
        {
            X.textAlign = "left";
            foreach (char ch in s) { X.fillText(ch.ToString(), x, y); x += X.measureText(ch.ToString()).width + spacing; }
        }

        // An uppercase DM Mono label; `right` puts a second label on the same line.
        void Mono(string text, string right = null, bool gapAfter = true, string color = MUTED)
        {
            double h = 16;
            if (uiDraw)
            {
                X.font = F_MONO; X.fillStyle = color; X.textBaseline = "middle";
                SpacedText(text.ToUpperInvariant(), cx0, cy + h / 2, 12 * 0.08);
                if (right != null) { string r = right.ToUpperInvariant(); SpacedText(r, cx0 + cw - SpacedWidth(r, 12 * 0.08), cy + h / 2, 12 * 0.08); }
            }
            cy += h; if (gapAfter) Gap();
        }
        // Mono label on the left, a small text button on the right (Settings on the title card).
        bool MonoWithLink(string text, string id, string link)
        {
            double h = 18;
            bool hit = false;
            if (uiDraw)
            {
                X.font = F_MONO; X.fillStyle = MUTED; X.textBaseline = "middle";
                SpacedText(text.ToUpperInvariant(), cx0, cy + h / 2, 0.96);
                string l = link.ToUpperInvariant(); double lw = SpacedWidth(l, 0.96);
                var r = new Rect(cx0 + cw - lw - 8, cy - 6, lw + 16, h + 12);
                Hit(id, r);
                X.fillStyle = uiPressed == id ? TEXT : MUTED;
                SpacedText(l, cx0 + cw - lw, cy + h / 2, 0.96);
                hit = Clicked(id);
            }
            cy += h; Gap();
            return hit;
        }
        void Hit(string id, Rect r) { if (uiDraw) uiHitsNext.Add((id, r)); }

        void H2(string text, double size = 32, string color = TEXT)
        {
            if (H < 520) size = Math.Min(size, 26);
            string font = "700 " + U.S(size) + "px \"Pixelify Sans\"";
            var lines = Wrap(text, font, cw);
            if (uiDraw)
            {
                X.font = font; X.fillStyle = color; X.textAlign = "left"; X.textBaseline = "middle";
                for (int i = 0; i < lines.Count; i++) X.fillText(lines[i], cx0, cy + size * 0.5 + i * size);
            }
            cy += lines.Count * size; Gap();
        }

        // Paragraph styles: "p", "small", "tip", "wallet", "bold".
        void Para(string text, string style = "p", string color = null, bool gapAfter = true)
        {
            if (string.IsNullOrEmpty(text)) return;
            double size = style == "small" ? 13 : style == "tip" ? 15 : 16;
            string font = (style == "wallet" || style == "bold" ? "800 " : "600 ") + U.S(size) + "px Nunito";
            double lh = size * 1.45;
            bool box = style == "tip";
            double padX = box ? 12 : 0, padY = box ? 10 : 0;
            var lines = Wrap(text, font, cw - padX * 2);
            double h = lines.Count * lh + padY * 2;
            if (uiDraw)
            {
                if (box) RoundRect(cx0, cy, cw, h, 8, PAPER2);
                X.font = font; X.textAlign = "left"; X.textBaseline = "middle";
                X.fillStyle = color ?? (style == "small" ? MUTED : style == "wallet" ? BASIL : TEXT);
                for (int i = 0; i < lines.Count; i++) X.fillText(lines[i], cx0 + padX, cy + padY + lh * (i + 0.5));
            }
            cy += h; if (gapAfter) Gap();
        }

        void Rule()
        {
            if (uiDraw) { X.fillStyle = LINE; for (double x = cx0; x < cx0 + cw; x += 9) X.fillRect(x, cy, Math.Min(5, cx0 + cw - x), 2); }
            cy += 2; Gap();
        }

        public void RoundRect(double x, double y, double w, double h, double r, string fill, string stroke = null, double lw = 2)
        {
            X.beginPath();
            r = Math.Min(r, Math.Min(w / 2, h / 2));
            X.moveTo(x + r, y);
            X.arcTo(x + w, y, x + w, y + h, r); X.arcTo(x + w, y + h, x, y + h, r); X.arcTo(x, y + h, x, y, r); X.arcTo(x, y, x + w, y, r);
            X.closePath();
            if (fill != null) { X.fillStyle = fill; X.fill(); }
            if (stroke != null) { X.strokeStyle = stroke; X.lineWidth = lw; X.stroke(); }
        }

        // ---- buttons ----
        // Variants: "btn" (tomato), "alt" (mustard), "ghost" (outlined), plus " small".
        void BtnVisual(Rect r, string variant, string label, string sub, bool pressed, bool disabled)
        {
            bool small = variant.Contains("small");
            string v = variant.Replace("small", "").Trim();
            double fs = small ? 15 : 20;
            double press = pressed ? (v == "ghost" ? 1 : 3) : 0;
            double a = disabled ? 0.45 : 1;
            X.globalAlpha = a;
            string face = v == "alt" ? MUSTARD : v == "ghost" ? null : TOMATO, shadow = v == "alt" ? MUSTARD_DK : TOMATO_DK;
            double ry = r.y + press;
            if (face != null)
            {
                if (!pressed) RoundRect(r.x, r.y + 4, r.w, r.h, 12, shadow);
                else RoundRect(r.x, r.y + 1 + press, r.w, r.h - press, 12, shadow);
                RoundRect(r.x, ry, r.w, r.h, 12, face);
            }
            else RoundRect(r.x + 1, ry + 1, r.w - 2, r.h - 2, 12, null, LINE, 2);
            string col = v == "btn" ? "#fff" : TEXT;
            string font = "400 " + U.S(fs) + "px \"Pixelify Sans\"";
            var lines = Wrap(label, font, r.w - 24);
            X.font = font; X.fillStyle = col; X.textAlign = "center"; X.textBaseline = "middle";
            double lh = fs * 1.0, total = lines.Count * lh + (sub != null ? 17 * Wrap(sub, "600 13px Nunito", r.w - 16).Count : 0);
            double y0 = ry + r.h / 2 - total / 2;
            for (int i = 0; i < lines.Count; i++) X.fillText(lines[i], r.x + r.w / 2, y0 + lh * (i + 0.5));
            if (sub != null)
            {
                var sl = Wrap(sub, "600 13px Nunito", r.w - 16);
                X.font = "600 13px Nunito"; X.fillStyle = MUTED;
                for (int i = 0; i < sl.Count; i++) X.fillText(sl[i], r.x + r.w / 2, y0 + lines.Count * lh + 9 + i * 17);
            }
            X.globalAlpha = 1;
        }
        double BtnHeight(string variant, string label, string sub, double w)
        {
            bool small = variant.Contains("small");
            double fs = small ? 15 : 20;
            var lines = Wrap(label, "400 " + U.S(fs) + "px \"Pixelify Sans\"", w - 24);
            double padY = small ? 8 : 14;
            return lines.Count * fs + padY * 2 + (sub != null ? 17 * Wrap(sub, "600 13px Nunito", w - 16).Count : 0);
        }
        bool Button(string id, string label, string variant = "btn", string sub = null, bool disabled = false, double? width = null, bool alignRight = false)
        {
            double w = width ?? cw;
            double h = BtnHeight(variant, label, sub, w);
            bool clicked = false;
            if (uiDraw)
            {
                double x = alignRight ? cx0 + cw - w : cx0;
                var r = new Rect(x, cy, w, h);
                if (!disabled) Hit(id, r);
                BtnVisual(r, variant, label, sub, uiPressed == id, disabled);
                clicked = !disabled && Clicked(id);
            }
            cy += h + 4; Gap(GAP - 4);
            return clicked;
        }
        // A row of buttons sharing the width (the web version's .btn-row).
        string BtnRow(params (string id, string label, string variant, bool hidden)[] btns)
        {
            var vis = btns.Where(b => !b.hidden).ToList();
            if (vis.Count == 0) return null;
            double gap = 10;
            // Wrap like flex: 1 1 120px.
            int perRow = Math.Max(1, Math.Min(vis.Count, (int)Math.Floor((cw + gap) / (120 + gap))));
            string clicked = null;
            for (int start = 0; start < vis.Count; start += perRow)
            {
                var row = vis.Skip(start).Take(perRow).ToList();
                double bw = (cw - gap * (row.Count - 1)) / row.Count;
                double h = row.Max(b => BtnHeight(b.variant, b.label, null, bw));
                if (uiDraw)
                {
                    for (int i = 0; i < row.Count; i++)
                    {
                        var r = new Rect(cx0 + i * (bw + gap), cy, bw, h);
                        Hit(row[i].id, r);
                        BtnVisual(r, row[i].variant, row[i].label, null, uiPressed == row[i].id, false);
                        if (Clicked(row[i].id)) clicked = row[i].id;
                    }
                }
                cy += h + 4 + (start + perRow < vis.Count ? gap - 4 : 0);
            }
            Gap(GAP - 4);
            return clicked;
        }
        // Toggle-style buttons in a row (.rate-row / tabs): returns the clicked id.
        string RateRow(params (string id, string label, bool pressed, bool hidden)[] btns)
        {
            var vis = btns.Where(b => !b.hidden).ToList();
            double gap = 6, bw = (cw - gap * (vis.Count - 1)) / Math.Max(1, vis.Count);
            string font = "700 14px \"Pixelify Sans\"";
            double h = vis.Count == 0 ? 0 : vis.Max(b => Wrap(b.label, font, bw - 8).Count) * 15.4 + 18 + 4;
            string clicked = null;
            if (uiDraw)
                for (int i = 0; i < vis.Count; i++)
                {
                    var r = new Rect(cx0 + i * (bw + gap), cy, bw, h);
                    Hit(vis[i].id, r);
                    RateBtn(r, vis[i].label, vis[i].pressed, uiPressed == vis[i].id, TOMATO, "#fff0ec");
                    if (Clicked(vis[i].id)) clicked = vis[i].id;
                }
            cy += h; Gap();
            return clicked;
        }
        void RateBtn(Rect r, string label, bool on, bool down, string onBorder, string onFill, string font = "700 14px \"Pixelify Sans\"")
        {
            RoundRect(r.x + 1, r.y + 1 + (down ? 1 : 0), r.w - 2, r.h - 2, 8, on ? onFill : "#ffffff", on ? onBorder : LINE, 2);
            var lines = Wrap(label, font, r.w - 8);
            X.font = font; X.fillStyle = TEXT; X.textAlign = "center"; X.textBaseline = "middle";
            double lh = 15.4, y0 = r.y + r.h / 2 - lines.Count * lh / 2 + (down ? 1 : 0);
            for (int i = 0; i < lines.Count; i++) X.fillText(lines[i], r.x + r.w / 2, y0 + lh * (i + 0.5));
        }

        // A block of custom drawing `h` high.
        void Custom(double h, Action<Rect> draw, bool gapAfter = true)
        {
            if (uiDraw) draw(new Rect(cx0, cy, cw, h));
            cy += h; if (gapAfter) Gap();
        }

        // Cells in a grid of at least `minCell` wide (menu-list, helper-list): `cellH` tall.
        void Grid(int n, double minCell, double gap, Func<int, double, double> cellH, Action<int, Rect> draw)
        {
            if (n == 0) return;
            int cols = Math.Max(1, (int)Math.Floor((cw + gap) / (minCell + gap)));
            double w = (cw - gap * (cols - 1)) / cols;
            for (int start = 0; start < n; start += cols)
            {
                double h = 0;
                for (int i = start; i < Math.Min(n, start + cols); i++) h = Math.Max(h, cellH(i, w));
                if (uiDraw) for (int i = start; i < Math.Min(n, start + cols); i++) draw(i, new Rect(cx0 + (i - start) * (w + gap), cy, w, h));
                cy += h + (start + cols < n ? gap : 0);
            }
            Gap();
        }

        // ---- the tutorial card over the kitchen ----
        double tutReserve() => G != null && G.tut != null && !tutHidden ? tutCardH + 10 : 0;
        void DrawTutorialCard()
        {
            var step = Data.TUT_STEPS[G.tut.i];
            double w = Math.Min(420, W - 24), x = U.Round(W / 2 - w / 2), y = L.kitchen.y + 2;
            double pad = 12, iw = w - pad * 2;
            var lines = Wrap(step.text, "600 14px Nunito", iw);
            double h = 8 + 14 + 6 + lines.Count * 18.2;
            int nIcons = step.icons?.Count ?? 0;
            double iconRows = Math.Ceiling(nIcons / 2.0);
            if (nIcons > 0) h += 6 + iconRows * 46;
            bool hasNext = step.next;
            if (hasNext) h += 6 + 38;
            h += 10;
            tutCardH = h;
            tutCardRect = new Rect(x, y, w, h);
            // Card with a yellow ring.
            X.fillStyle = "rgba(0,0,0,.4)"; X.fillRect(x + 2, y + 8, w, h);
            X.fillStyle = "#ffd23a"; X.fillRect(x - 3, y - 3, w + 6, h + 6);
            X.fillStyle = PAPER; X.fillRect(x, y, w, h);
            X.font = "500 11px \"DM Mono\""; X.fillStyle = MUTED; X.textBaseline = "middle";
            SpacedText(("Step " + (G.tut.i + 1) + " of " + Data.TUT_STEPS.Count).ToUpperInvariant(), x + pad, y + 8 + 7, 0.9);
            double ty = y + 8 + 14 + 6;
            X.font = "600 14px Nunito"; X.fillStyle = TEXT; X.textAlign = "left";
            for (int i = 0; i < lines.Count; i++) X.fillText(lines[i], x + pad, ty + 18.2 * (i + 0.5));
            ty += lines.Count * 18.2;
            if (nIcons > 0)
            {
                ty += 6;
                double colW = (iw - 10) / 2;
                for (int i = 0; i < nIcons; i++)
                {
                    double ix = x + pad + (i % 2) * (colW + 10), iy = ty + Math.Floor(i / 2.0) * 46;
                    symbolIcon(step.icons[i][0], ix, iy + 2, 40);
                    var ll = Wrap(step.icons[i][1], "800 13px Nunito", colW - 48);
                    X.font = "800 13px Nunito"; X.fillStyle = TEXT; X.textAlign = "left"; X.textBaseline = "middle";
                    for (int k = 0; k < ll.Count; k++) X.fillText(ll[k], ix + 48, iy + 22 - (ll.Count - 1) * 8 + k * 16);
                }
                ty += iconRows * 46;
            }
            if (hasNext)
            {
                ty += 6;
                string label = step.finish ? "Start cooking" : "Next";
                X.font = "400 15px \"Pixelify Sans\""; double bw = X.measureText(label).width + 24;
                var r = new Rect(x + w - pad - bw, ty, bw, 34);
                uiHitsNext.Add(("tut-next", r));
                uiDraw = true;
                BtnVisual(r, "btn small", label, null, uiPressed == "tut-next", false);
                if (Clicked("tut-next")) { tutNext(); }
            }
        }
    }
}
