using UnityEngine;

namespace MetaMove.UI.Kit
{
    // Small white line icons for round buttons, drawn in code (no image assets): tint them with
    // the Image colour. x, y run -1..1, y up; 3x3 supersampling for clean edges.
    public static class UIIcons
    {
        delegate float Shape(float x, float y);

        static Sprite _check, _report, _restart, _home, _guide, _play, _gear;

        public static Sprite Check => _check != null ? _check : (_check = Make("IconCheck", (x, y) =>
            Stroke(Mathf.Min(Seg(x, y, -0.55f, 0.02f, -0.15f, -0.40f), Seg(x, y, -0.15f, -0.40f, 0.60f, 0.42f)), 0.13f)));

        // three bars of a chart
        public static Sprite Report => _report != null ? _report : (_report = Make("IconReport", (x, y) =>
        {
            float a = Box(x, y, -0.45f, -0.55f, -0.20f, 0.05f), b = Box(x, y, -0.12f, -0.55f, 0.13f, 0.55f), c = Box(x, y, 0.21f, -0.55f, 0.46f, -0.15f);
            float floor = Box(x, y, -0.62f, -0.68f, 0.62f, -0.60f);
            return Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, floor));
        }));

        // circular arrow, open at the top right, head at the gap
        public static Sprite Restart => _restart != null ? _restart : (_restart = Make("IconRestart", (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y), ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            float ring = !(ang > 15f && ang < 80f) ? Stroke(Mathf.Abs(r - 0.55f), 0.11f) : 0f;   // gap at the top right
            // arrow head at ~80 deg pointing clockwise (towards the gap)
            float head = Tri(x, y, new Vector2(0.06f, 0.88f), new Vector2(0.06f, 0.22f), new Vector2(0.48f, 0.56f));
            return Mathf.Max(ring, head);
        }));

        public static Sprite Home => _home != null ? _home : (_home = Make("IconHome", (x, y) =>
        {
            float roof = Tri(x, y, new Vector2(-0.70f, 0.05f), new Vector2(0.70f, 0.05f), new Vector2(0f, 0.68f));
            float body = Box(x, y, -0.48f, -0.62f, 0.48f, 0.08f);
            float door = Box(x, y, -0.13f, -0.62f, 0.13f, -0.18f);
            return Mathf.Max(roof, body) * (1f - door);
        }));

        // crosshair: "show the arm where" (guide by hand)
        public static Sprite Guide => _guide != null ? _guide : (_guide = Make("IconGuide", (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float ring = Stroke(Mathf.Abs(r - 0.45f), 0.09f);
            float dot = r < 0.14f ? 1f : 0f;
            float ticks = Mathf.Max(Box(x, y, -0.06f, 0.55f, 0.06f, 0.85f), Box(x, y, -0.06f, -0.85f, 0.06f, -0.55f));
            ticks = Mathf.Max(ticks, Mathf.Max(Box(x, y, 0.55f, -0.06f, 0.85f, 0.06f), Box(x, y, -0.85f, -0.06f, -0.55f, 0.06f)));
            return Mathf.Max(Mathf.Max(ring, dot), ticks);
        }));

        public static Sprite Play => _play != null ? _play : (_play = Make("IconPlay", (x, y) =>
            Tri(x, y, new Vector2(-0.38f, -0.58f), new Vector2(-0.38f, 0.58f), new Vector2(0.62f, 0f))));

        public static Sprite Gear => _gear != null ? _gear : (_gear = Make("IconGear", (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y), ang = Mathf.Atan2(y, x);
            float teeth = Mathf.Cos(ang * 8f) > 0.2f ? 0.78f : 0.60f;
            float body = r < teeth ? 1f : 0f;
            float hole = r < 0.26f ? 1f : 0f;
            return body * (1f - hole);
        }));

        // ------------------------------------------------------------ shapes

        static float Stroke(float d, float half) => d < half ? 1f : 0f;

        static float Seg(float x, float y, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(x, y); var a = new Vector2(ax, ay); var b = new Vector2(bx, by);
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        static float Box(float x, float y, float x0, float y0, float x1, float y1) => x >= x0 && x <= x1 && y >= y0 && y <= y1 ? 1f : 0f;

        static float Tri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            var p = new Vector2(x, y);
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return neg && pos ? 0f : 1f;
        }

        static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        static Sprite Make(string name, Shape shape)
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float a = 0f;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                            a += shape((i + (sx + 0.5f) / 3f) / n * 2f - 1f, (j + (sy + 0.5f) / 3f) / n * 2f - 1f);
                    px[j * n + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a / 9f) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
