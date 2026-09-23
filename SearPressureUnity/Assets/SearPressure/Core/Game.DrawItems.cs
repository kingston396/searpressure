using System;
using System.Collections.Generic;
using System.Linq;

namespace SearPressure
{
    public sealed partial class Game
    {
        // ---- sprites ----
        public Img sprite(string name, Sw swaps = null) => Sprites.Get(name, swaps);
        // Draw a sprite centred on (cx, cy); px is the size of one sprite pixel in current units.
        public void spr(string name, double cx, double cy, double px, Sw swaps = null, bool flip = false)
        {
            var c = sprite(name, swaps);
            double w = c.width * px, h = c.height * px;
            double x = U.Round((cx - w / 2) / px) * px, y = U.Round((cy - h / 2) / px) * px;
            if (flip) { X.save(); X.translate(x + w, y); X.scale(-1, 1); X.drawImage(c, 0, 0, w, h); X.restore(); }
            else X.drawImage(c, x, y, w, h);
        }
        // Top-left anchored, for tiles.
        public void sprTL(string name, double x, double y, double px, Sw swaps = null) { var c = sprite(name, swaps); X.drawImage(c, x, y, c.width * px, c.height * px); }

        static Sw S(params string[] kv) { var s = new Sw(); for (int i = 0; i + 1 < kv.Length; i += 2) s[kv[i][0]] = kv[i + 1]; return s; }
        static readonly Dictionary<string, Sw> PATTY = new Dictionary<string, Sw> { ["chopped"] = S("1", "#ec8a92", "2", "#b24f5b", "3", "#f7c0c6"), ["cooked"] = S("1", "#8a5a36", "2", "#5a3620", "3", "#5a3620"), ["burnt"] = S("1", "#3a3d48", "2", "#2b1d2a", "3", "#2b1d2a") };
        static readonly Dictionary<string, Sw> FRIED = new Dictionary<string, Sw> { ["cooked"] = S("1", "#ffffff", "2", "#f5b82e"), ["burnt"] = S("1", "#4a4040", "2", "#2b1d2a") };
        static readonly Dictionary<string, Sw> BACON = new Dictionary<string, Sw> { ["raw"] = S("1", "#ec8a92", "2", "#f8e0da"), ["cooked"] = S("1", "#a3492f", "2", "#e8b27a"), ["burnt"] = S("1", "#3a3d48", "2", "#5a5f6e") };
        static readonly Dictionary<string, Sw> SMOKED = new Dictionary<string, Sw> { ["raw"] = S("1", "#d95a66", "2", "#9e3440", "3", "#f7e6d8"), ["cooked"] = S("1", "#6a2e1c", "2", "#3a160c", "3", "#e8c8a0"), ["burnt"] = S("1", "#3a3d48", "2", "#2b1d2a", "3", "#5a5f6e") };
        static readonly Dictionary<string, Sw> PIZZA = new Dictionary<string, Sw>
        {
            ["stretched"] = S("1", "#f0e0c0", "2", "#f0e0c0", "3", "#f0e0c0", "4", "#f0e0c0"),
            ["sauced"] = S("1", "#f0e0c0", "2", "#d8402a", "3", "#d8402a", "4", "#d8402a"),
            ["raw"] = S("1", "#f0e0c0", "2", "#d8402a", "3", "#fff0b0", "4", "#fff0b0"),
            ["cooked"] = S("1", "#d9953f", "2", "#c8402a", "3", "#f5d33e", "4", "#f5d33e"),
            ["burnt"] = S("1", "#3a3d48", "2", "#2b1d2a", "3", "#3a3d48", "4", "#2b1d2a"),
        };
        static readonly Dictionary<string, Sw> PANCAKE = new Dictionary<string, Sw> { ["cooked"] = S("1", "#e8b060", "2", "#b8762e", "3", "#fff0a0", "4", "#8a4a16"), ["burnt"] = S("1", "#3a3d48", "2", "#2b1d2a", "3", "#4a4040", "4", "#2b1d2a") };
        static Sw Pick(Dictionary<string, Sw> d, string state) => state != null && d.TryGetValue(state, out var s) ? s : null;

