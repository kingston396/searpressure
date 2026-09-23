using System;
using System.Collections.Generic;

namespace SearPressure
{
    public struct Vtx
    {
        public float x, y, u, v;
        public byte r, g, b, a;
    }

    public struct DrawCmd { public int tex, start, count; }

    public struct TextMetrics { public double width; }

    // A small immediate-mode 2D canvas with the same API shape as the browser's CanvasRenderingContext2D
    // (the subset the web game uses), so the game's drawing code ports line for line.
    // Everything is turned into textured triangles in device pixels; the platform draws the list.
    // Texture ids: atlas pages are 0..N-1; font textures come from IFontSource.TextureId.
    public sealed class Canvas
    {
        struct State
        {
            public double a, b, c, d, e, f;
            public double globalAlpha, lineWidth;
            public string fillStyle, strokeStyle, font, textAlign, textBaseline, lineJoin;
        }

        public readonly List<Vtx> Verts = new List<Vtx>(16384);
        public readonly List<DrawCmd> Cmds = new List<DrawCmd>(256);
        public IFontSource Fonts;
        public bool imageSmoothingEnabled;

        State s;
        readonly Stack<State> stack = new Stack<State>();
        readonly List<List<double>> path = new List<List<double>>();
        readonly List<bool> pathClosed = new List<bool>();
        List<double> sub;
        int curTex = -1;

        public Canvas() { Reset(); }

        public void Reset()
        {
            Verts.Clear(); Cmds.Clear(); stack.Clear(); curTex = -1;
            s = new State { a = 1, d = 1, globalAlpha = 1, lineWidth = 1, fillStyle = "#000000", strokeStyle = "#000000", font = "10px sans-serif", textAlign = "start", textBaseline = "alphabetic", lineJoin = "miter" };
            beginPath();
        }

        // ---- state ----
        public double globalAlpha { get => s.globalAlpha; set => s.globalAlpha = double.IsNaN(value) ? s.globalAlpha : Math.Max(0, Math.Min(1, value)); }
        public string fillStyle { get => s.fillStyle; set => s.fillStyle = value; }
        public string strokeStyle { get => s.strokeStyle; set => s.strokeStyle = value; }
        public double lineWidth { get => s.lineWidth; set => s.lineWidth = value; }
        public string font { get => s.font; set => s.font = value; }
        public string textAlign { get => s.textAlign; set => s.textAlign = value; }
        public string textBaseline { get => s.textBaseline; set => s.textBaseline = value; }
        public string lineJoin { get => s.lineJoin; set => s.lineJoin = value; }

        public void save() => stack.Push(s);
        public void restore() { if (stack.Count > 0) s = stack.Pop(); }

        public void setTransform(double a, double b, double c, double d, double e, double f) { s.a = a; s.b = b; s.c = c; s.d = d; s.e = e; s.f = f; }
        public void resetTransform() => setTransform(1, 0, 0, 1, 0, 0);
        public void transform(double a, double b, double c, double d, double e, double f)
        {
            var o = s;
            s.a = o.a * a + o.c * b; s.b = o.b * a + o.d * b;
            s.c = o.a * c + o.c * d; s.d = o.b * c + o.d * d;
            s.e = o.a * e + o.c * f + o.e; s.f = o.b * e + o.d * f + o.f;
        }
        public void translate(double x, double y) => transform(1, 0, 0, 1, x, y);
        public void scale(double x, double y) => transform(x, 0, 0, y, 0, 0);
        public void rotate(double ang) { double cs = Math.Cos(ang), sn = Math.Sin(ang); transform(cs, sn, -sn, cs, 0, 0); }

        double ScaleFactor() => Math.Sqrt(Math.Abs(s.a * s.d - s.b * s.c));
        void Tx(double x, double y, out float ox, out float oy) { ox = (float)(s.a * x + s.c * y + s.e); oy = (float)(s.b * x + s.d * y + s.f); }

