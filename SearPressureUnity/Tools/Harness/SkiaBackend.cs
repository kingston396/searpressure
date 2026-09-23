using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace SearPressure.Harness
{
    // Glyphs rasterised with SkiaSharp into one atlas per font face.
    public sealed class SkiaFonts : IFontSource
    {
        sealed class FaceAtlas
        {
            public SKTypeface tf;
            public SKBitmap bmp = new SKBitmap(1024, 1024, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            public int x, y, rowH;
            public Dictionary<(int, char), Glyph> glyphs = new Dictionary<(int, char), Glyph>();
            public bool dirty = true;
            public SKImage img;
        }
        readonly Dictionary<Face, FaceAtlas> faces = new Dictionary<Face, FaceAtlas>();
        bool rebuilt;

        public SkiaFonts(string fontDir)
        {
            void Add(Face f, string file) { var a = new FaceAtlas { tf = SKTypeface.FromFile(Path.Combine(fontDir, file)) }; a.bmp.Erase(SKColors.Transparent); faces[f] = a; }
            Add(Face.PixelifyRegular, "PixelifySans-Regular.ttf");
            Add(Face.PixelifySemiBold, "PixelifySans-SemiBold.ttf");
            Add(Face.PixelifyBold, "PixelifySans-Bold.ttf");
            Add(Face.DMMono, "DMMono-Medium.ttf");
            Add(Face.NunitoSemiBold, "Nunito-SemiBold.ttf");
            Add(Face.NunitoExtraBold, "Nunito-ExtraBold.ttf");
        }

        public void Request(Face face, int px, string text) { foreach (char ch in text) TryGlyph(face, px, ch, out _); }

        public bool TryGlyph(Face face, int px, char ch, out Glyph g)
        {
            var a = faces[face];
            if (a.glyphs.TryGetValue((px, ch), out g)) return g.advance >= 0;
            using var font = new SKFont(a.tf, px);
            var gid = font.GetGlyph(ch);
            if (gid == 0 && ch != ' ') { g = new Glyph { advance = -1 }; a.glyphs[(px, ch)] = g; return false; }
            var widths = new float[1]; var boundsArr = new SKRect[1];
            font.GetGlyphWidths(new ushort[] { gid }, widths, boundsArr);
            float adv = widths[0];
            var b = boundsArr[0];
            int gw = (int)Math.Ceiling(b.Width) + 2, gh = (int)Math.Ceiling(b.Height) + 2;
            g = new Glyph { advance = adv };
            if (b.Width > 0 && b.Height > 0)
            {
                if (a.x + gw > a.bmp.Width) { a.x = 0; a.y += a.rowH + 1; a.rowH = 0; }
                if (a.y + gh > a.bmp.Height) { a.bmp.Erase(SKColors.Transparent); a.glyphs.Clear(); a.x = a.y = a.rowH = 0; rebuilt = true; }
                using (var c = new SKCanvas(a.bmp))
                using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = true, Typeface = a.tf, TextSize = px })
                {
                    c.DrawText(ch.ToString(), a.x + 1 - b.Left, a.y + 1 - b.Top, paint);
                }
                float S = a.bmp.Width;
                g.x0 = b.Left - 1; g.y0 = b.Top - 1; g.x1 = b.Left - 1 + gw; g.y1 = b.Top - 1 + gh;
                g.u0 = a.x / S; g.v0 = a.y / S; g.u1 = (a.x + gw) / S; g.v1 = (a.y + gh) / S;
                a.x += gw + 1; a.rowH = Math.Max(a.rowH, gh);
                a.dirty = true;
            }
            a.glyphs[(px, ch)] = g;
            return true;
        }

        public float Ascent(Face face, int px) { using var f = new SKFont(faces[face].tf, px); return -f.Metrics.Ascent; }
        public float Descent(Face face, int px) { using var f = new SKFont(faces[face].tf, px); return f.Metrics.Descent; }
        public int TextureId(Face face) => 1000 + (int)face;
        public bool TakeRebuilt() { var r = rebuilt; rebuilt = false; return r; }

        public SKImage Image(int tex)
        {
            var a = faces[(Face)(tex - 1000)];
            if (a.dirty || a.img == null) { a.img?.Dispose(); a.img = SKImage.FromBitmap(a.bmp); a.dirty = false; }
            return a.img;
        }
    }

    // Draws a Canvas display list into a bitmap, the way the Unity renderer draws it on screen.
    public static class SkiaRaster
    {
        static readonly Dictionary<int, SKImage> pageImgs = new Dictionary<int, SKImage>();

        static SKImage Page(int i)
        {
            var atlas = Sprites.Atlas;
            if (atlas.Dirty[i] || !pageImgs.ContainsKey(i))
            {
                var info = new SKImageInfo(Atlas.Size, Atlas.Size, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                pageImgs.TryGetValue(i, out var old); old?.Dispose();
                pageImgs[i] = SKImage.FromPixelCopy(info, atlas.Pages[i]);
                atlas.Dirty[i] = false;
            }
            return pageImgs[i];
        }

        public static SKBitmap Render(Canvas cv, SkiaFonts fonts, int w, int h, string bg = "#1c2133")
        {
            var bmp = new SKBitmap(w, h);
            using var c = new SKCanvas(bmp);
            var bc = Css.Parse(bg); c.Clear(new SKColor(bc.r, bc.g, bc.b, bc.a));
            foreach (var cmd in cv.Cmds)
            {
                if (cmd.count == 0) continue;
                var img = cmd.tex >= 1000 ? fonts.Image(cmd.tex) : Page(cmd.tex);
                var pos = new SKPoint[cmd.count]; var tex = new SKPoint[cmd.count]; var col = new SKColor[cmd.count];
                for (int k = 0; k < cmd.count; k++)
                {
                    var v = cv.Verts[cmd.start + k];
                    pos[k] = new SKPoint(v.x, v.y); tex[k] = new SKPoint(v.u * img.Width, v.v * img.Height); col[k] = new SKColor(v.r, v.g, v.b, v.a);
                }
                using var shader = img.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
                using var paint = new SKPaint { Shader = shader, IsAntialias = false, FilterQuality = SKFilterQuality.None };
                c.DrawVertices(SKVertexMode.Triangles, pos, tex, col, SKBlendMode.Modulate, null, paint);
            }
            return bmp;
        }

        public static void Save(SKBitmap bmp, string path)
        {
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(path, data.ToArray());
        }
    }
}