        // Which sprite (and swaps) shows an ingredient in a given state.
        public static (string, Sw) ingSprite(string kind, string state)
        {
            switch (kind)
            {
                case "tomato": return (state == "raw" ? "tomato" : "tomatoChopped", null);
                case "lettuce": return (state == "raw" ? "lettuce" : "lettuceChopped", null);
                case "onion": return (state == "raw" ? "onion" : "onionChopped", null);
                case "meat": return state == "raw" ? ("steak", null) : ("patty", Pick(PATTY, state));
                case "bun": return ("bun", null);
                case "soup": return ("bowl", S("1", state == "tomato" ? "#e5552f" : "#d69a3a"));
                case "egg": return state == "raw" ? ("egg", null) : ("eggFried", Pick(FRIED, state));
                case "bacon": return ("bacon", Pick(BACON, state));
                case "batter": return state == "raw" ? ("batter", null) : ("pancakes", Pick(PANCAKE, state));
                case "coffee": return ("mug", null);
                case "potato": return state == "raw" ? ("potato", null) : state == "chopped" ? ("potatoSticks", null) : ("fries", state == "burnt" ? S("1", "#3a3d48", "2", "#2b1d2a") : S("1", "#f5d33e", "2", "#f59a2e"));
                case "icecream": return ("icecream", null);
                case "strawberry": return ("strawberry", null);
                case "cheese": return ("cheese", null);
                case "shake": return ("shake", S("1", state == "strawberry" ? "#f4a3b8" : "#fff1c8"));
                case "ribs": return ("ribs", Pick(SMOKED, state));
                case "brisket": return state == "sliced" ? ("brisketSliced", S("1", "#c46a5a", "2", "#4a2418", "3", "#f0d8b8")) : ("brisket", Pick(SMOKED, state));
                case "macaroni": return ("macaroni", null);
                case "mac": return ("bowl", S("1", "#f2c14e"));
                case "tortilla": return ("tortilla", state == "raw" ? S("1", "#f3e6c4", "2", "#e0cc9c") : state == "burnt" ? S("1", "#3a3d48", "2", "#2b1d2a") : S("1", "#f0cf8a", "2", "#b8762e"));
                case "rice": return state == "raw" ? ("riceSack", null) : ("mound", null);
                case "beans": return state == "raw" ? ("beanCan", null) : ("mound", S("w", "#8a3a20", "W", "#5a2410"));
                case "chips": return ("chips", null);
                case "avocado": return state == "raw" ? ("avocado", null) : ("guac", null);
                case "sausage": return state == "raw" ? ("sausage", S("1", "#b8483a", "2", "#e07a60")) : ("sausageChopped", S("1", "#b8483a", "2", "#f0a890"));
                case "shrimp": return ("shrimp", state == "raw" ? S("1", "#f0a0a0", "2", "#c86060") : S("1", "#f8c8b8", "2", "#e88a70"));
                case "sugar": return ("sugar", null);
                case "gumbo": return ("bowl", S("1", "#6a3a1a"));
                case "jambalaya": return ("bowl", S("1", "#c8602a"));
                case "dough": return state == "raw" ? ("dough", null) : state == "stretched" ? ("pizza", PIZZA["stretched"]) : state == "burnt" ? ("beignets", S("1", "#3a3d48", "2", "#2b1d2a")) : ("beignets", S("1", "#e8b060", "2", "#c8883a"));
                case "pizza":
                case "pepperonipizza":
                    {
                        bool pep = kind == "pepperonipizza";
                        if (state == "sliced") return ("pizzaSlice", S("1", "#d9953f", "3", "#f5d33e", "4", pep ? "#8a1a1a" : "#f5d33e"));
                        var sw = (Pick(PIZZA, state) ?? PIZZA["raw"]).Copy();
                        if (pep && state != "burnt") sw['4'] = state == "cooked" ? "#8a1a1a" : "#b02a2a";
                        return ("pizza", sw);
                    }
                case "sauce": return ("sauceJar", null);
                case "pepperoni": return ("pepperoni", S("1", "#b02a2a", "2", "#e8a0a0"));
                case "rye": return ("bread", S("1", "#6a3a1a", "2", "#a87848"));
                case "pastrami": return state == "sliced" ? ("brisketSliced", S("1", "#d06a78", "2", "#4a1a1a", "3", "#f0c8c8")) : ("brisket", S("1", "#d06a78", "2", "#6a2a2a", "3", "#f0c8c8"));
                case "mustard": return ("mustard", null);
                case "bagel": return ("bagel", state == "raw" ? S("1", "#e8c890", "2", "#c8a060") : state == "burnt" ? S("1", "#3a3d48", "2", "#2b1d2a") : S("1", "#c8843a", "2", "#8a5220"));
                case "creamcheese": return ("tub", S("1", "#6fb0d6"));
                case "clam": return state == "raw" ? ("clam", S("1", "#c8c0b0", "2", "#8a8070")) : ("sausageChopped", S("1", "#f0d8c0", "2", "#e8b890"));
                case "milk": return ("milk", null);
                case "fish": return state == "raw" ? ("fish", S("1", "#8aa8c0", "2", "#5a7890")) : state == "chopped" ? ("fillet", S("1", "#f4e8e0", "2", "#e8c8c0")) : state == "burnt" ? ("fillet", S("1", "#3a3d48", "2", "#2b1d2a")) : ("fillet", S("1", "#e8a840", "2", "#b8762e"));
                case "lobster": return ("lobster", state == "raw" ? S("1", "#3a5a78", "2", "#2a4058") : S("1", "#e8472f", "2", "#a8301d"));
                case "roll": return ("roll", null);
                case "chowder": return ("bowl", S("1", "#efe4c8"));
                case "bread": return ("bread", state == "raw" ? S("1", "#c8883a", "2", "#f0dcb0") : state == "burnt" ? S("1", "#2b1d2a", "2", "#3a3d48") : S("1", "#8a5220", "2", "#d9a050"));
                case "banana": return ("banana", null);
                case "granola": return ("granola", null);
                case "smoothie": return ("shake", S("1", "#c05a9a"));
                case "turkey": return state == "sliced" ? ("brisketSliced", S("1", "#f0e0d0", "2", "#c8843a", "3", "#f8f0e8")) : ("turkey", state == "raw" ? S("1", "#f0d0c0", "2", "#e8b8a0") : state == "burnt" ? S("1", "#3a3d48", "2", "#2b1d2a") : S("1", "#c8843a", "2", "#8a5220"));
                case "pie": return state == "sliced" ? ("pizzaSlice", S("1", "#c8883a", "3", "#d87a2a", "4", "#fff4e0")) : ("pie", state == "raw" ? S("1", "#f0b070", "2", "#f0e0c0", "3", "#e0a060") : state == "burnt" ? S("1", "#3a3d48", "2", "#2b1d2a", "3", "#2b1d2a") : S("1", "#d87a2a", "2", "#c8883a", "3", "#fff4e0"));
                case "stuffing": return ("bowl", S("1", "#a0703a"));
                case "mash": return ("bowl", S("1", "#f4ecd0"));
                case "cornmeal": return state == "raw" ? ("batter", S("Y", "#e8c060")) : ("cornbread", state == "burnt" ? S("1", "#3a3d48", "2", "#2b1d2a", "3", "#2b1d2a") : S("1", "#f2c14e", "2", "#d9953f", "3", "#b8762e"));
            }
            return ("plate", null);
        }
        public void drawIng(string kind, string state, double x, double y, double px) { var (n, sw) = ingSprite(kind, state); spr(n, x, y, px, sw); }
        public void drawKey(string k, double x, double y, double px) { var p = k.Split(':'); drawIng(p[0], p.Length > 1 ? p[1] : "raw", x, y, px); }