        // ---- emitting ----
        void UseTex(int tex)
        {
            if (tex == curTex && Cmds.Count > 0) return;
            curTex = tex;
            Cmds.Add(new DrawCmd { tex = tex, start = Verts.Count, count = 0 });
        }
        void Emit(float x, float y, float u, float v, Rgba c)
        {
            Verts.Add(new Vtx { x = x, y = y, u = u, v = v, r = c.r, g = c.g, b = c.b, a = c.a });
            var cmd = Cmds[Cmds.Count - 1]; cmd.count++; Cmds[Cmds.Count - 1] = cmd;
        }
        Rgba Col(string style, double alphaMul = 1)
        {
            var c = Css.Parse(style);
            c.a = (byte)Math.Round(c.a * s.globalAlpha * alphaMul);
            return c;
        }

        // A transformed quad with a UV rectangle.
        void Quad(double x, double y, double w, double h, int tex, float u0, float v0, float u1, float v1, Rgba c)
        {
            if (c.a == 0) return;
            Tx(x, y, out var x0, out var y0); Tx(x + w, y, out var x1, out var y1);
            Tx(x + w, y + h, out var x2, out var y2); Tx(x, y + h, out var x3, out var y3);
            UseTex(tex);
            Emit(x0, y0, u0, v0, c); Emit(x1, y1, u1, v0, c); Emit(x2, y2, u1, v1, c);
            Emit(x0, y0, u0, v0, c); Emit(x2, y2, u1, v1, c); Emit(x3, y3, u0, v1, c);
        }
        void Tri(float ax, float ay, float bx, float by, float cx, float cy, Rgba c)
        {
            UseTex(0);
            const float a0 = 1f / Atlas.Size, a1 = 3f / Atlas.Size;
            Emit(ax, ay, a0, a0, c); Emit(bx, by, a1, a0, c); Emit(cx, cy, a0, a1, c);
        }
        void DevQuad(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy, Rgba c)
        {
            Tri((float)ax, (float)ay, (float)bx, (float)by, (float)cx, (float)cy, c);
            Tri((float)ax, (float)ay, (float)cx, (float)cy, (float)dx, (float)dy, c);
        }

        // ---- rectangles ----
        public void fillRect(double x, double y, double w, double h)
        {
            if (w < 0) { x += w; w = -w; }
            if (h < 0) { y += h; h = -h; }
            const float a0 = 1f / Atlas.Size, a1 = 3f / Atlas.Size;
            Quad(x, y, w, h, 0, a0, a0, a1, a1, Col(s.fillStyle));
        }
        public void clearRect(double x, double y, double w, double h) { }
        public void strokeRect(double x, double y, double w, double h)
        {
            beginPath(); rect(x, y, w, h); stroke();
        }

        // ---- images ----
        public void drawImage(Img img, double dx, double dy, double dw, double dh)
        {
            if (img == null) return;
            var c = new Rgba(255, 255, 255, (byte)Math.Round(255 * s.globalAlpha));
            Quad(dx, dy, dw, dh, img.page, img.u0, img.v0, img.u1, img.v1, c);
        }
        public void drawImage(Img img, double dx, double dy) => drawImage(img, dx, dy, img.width, img.height);
        public void drawImage(Img img, double sx, double sy, double sw, double sh, double dx, double dy, double dw, double dh)
        {
            if (img == null) return;
            float S = Atlas.Size;
            var c = new Rgba(255, 255, 255, (byte)Math.Round(255 * s.globalAlpha));
            Quad(dx, dy, dw, dh, img.page, (float)((img.x + sx) / S), (float)((img.y + sy) / S), (float)((img.x + sx + sw) / S), (float)((img.y + sy + sh) / S), c);
        }

