using System;
using System.Collections.Generic;

namespace SearPressure
{
    // A region of an atlas page: what drawImage draws.
    public sealed class Img
    {
        public int page, x, y, width, height;
        public float u0, v0, u1, v1;
    }

    // Sprite pixels packed into RGBA pages. The renderer uploads a page whenever it's dirty.
    // Page 0 starts with a small white square used for solid fills.
    public sealed class Atlas
    {
        public const int Size = 1024;
        public readonly List<byte[]> Pages = new List<byte[]>();
        public readonly List<bool> Dirty = new List<bool>();
        int shelfX, shelfY, shelfH;
        public float WhiteU, WhiteV;

        public Atlas()
        {
            NewPage();
            // 4x4 white block at the top-left, sample its centre.
            var p = Pages[0];
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) { int i = (y * Size + x) * 4; p[i] = p[i + 1] = p[i + 2] = p[i + 3] = 255; }
            shelfX = 6; shelfH = 4;
            WhiteU = 2f / Size; WhiteV = 2f / Size;
        }

        void NewPage()
        {
            Pages.Add(new byte[Size * Size * 4]);
            Dirty.Add(true);
            shelfX = 0; shelfY = 0; shelfH = 0;
        }

        // Copy an RGBA image (row 0 = top) into the atlas with a 1px transparent gutter.
        public Img Add(byte[] rgba, int w, int h)
        {
            if (w + 2 > Size || h + 2 > Size) throw new ArgumentException("image too big for atlas");
            if (shelfX + w + 2 > Size) { shelfY += shelfH + 2; shelfX = 0; shelfH = 0; }
            if (shelfY + h + 2 > Size) NewPage();
            int pi = Pages.Count - 1, ox = shelfX + 1, oy = shelfY + 1;
            var page = Pages[pi];
            for (int y = 0; y < h; y++)
                Buffer.BlockCopy(rgba, y * w * 4, page, ((oy + y) * Size + ox) * 4, w * 4);
            Dirty[pi] = true;
            shelfX += w + 2; shelfH = Math.Max(shelfH, h);
            return new Img
            {
                page = pi, x = ox, y = oy, width = w, height = h,
                u0 = (float)ox / Size, v0 = (float)oy / Size, u1 = (float)(ox + w) / Size, v1 = (float)(oy + h) / Size,
            };
        }
    }

    // Sprites from palette-letter grids, like the web version's sprite(): '.' is transparent, digits are swap slots.
    public static class Sprites
    {
        public static Atlas Atlas = new Atlas();
        static readonly Dictionary<string, Img> cache = new Dictionary<string, Img>();

        public static void Reset() { Atlas = new Atlas(); cache.Clear(); }

        public static bool Has(string name) => Data.SPR.ContainsKey(name);

        public static Img Get(string name, Sw swaps = null)
        {
            string key = swaps != null && swaps.Count > 0 ? name + "|" + swaps.Key() : name;
            if (cache.TryGetValue(key, out var img)) return img;
            if (!Data.SPR.TryGetValue(name, out var rows)) rows = new[] { "k" };
            img = Build(rows, swaps);
            cache[key] = img;
            return img;
        }

        public static Img Build(string[] rows, Sw swaps)
        {
            int w = 1, h = rows.Length;
            foreach (var r in rows) w = Math.Max(w, r.Length);
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                var row = rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    char ch = row[x];
                    if (ch == '.' || ch == ' ') continue;
                    string col = null;
                    if (swaps != null && swaps.TryGetValue(ch, out var s)) col = s;
                    if (col == null && !Data.PAL.TryGetValue(ch, out col)) continue;
                    if (col == null) continue;
                    var c = Css.Parse(col);
                    int i = (y * w + x) * 4;
                    px[i] = c.r; px[i + 1] = c.g; px[i + 2] = c.b; px[i + 3] = c.a;
                }
            }
            return Atlas.Add(px, Math.Max(1, w), Math.Max(1, h));
        }
    }
}