        public void drawPlate(IList<string> items, double x, double y, double px)
        {
            spr("plate", x, y, px);
            var keys = items.OrderBy(s => s, StringComparer.Ordinal).ToList();
            if (keys.Contains("bun:raw") && keys.Contains("meat:cooked"))
            {
                string filling = keys.Contains("lettuce:chopped") ? Data.PAL['n'] : keys.Contains("cheese:raw") ? Data.PAL['y'] : Data.PAL['B'];
                if (keys.Contains("potato:cooked"))
                {
                    spr("burger", x - px * 2, y - px, px * 0.8, S("3", filling));
                    drawIng("potato", "cooked", x + px * 3, y, px * 0.6);
                }
                else spr("burger", x, y - px, px, S("3", filling));
                return;
            }
            if (keys.Count == 1) { drawKey(keys[0], x, y - px, px * 0.84); return; }
            if (keys.Count == 2 && keys[0] == "granola:raw" && keys[1] == "smoothie:berry") { spr("bowl", x, y - px, px * 0.84, S("1", "#c05a9a")); drawIng("granola", "raw", x, y - px * 1.5, px * 0.45); return; }
            if (keys.Count == 2 && keys[0] == "dough:cooked" && keys[1] == "sugar:raw") { spr("beignets", x, y - px, px * 0.84, S("1", "#fff8e8", "2", "#e8b060")); return; }
            // Tortillas and chips sit in the middle with the toppings around them.
            var bas = keys.FirstOrDefault(k => k == "tortilla:cooked" || k == "chips:raw");
            if (bas != null)
            {
                drawKey(bas, x, y - px, px * 0.84);
                var rest = keys.ToList(); rest.RemoveAt(rest.IndexOf(bas));
                for (int i = 0; i < rest.Count; i++)
                {
                    double a = -Math.PI / 2 + i * 2 * Math.PI / rest.Count;
                    drawKey(rest[i], x + Math.Cos(a) * px * 3, y - px + Math.Sin(a) * px * 2.6, px * 0.5);
                }
                return;
            }
            for (int i = 0; i < keys.Count; i++)
            {
                double a = -Math.PI / 2 + i * 2 * Math.PI / keys.Count;
                drawKey(keys[i], x + Math.Cos(a) * px * 2.6, y - px + Math.Sin(a) * px * 2.2, px * 0.6);
            }
        }
        // One dish the way an order ticket shows it.
        public void drawDish(string recipeKey, double x, double y, double px)
        {
            var r = RECIPES[recipeKey];
            if (r.mug) drawKey(r.items[0], x, y, px);
            else drawPlate(r.items, x, y, px);
        }