        // ---- paths ----
        public void beginPath() { path.Clear(); pathClosed.Clear(); sub = null; }
        void NewSub(double dx, double dy) { sub = new List<double> { dx, dy }; path.Add(sub); pathClosed.Add(false); }
        public void moveTo(double x, double y) { Tx(x, y, out var dx, out var dy); NewSub(dx, dy); }
        public void lineTo(double x, double y)
        {
            Tx(x, y, out var dx, out var dy);
            if (sub == null) { NewSub(dx, dy); return; }
            sub.Add(dx); sub.Add(dy);
        }
        public void closePath()
        {
            if (sub == null) return;
            pathClosed[pathClosed.Count - 1] = true;
            double x = sub[0], y = sub[1];
            sub = new List<double> { x, y }; path.Add(sub); pathClosed.Add(false);
        }
        public void rect(double x, double y, double w, double h)
        {
            moveTo(x, y); lineTo(x + w, y); lineTo(x + w, y + h); lineTo(x, y + h); closePath();
        }
        public void arc(double x, double y, double r, double a0, double a1, bool ccw = false)
        {
            double sweep = a1 - a0;
            if (!ccw) { if (sweep < 0) sweep = sweep % (2 * Math.PI) + 2 * Math.PI; if (a1 - a0 >= 2 * Math.PI) sweep = 2 * Math.PI; }
            else { if (sweep > 0) sweep = sweep % (2 * Math.PI) - 2 * Math.PI; if (a0 - a1 >= 2 * Math.PI) sweep = -2 * Math.PI; }
            double devR = r * ScaleFactor();
            int n = Math.Max(6, Math.Min(96, (int)Math.Ceiling(Math.Abs(sweep) * Math.Max(2, devR) / 3)));
            for (int i = 0; i <= n; i++)
            {
                double t = a0 + sweep * i / n, px = x + Math.Cos(t) * r, py = y + Math.Sin(t) * r;
                if (i == 0 && sub != null) lineTo(px, py);
                else if (i == 0) moveTo(px, py);
                else lineTo(px, py);
            }
        }
        public void ellipse(double x, double y, double rx, double ry, double rot, double a0, double a1, bool ccw = false)
        {
            save(); translate(x, y); rotate(rot); scale(rx, ry); arc(0, 0, 1, a0, a1, ccw); restore();
        }
        public void arcTo(double x1, double y1, double x2, double y2, double r)
        {
            // Work in user space: find the current point by inverting the transform.
            if (sub == null) { moveTo(x1, y1); return; }
            Inv(sub[sub.Count - 2], sub[sub.Count - 1], out var x0, out var y0);
            double v1x = x0 - x1, v1y = y0 - y1, v2x = x2 - x1, v2y = y2 - y1;
            double l1 = Math.Sqrt(v1x * v1x + v1y * v1y), l2 = Math.Sqrt(v2x * v2x + v2y * v2y);
            if (l1 < 1e-9 || l2 < 1e-9 || r <= 0) { lineTo(x1, y1); return; }
            v1x /= l1; v1y /= l1; v2x /= l2; v2y /= l2;
            double cos = v1x * v2x + v1y * v2y, ang = Math.Acos(Math.Max(-1, Math.Min(1, cos)));
            if (Math.Abs(Math.Sin(ang)) < 1e-9) { lineTo(x1, y1); return; }
            double dist = r / Math.Tan(ang / 2);
            double t1x = x1 + v1x * dist, t1y = y1 + v1y * dist, t2x = x1 + v2x * dist, t2y = y1 + v2y * dist;
            double bx = v1x + v2x, by = v1y + v2y, bl = Math.Sqrt(bx * bx + by * by);
            double cd = r / Math.Sin(ang / 2);
            double cx = x1 + bx / bl * cd, cy = y1 + by / bl * cd;
            double s0 = Math.Atan2(t1y - cy, t1x - cx), s1 = Math.Atan2(t2y - cy, t2x - cx);
            double cross = v1x * v2y - v1y * v2x;
            lineTo(t1x, t1y);
            arc(cx, cy, r, s0, s1, cross > 0);
        }
        void Inv(double dx, double dy, out double x, out double y)
        {
            double det = s.a * s.d - s.b * s.c;
            if (Math.Abs(det) < 1e-12) { x = dx; y = dy; return; }
            double px = dx - s.e, py = dy - s.f;
            x = (s.d * px - s.c * py) / det; y = (-s.b * px + s.a * py) / det;
        }

        public void fill()
        {
            var c = Col(s.fillStyle);
            if (c.a == 0) return;
            foreach (var p in path) if (p.Count >= 6) FillPoly(p, c);
        }

