using System;

namespace SearPressure
{
    // The six font files the game ships with (the same families as the web version).
    public enum Face { PixelifyRegular, PixelifySemiBold, PixelifyBold, DMMono, NunitoSemiBold, NunitoExtraBold }

    public struct Glyph
    {
        // Quad corners relative to the pen position on the baseline, in device pixels (y down).
        public float x0, y0, x1, y1;
        public float u0, v0, u1, v1;
        public float advance;
    }

    // Supplied by the platform: Unity's dynamic fonts in the game, SkiaSharp in the test harness.
    public interface IFontSource
    {
        // Make sure these characters are in the font texture at this pixel size.
        void Request(Face face, int px, string text);
        bool TryGlyph(Face face, int px, char ch, out Glyph g);
        // Distance from the baseline up to the top of the em box, and down to its bottom (device px).
        float Ascent(Face face, int px);
        float Descent(Face face, int px);
        // Texture id for the renderer (distinct from atlas pages).
        int TextureId(Face face);
        // True if any font texture was rebuilt since the last call (glyph UVs recorded earlier are stale).
        bool TakeRebuilt();
    }

    public struct FontSpec { public Face face; public double size; }

    public static class FontParse
    {
        static string lastSpec; static FontSpec last;

        // Parse a CSS font shorthand like `700 15px "Pixelify Sans", monospace`.
        public static FontSpec Parse(string css)
        {
            if (css == lastSpec) return last;
            int weight = 400; double size = 10;
            string rest = css.Trim();
            var parts = rest.Split(' ');
            int i = 0;
            if (parts.Length > 0 && int.TryParse(parts[0], out var w)) { weight = w; i = 1; }
            else if (parts.Length > 0 && parts[0] == "bold") { weight = 700; i = 1; }
            if (i < parts.Length)
            {
                var sz = parts[i];
                int slash = sz.IndexOf('/'); if (slash >= 0) sz = sz.Substring(0, slash);
                if (sz.EndsWith("px")) double.TryParse(sz.Substring(0, sz.Length - 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out size);
                i++;
            }
            string fam = string.Join(" ", parts, Math.Min(i, parts.Length), Math.Max(0, parts.Length - i)).ToLowerInvariant();
            Face face;
            int pix = fam.IndexOf("pixelify"), dm = fam.IndexOf("dm mono"), nun = fam.IndexOf("nunito"), cour = fam.IndexOf("courier");
            int first = Min(pix, dm, nun, cour);
            if (first < 0) face = weight >= 700 ? Face.NunitoExtraBold : Face.NunitoSemiBold;
            else if (first == pix) face = weight >= 700 ? Face.PixelifyBold : weight >= 600 ? Face.PixelifySemiBold : Face.PixelifyRegular;
            else if (first == dm || first == cour) face = Face.DMMono;
            else face = weight >= 700 ? Face.NunitoExtraBold : Face.NunitoSemiBold;
            last = new FontSpec { face = face, size = size }; lastSpec = css;
            return last;
        }

        static int Min(params int[] v) { int m = -1; foreach (var x in v) if (x >= 0 && (m < 0 || x < m)) m = x; return m; }
    }
}
