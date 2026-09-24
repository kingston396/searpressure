using System.Collections.Generic;
using UnityEngine;

namespace SearPressure.UnityHost
{
    // The game's six fonts as Unity dynamic fonts (TTFs in Resources/SearPressure/Fonts).
    public sealed class UnityFonts : IFontSource
    {
        readonly Dictionary<Face, Font> fonts = new Dictionary<Face, Font>();
        readonly Dictionary<(Face, int), (float asc, float desc)> metrics = new Dictionary<(Face, int), (float, float)>();
        bool rebuilt;
        // Characters a font doesn't have, so we don't ask Unity again every frame (cleared on rebuild).
        readonly HashSet<(Face, int, char)> missing = new HashSet<(Face, int, char)>();
        readonly System.Action<Font> onRebuilt;

        public UnityFonts()
        {
            Load(Face.PixelifyRegular, "PixelifySans-Regular");
            Load(Face.PixelifySemiBold, "PixelifySans-SemiBold");
            Load(Face.PixelifyBold, "PixelifySans-Bold");
            Load(Face.DMMono, "DMMono-Medium");
            Load(Face.NunitoSemiBold, "Nunito-SemiBold");
            Load(Face.NunitoExtraBold, "Nunito-ExtraBold");
            onRebuilt = _ => { rebuilt = true; missing.Clear(); };
            Font.textureRebuilt += onRebuilt;
        }

        void Load(Face f, string file)
        {
            var font = Resources.Load<Font>("SearPressure/Fonts/" + file);
            if (font == null) { Debug.LogWarning("Sear Pressure: missing font " + file + ", using the built-in one."); font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            fonts[f] = font;
        }

        public void Dispose() => Font.textureRebuilt -= onRebuilt;

        public Font Get(Face f) => fonts[f];
        public Texture TextureOf(int id) => fonts[(Face)(id - 1000)].material.mainTexture;

        public void Request(Face face, int px, string text) => fonts[face].RequestCharactersInTexture(text, px, FontStyle.Normal);

        public bool TryGlyph(Face face, int px, char ch, out Glyph g)
        {
            g = default;
            var font = fonts[face];
            if (missing.Contains((face, px, ch))) return false;
            if (!font.GetCharacterInfo(ch, out var ci, px, FontStyle.Normal))
            {
                font.RequestCharactersInTexture(ch.ToString(), px, FontStyle.Normal);
                if (!font.GetCharacterInfo(ch, out ci, px, FontStyle.Normal)) { missing.Add((face, px, ch)); return false; }
            }
            if (ch != ' ' && ci.glyphWidth == 0 && ci.advance == 0) return false;
            // Unity's glyph box: x from minX to maxX, y up from the baseline. Ours is y down.
            g.x0 = ci.minX; g.x1 = ci.maxX; g.y0 = -ci.maxY; g.y1 = -ci.minY;
            g.advance = ci.advance;
            g.corners = true;
            g.tlu = ci.uvTopLeft.x; g.tlv = ci.uvTopLeft.y;
            g.tru = ci.uvTopRight.x; g.trv = ci.uvTopRight.y;
            g.bru = ci.uvBottomRight.x; g.brv = ci.uvBottomRight.y;
            g.blu = ci.uvBottomLeft.x; g.blv = ci.uvBottomLeft.y;
            return true;
        }

        (float, float) Metrics(Face face, int px)
        {
            if (metrics.TryGetValue((face, px), out var m)) return m;
            // Ascent from the font; descent from the lowest common descender.
            var font = fonts[face];
            font.RequestCharactersInTexture("Hgjpqy", px, FontStyle.Normal);
            float asc = font.ascent > 0 ? font.ascent * px / Mathf.Max(1, font.fontSize > 0 ? font.fontSize : px) : px * 0.8f;
            float desc = 0;
            foreach (char c in "gjpqy") if (font.GetCharacterInfo(c, out var ci, px, FontStyle.Normal)) desc = Mathf.Max(desc, -ci.minY);
            if (font.GetCharacterInfo('H', out var h, px, FontStyle.Normal)) asc = Mathf.Max(asc, h.maxY * 1.25f);
            m = (asc, Mathf.Max(desc, px * 0.2f));
            metrics[(face, px)] = m;
            return m;
        }
        public float Ascent(Face face, int px) => Metrics(face, px).Item1;
        public float Descent(Face face, int px) => Metrics(face, px).Item2;
        public int TextureId(Face face) => 1000 + (int)face;
        public bool TakeRebuilt() { var r = rebuilt; rebuilt = false; return r; }
    }
}