        public void stroke()
        {
            var c = Col(s.strokeStyle);
            if (c.a == 0) return;
            double hw = s.lineWidth * ScaleFactor() / 2;
            for (int k = 0; k < path.Count; k++)
            {
                var p = path[k];
                int n = p.Count / 2;
                if (n < 2) continue;
                int segs = pathClosed[k] ? n : n - 1;
                for (int i = 0; i < segs; i++)
                {
                    int j = (i + 1) % n;
                    double ax = p[i * 2], ay = p[i * 2 + 1], bx = p[j * 2], by = p[j * 2 + 1];
                    double dx = bx - ax, dy = by - ay, l = Math.Sqrt(dx * dx + dy * dy);
                    if (l < 1e-9) continue;
                    double nx = -dy / l * hw, ny = dx / l * hw;
                    // Extend each end by half the width so corners meet (square joins).
                    double ex = dx / l * hw, ey = dy / l * hw;
                    DevQuad(ax - ex + nx, ay - ey + ny, bx + ex + nx, by + ey + ny, bx + ex - nx, by + ey - ny, ax - ex - nx, ay - ey - ny, c);
                }
            }
        }

        // Ear clipping in device space (works for the simple polygons the game draws).
        void FillPoly(List<double> p, Rgba c)
        {
            int n = p.Count / 2;
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                // Skip consecutive duplicates.
                if (idx.Count > 0) { int q = idx[idx.Count - 1]; if (Math.Abs(p[q * 2] - p[i * 2]) < 1e-6 && Math.Abs(p[q * 2 + 1] - p[i * 2 + 1]) < 1e-6) continue; }
                idx.Add(i);
            }
            if (idx.Count > 2) { int f = idx[0], l = idx[idx.Count - 1]; if (Math.Abs(p[f * 2] - p[l * 2]) < 1e-6 && Math.Abs(p[f * 2 + 1] - p[l * 2 + 1]) < 1e-6) idx.RemoveAt(idx.Count - 1); }
            if (idx.Count < 3) return;
            double area = 0;
            for (int i = 0; i < idx.Count; i++) { int a = idx[i], b = idx[(i + 1) % idx.Count]; area += p[a * 2] * p[b * 2 + 1] - p[b * 2] * p[a * 2 + 1]; }
            bool ccw = area > 0;
            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    double ax = p[ia * 2], ay = p[ia * 2 + 1], bx = p[ib * 2], by = p[ib * 2 + 1], cx = p[ic * 2], cy = p[ic * 2 + 1];
                    double cr = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                    if (ccw ? cr <= 0 : cr >= 0) continue;
                    bool inside = false;
                    foreach (int q in idx)
                    {
                        if (q == ia || q == ib || q == ic) continue;
                        if (InTri(p[q * 2], p[q * 2 + 1], ax, ay, bx, by, cx, cy)) { inside = true; break; }
                    }
                    if (inside) continue;
                    Tri((float)ax, (float)ay, (float)bx, (float)by, (float)cx, (float)cy, c);
                    idx.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break;
            }
            if (idx.Count >= 3)
            {
                // Whatever is left (or a degenerate shape): fan it.
                for (int i = 1; i + 1 < idx.Count; i++)
                    Tri((float)p[idx[0] * 2], (float)p[idx[0] * 2 + 1], (float)p[idx[i] * 2], (float)p[idx[i] * 2 + 1], (float)p[idx[i + 1] * 2], (float)p[idx[i + 1] * 2 + 1], c);
            }
        }
        static bool InTri(double px, double py, double ax, double ay, double bx, double by, double cx, double cy)
        {
            double d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
            double d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
            double d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // ---- text ----
        const string Symbols = "★✓✗";

        public TextMetrics measureText(string text)
        {
            var spec = FontParse.Parse(s.font);
            double sc = ScaleFactor(); if (sc <= 0) sc = 1;
            int px = Math.Max(1, (int)Math.Round(spec.size * sc));
            return new TextMetrics { width = MeasureDev(spec.face, px, text ?? "") / sc };
        }

        double MeasureDev(Face face, int px, string text)
        {
            if (Fonts == null) return text.Length * px * 0.55;
            Fonts.Request(face, px, text);
            double w = 0;
            foreach (char ch in text)
            {
                if (Fonts.TryGlyph(face, px, ch, out var g)) w += g.advance;
                else w += Symbols.IndexOf(ch) >= 0 ? px * 0.9 : px * 0.5;
            }
            return w;
        }

        public void fillText(string text, double x, double y) => DrawText(text, x, y, Col(s.fillStyle), 0);
        public void strokeText(string text, double x, double y)
        {
            // An outline: the text drawn around the pen in a ring, under whatever gets filled on top.
            var c = Col(s.strokeStyle);
            double r = s.lineWidth / 2;
            for (int k = 0; k < 12; k++)
            {
                double a = k * Math.PI * 2 / 12;
                DrawText(text, x + Math.Cos(a) * r, y + Math.Sin(a) * r, c, 0);
            }
        }

        void DrawText(string text, double x, double y, Rgba c, int _)
        {
            if (string.IsNullOrEmpty(text) || c.a == 0) return;
            var spec = FontParse.Parse(s.font);
            double sc = ScaleFactor(); if (sc <= 0) return;
            int px = Math.Max(1, (int)Math.Round(spec.size * sc));
            double inv = 1 / sc;
            double w = MeasureDev(spec.face, px, text) * inv;
            double asc = Fonts != null ? Fonts.Ascent(spec.face, px) * inv : spec.size * 0.8;
            double desc = Fonts != null ? Fonts.Descent(spec.face, px) * inv : spec.size * 0.2;
            string al = s.textAlign;
            double pen = al == "center" ? x - w / 2 : al == "right" || al == "end" ? x - w : x;
            string bl = s.textBaseline;
            double baseY = bl == "top" || bl == "hanging" ? y + asc : bl == "middle" ? y + (asc - desc) / 2 : bl == "bottom" || bl == "ideographic" ? y - desc : y;
            int tex = Fonts != null ? Fonts.TextureId(spec.face) : 0;
            foreach (char ch in text)
            {
                if (Fonts != null && Fonts.TryGlyph(spec.face, px, ch, out var g))
                {
                    if (g.x1 > g.x0 && g.y1 > g.y0)
                    {
                        double gx = pen + g.x0 * inv, gy = baseY + g.y0 * inv, gw = (g.x1 - g.x0) * inv, gh = (g.y1 - g.y0) * inv;
                        Quad(gx, gy, gw, gh, tex, g.u0, g.v0, g.u1, g.v1, c);
                    }
                    pen += g.advance * inv;
                }
                else if (Symbols.IndexOf(ch) >= 0)
                {
                    double em = px * 0.9 * inv;
                    DrawSymbol(ch, pen, baseY - asc * 0.78, em, c);
                    pen += em;
                }
                else pen += px * 0.5 * inv;
            }
        }

        // Symbols the pixel fonts don't have, drawn as shapes: a five-point star, a tick and a cross.
        void DrawSymbol(char ch, double x, double y, double em, Rgba c)
        {
            var saveFill = s.fillStyle; var saveAlpha = s.globalAlpha;
            s.fillStyle = "rgba(" + c.r + "," + c.g + "," + c.b + "," + (c.a / 255.0).ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
            s.globalAlpha = 1;
            double cx = x + em / 2, cy = y + em / 2, R = em * 0.48;
            beginPath();
            if (ch == '★')
            {
                for (int i = 0; i < 10; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 == 0 ? R : R * 0.42;
                    if (i == 0) moveTo(cx + Math.Cos(a) * rr, cy + Math.Sin(a) * rr); else lineTo(cx + Math.Cos(a) * rr, cy + Math.Sin(a) * rr);
                }
                closePath(); fill();
            }
            else
            {
                double lw = em * 0.16;
                if (ch == '✓') { Seg(cx - R * 0.7, cy, cx - R * 0.15, cy + R * 0.55, lw); Seg(cx - R * 0.15, cy + R * 0.55, cx + R * 0.75, cy - R * 0.6, lw); }
                else { Seg(cx - R * 0.6, cy - R * 0.6, cx + R * 0.6, cy + R * 0.6, lw); Seg(cx + R * 0.6, cy - R * 0.6, cx - R * 0.6, cy + R * 0.6, lw); }
            }
            beginPath();
            s.fillStyle = saveFill; s.globalAlpha = saveAlpha;
        }
        void Seg(double ax, double ay, double bx, double by, double lw)
        {
            double dx = bx - ax, dy = by - ay, l = Math.Sqrt(dx * dx + dy * dy), nx = -dy / l * lw / 2, ny = dx / l * lw / 2;
            beginPath(); moveTo(ax + nx, ay + ny); lineTo(bx + nx, by + ny); lineTo(bx - nx, by - ny); lineTo(ax - nx, ay - ny); closePath(); fill();
        }
    }
}