        string potColor(Item p)
        {
            if (p.burnt) return "#1e1a1c";
            if (p.items.Count == 0) return "#3a3d48";
            var r = potTarget(p); string bas = r != null ? r.color : "#e5552f";
            if (potFull(p) == null) return "#8fc0d6";
            return Css.Mix("#8fc0d6", bas, U.Round(Math.Min(1, p.cook) * 6) / 6);
        }
        void drawPot(Item p, double x, double y, double px)
        {
            spr("pot", x, y, px, S("1", potColor(p)));
            if (!p.burnt && p.items.Count > 0 && p.cook < 1)
                for (int i = 0; i < p.items.Count; i++) drawKey(p.items[i], x - px * 2.5 + i * px * 2.5, y - px, px * 0.4);
        }
        void drawPan(Item p, double x, double y, double px)
        {
            spr("pan", x, y, px);
            if (p.items.Count > 0) drawIng("meat", p.burnt ? "burnt" : p.cook >= 1 ? "cooked" : "chopped", x - px, y, px * 0.66);
        }
        void drawDirty(int n, double x, double y, double px)
        {
            int k = Math.Min(n, 4);
            for (int i = 0; i < k; i++) spr("dirtyPlate", x, y - i * px * 1.5, px);
            if (n > 1)
            {
                double bx = x + px * 5, by = y - px * 5;
                X.fillStyle = Data.PAL['k']; X.fillRect(bx - px * 3, by - px * 3, px * 6, px * 6);
                X.fillStyle = "#fff5de"; X.font = "700 " + U.S(px * 5) + "px \"Pixelify Sans\", monospace"; X.textAlign = "center"; X.textBaseline = "middle";
                X.fillText(n.ToString(), bx, by + px * 0.3);
            }
        }

        public void drawItem(Item it, double x, double y, double px)
        {
            if (it == null) return;
            switch (it.type)
            {
                case "ing": drawIng(it.kind, it.state, x, y, px); break;
                case "plate": drawPlate(it.items, x, y, px); break;
                case "pot": drawPot(it, x, y, px); break;
                case "pan": drawPan(it, x, y, px); break;
                case "ext": spr("ext", x, y, px); break;
                case "dirty": drawDirty(it.n, x, y, px); break;
                case "bag": spr("bag", x, y, px); break;
            }
        }

        // Paint a dish into a square (menu cards, level list): the web version's little DOM canvases.
        public void dishIcon(string recipeKey, double x, double y, double size)
        {
            double px = Math.Max(1, Math.Floor(size * DPR / 12)) / DPR;
            drawDish(recipeKey, x + size / 2, y + size / 2, px);
        }
        public void iconAt(string name, Sw sw, double x, double y, double size)
        {
            var s0 = sprite(name, sw);
            double px = Math.Max(1, Math.Floor(size * DPR * 0.8 / Math.Max(s0.width, s0.height))) / DPR;
            spr(name, x + size / 2, y + size / 2, px, sw);
        }
        public void chefIcon(string hat, string[] hc, string[] jacket, string scarf0, string scarf1, double x, double y, double size)
        {
            spr(chefSprite("chefDown", hat), x + size / 2, y + size / 2, size / 16, outfitSwaps(hc[0], hc[1], jacket, scarf0, scarf1));
        }
    }
}
