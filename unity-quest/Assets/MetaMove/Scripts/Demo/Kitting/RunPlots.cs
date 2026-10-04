using System.Collections.Generic;
using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // The two plots of the shift report, drawn into textures (no chart library on the Quest):
    //   Timeline  arm speed (%) on top, the visitor's distance to the robot below, with the
    //             stop and speed-scaling zones as bands and the story's moments as markers
    //   Path      the cell from above - table, rack, trolley, both zone rings - and the
    //             visitor's walk, coloured by what the arm was doing: green full speed,
    //             amber scaled, red stopped
    // Text (titles, axis and marker labels) is laid over the textures as UI, not drawn here.
    public static class RunPlots
    {
        public struct Sample { public float t, dist, factor, x, z; }
        public struct Mark { public float t; public string label; }

        static readonly Color Bg = new Color(0.07f, 0.09f, 0.11f, 1f);
        static readonly Color Grid = new Color(1f, 1f, 1f, 0.07f);
        static readonly Color Red = new Color(1.00f, 0.32f, 0.28f), Amber = new Color(1.00f, 0.66f, 0.16f);
        static readonly Color Green = new Color(0.30f, 0.90f, 0.52f), Steel = new Color(0.45f, 0.50f, 0.56f);
        public static readonly Color Cyan = new Color(0.25f, 0.82f, 0.88f), White = new Color(0.95f, 0.97f, 1f);
        public static readonly Color ArmGrey = new Color(0.72f, 0.76f, 0.80f);

        public static Color SpeedColour(float f) => f <= 0.001f ? Red : f < 0.99f ? Color.Lerp(Amber, Green, f) : Green;

        // ---------------------------------------------------------------- timeline

        // Returns the texture; marker x positions (0..1 of the width) go out for the labels.
        public static Texture2D Timeline(List<Sample> s, List<Mark> marks, float near, float far, float maxDist,
                                         int w, int h, List<float> markX)
        {
            var px = Canvas(w, h);
            float total = s.Count > 1 ? Mathf.Max(1f, s[s.Count - 1].t) : 1f;
            int split = (int)(h * 0.62f);            // distance below, speed above
            int pad = 6;
            // distance strip: zone bands
            Band(px, w, h, 0, split - pad, 0f, near / maxDist, Red, 0.22f);
            Band(px, w, h, 0, split - pad, near / maxDist, far / maxDist, Amber, 0.14f);
            Band(px, w, h, 0, split - pad, far / maxDist, 1f, Green, 0.07f);
            for (int i = 1; i < 4; i++) HLine(px, w, h, (int)((split - pad) * i / 4f), Grid);
            // speed strip: filled area
            int sp0 = split + pad, sp1 = h - 1;
            HLine(px, w, h, sp0, Grid); HLine(px, w, h, sp1 - 1, Grid);
            for (int i = 0; i + 1 < s.Count; i++)
            {
                int x0 = (int)(s[i].t / total * (w - 1)), x1 = (int)(s[i + 1].t / total * (w - 1));
                for (int x = x0; x <= x1; x++)
                {
                    int top = sp0 + (int)(Mathf.Clamp01(s[i].factor) * (sp1 - sp0));
                    var c = SpeedColour(s[i].factor); c.a = 0.75f;
                    for (int y = sp0; y <= top; y++) Blend(px, w, h, x, y, c);
                }
            }
            // distance line
            for (int i = 0; i + 1 < s.Count; i++)
            {
                if (s[i].dist < 0f || s[i + 1].dist < 0f) continue;
                Line(px, w, h,
                     s[i].t / total * (w - 1), Mathf.Clamp01(s[i].dist / maxDist) * (split - pad),
                     s[i + 1].t / total * (w - 1), Mathf.Clamp01(s[i + 1].dist / maxDist) * (split - pad), White, 2.2f);
            }
            // markers
            markX.Clear();
            foreach (var m in marks)
            {
                float u = Mathf.Clamp01(m.t / total);
                markX.Add(u);
                int x = (int)(u * (w - 1));
                for (int y = 0; y < h; y += 2) Blend(px, w, h, x, y, new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f));
            }
            return ToTexture(px, w, h);
        }

        // ---------------------------------------------------------------- path, top view

        // Cell frame metres: x in [-2.5, 2.5] (cell +X = the visitor's left = image left),
        // z in [-1.5, 3.5] (wall at the top, visitor side at the bottom).
        public static Texture2D Path(List<Sample> s, float near, float far, int size)
        {
            var px = Canvas(size, size);
            const float x0 = 2.5f, z0 = -1.5f, span = 5f;
            System.Func<float, float> U = x => (x0 - x) / span * (size - 1);
            System.Func<float, float> V = z => (z - z0) / span * (size - 1);   // image row from the top: wall up, visitor down
            for (int i = 1; i < 5; i++) { HLine(px, size, size, (int)(size * i / 5f), Grid); VLine(px, size, size, (int)(size * i / 5f), Grid); }
            // wall, table, rack, trolley (cell frame, metres)
            Rect(px, size, -1.7f, -1.51f, 1.7f, -1.29f, U, V, new Color(0.55f, 0.56f, 0.58f, 0.6f), true);
            Rect(px, size, -0.65f, -0.30f, 0.35f, 0.30f, U, V, Steel, false);
            Rect(px, size, -0.62f, 0.31f, 0.62f, 1.17f, U, V, new Color(0.25f, 0.45f, 0.85f, 1f), false);
            Rect(px, size, 0.78f, 0.63f, 1.32f, 1.27f, U, V, Steel, false);   // trolley, front left of the rack
            Circle(px, size, U(0f), V(0f), near / span * (size - 1), Red, 2.2f);
            Circle(px, size, U(0f), V(0f), far / span * (size - 1), Amber, 2.2f);
            Disc(px, size, U(0f), V(0f), 9f, ArmGrey);   // robot base
            for (int i = 0; i + 1 < s.Count; i++)
                Line(px, size, size, U(s[i].x), size - 1 - V(s[i].z), U(s[i + 1].x), size - 1 - V(s[i + 1].z), SpeedColour(s[i].factor), 3f, true);
            if (s.Count > 0)
            {
                Disc(px, size, U(s[0].x), V(s[0].z), 6f, White);
                Disc(px, size, U(s[s.Count - 1].x), V(s[s.Count - 1].z), 6f, Cyan);
            }
            return ToTexture(px, size, size);
        }

        // ---------------------------------------------------------------- raster helpers

        static Color32[] Canvas(int w, int h)
        {
            var px = new Color32[w * h];
            Color32 bg = Bg;
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            return px;
        }

        static Texture2D ToTexture(Color32[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply(false, false);
            return t;
        }

        static void Blend(Color32[] px, int w, int h, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            Color d = px[i];
            px[i] = Color.Lerp(d, new Color(c.r, c.g, c.b, 1f), c.a);
        }

        static void Band(Color32[] px, int w, int h, int y0, int y1, float a, float b, Color c, float alpha)
        {
            int from = y0 + (int)(a * (y1 - y0)), to = y0 + (int)(b * (y1 - y0));
            c.a = alpha;
            for (int y = from; y < to; y++) for (int x = 0; x < w; x++) Blend(px, w, h, x, y, c);
        }

        static void HLine(Color32[] px, int w, int h, int y, Color c) { for (int x = 0; x < w; x++) Blend(px, w, h, x, y, c); }
        static void VLine(Color32[] px, int w, int h, int x, Color c) { for (int y = 0; y < h; y++) Blend(px, w, h, x, y, c); }

        // y in texture space (0 = bottom) unless 'imageY' (0 = top, flipped here)
        static void Line(Color32[] px, int w, int h, float xa, float ya, float xb, float yb, Color c, float width, bool imageY = false)
        {
            float len = Mathf.Max(1f, Vector2.Distance(new Vector2(xa, ya), new Vector2(xb, yb)));
            int steps = (int)(len * 1.5f);
            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)steps;
                Disc(px, w, h, Mathf.Lerp(xa, xb, t), Mathf.Lerp(ya, yb, t), width * 0.5f, c);
            }
        }

        static void Disc(Color32[] px, int size, float cx, float imgY, float r, Color c) => Disc(px, size, size, cx, size - 1 - imgY, r, c);

        static void Disc(Color32[] px, int w, int h, float cx, float cy, float r, Color c)
        {
            int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
            int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01(r + 0.5f - d);
                    if (a > 0f) Blend(px, w, h, x, y, new Color(c.r, c.g, c.b, a * (c.a <= 0f ? 1f : c.a)));
                }
        }

        static void Circle(Color32[] px, int size, float cx, float imgY, float r, Color c, float width)
        {
            int n = Mathf.Max(64, (int)(r * 6f));
            float cy = size - 1 - imgY;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Disc(px, size, size, cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, width * 0.5f, c);
            }
        }

        static void Rect(Color32[] px, int size, float xa, float za, float xb, float zb,
                         System.Func<float, float> U, System.Func<float, float> V, Color c, bool fill)
        {
            float u0 = Mathf.Min(U(xa), U(xb)), u1 = Mathf.Max(U(xa), U(xb));
            float v0 = Mathf.Min(V(za), V(zb)), v1 = Mathf.Max(V(za), V(zb));
            for (int y = (int)v0; y <= (int)v1; y++)
                for (int x = (int)u0; x <= (int)u1; x++)
                {
                    bool edge = x <= u0 + 1 || x >= u1 - 1 || y <= v0 + 1 || y >= v1 - 1;
                    if (fill || edge) Blend(px, size, size, x, size - 1 - y, fill ? c : new Color(c.r, c.g, c.b, 0.9f));
                }
        }
    }
}
