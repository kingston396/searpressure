using System.Collections.Generic;
using UnityEngine;

namespace SearPressure.UnityHost
{
    // Draws a Canvas display list: triangles in device pixels (y down), textured by the sprite
    // atlas pages or a font texture, with per-vertex colour. Uses GL immediate mode, which works
    // the same in the built-in pipeline and URP.
    public sealed class CanvasRenderer2D
    {
        readonly UnityFonts fonts;
        readonly Material spriteMat, fontMat;
        readonly List<Texture2D> pages = new List<Texture2D>();

        public CanvasRenderer2D(UnityFonts fonts)
        {
            this.fonts = fonts;
            var sh = Shader.Find("Hidden/SearPressure/Canvas");
            var fsh = Shader.Find("Hidden/SearPressure/CanvasFont");
            if (sh == null || fsh == null) Debug.LogError("Sear Pressure: canvas shaders not found (Resources/SearPressure/*.shader).");
            spriteMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            fontMat = new Material(fsh) { hideFlags = HideFlags.HideAndDontSave };
            // Fresh textures: upload every atlas page again (matters when the editor skips a domain reload).
            var d = Sprites.Atlas.Dirty;
            for (int i = 0; i < d.Count; i++) d[i] = true;
        }

        public void Dispose()
        {
            foreach (var t in pages) if (t != null) Object.Destroy(t);
            pages.Clear();
            if (spriteMat != null) Object.Destroy(spriteMat);
            if (fontMat != null) Object.Destroy(fontMat);
        }

        Texture2D Page(int i)
        {
            var atlas = Sprites.Atlas;
            while (pages.Count < atlas.Pages.Count)
            {
                var t = new Texture2D(Atlas.Size, Atlas.Size, TextureFormat.RGBA32, false, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                pages.Add(t);
            }
            if (atlas.Dirty[i])
            {
                // Row 0 of our page lands on texture row 0, so our v (from the data's first row) is Unity's v.
                pages[i].LoadRawTextureData(atlas.Pages[i]);
                pages[i].Apply(false, false);
                atlas.Dirty[i] = false;
            }
            return pages[i];
        }

        public void Draw(Canvas cv, int w, int h)
        {
            // The atlas can be replaced (Sprites.Reset); start the textures over when it is.
            if (pages.Count > Sprites.Atlas.Pages.Count) { foreach (var p in pages) Object.Destroy(p); pages.Clear(); }
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, w, h, 0);
            var verts = cv.Verts;
            foreach (var cmd in cv.Cmds)
            {
                if (cmd.count == 0) continue;
                Material m;
                if (cmd.tex >= 1000) { m = fontMat; m.mainTexture = fonts.TextureOf(cmd.tex); }
                else { m = spriteMat; m.mainTexture = Page(cmd.tex); }
                m.SetPass(0);
                GL.Begin(GL.TRIANGLES);
                for (int k = 0; k < cmd.count; k++)
                {
                    var v = verts[cmd.start + k];
                    GL.Color(new Color32(v.r, v.g, v.b, v.a));
                    GL.TexCoord2(v.u, v.v);
                    GL.Vertex3(v.x, v.y, 0);
                }
                GL.End();
            }
            GL.PopMatrix();
        }
    }
}
