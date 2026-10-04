using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MetaMove.UI.Kit
{
    // Shared look for the headset UI: dark glass panels with rounded corners and a thin
    // brushed-metal rim, one accent colour, and the zone colours of the floor rings.
    // Everything is built from code at runtime, so there are no art assets to keep in sync.
    public static class UIKit
    {
        public static readonly Color Glass = new Color(0.055f, 0.07f, 0.085f, 0.88f);
        public static readonly Color GlassLight = new Color(0.12f, 0.145f, 0.17f, 0.92f);
        public static readonly Color Rim = new Color(0.78f, 0.84f, 0.92f, 0.55f);
        public static readonly Color Accent = new Color(0.25f, 0.82f, 0.88f, 1f);
        public static readonly Color Ok = new Color(0.30f, 0.90f, 0.52f, 1f);
        public static readonly Color Warn = new Color(1.00f, 0.66f, 0.16f, 1f);
        public static readonly Color Stop = new Color(1.00f, 0.32f, 0.28f, 1f);
        public static readonly Color TextHi = new Color(0.96f, 0.97f, 1.00f, 1f);
        public static readonly Color TextLo = new Color(0.62f, 0.68f, 0.75f, 1f);

        public static Canvas WorldCanvas(Transform parent, string name, Vector2 size, int order = 5)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.sortingOrder = order;
            ((RectTransform)c.transform).sizeDelta = size;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            return c;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            return r;
        }

        // Rounded glass panel. rim = 0 draws none.
        public static Image Panel(string name, Transform parent, Color color, float radius, float rim = 3f, bool metal = true)
        {
            var go = new GameObject(name, typeof(Image), typeof(RoundedRectGraphic));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rr = go.GetComponent<RoundedRectGraphic>();
            rr.Configure(radius, rim, Rim);
            rr.SetMetalRim(metal && rim > 0f);
            rr.SetMetalGloss(1.6f);
            rr.SetGlow(0f, 0f, 1f);
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            else if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            return t;
        }

        // Shrink to fit instead of running out of the box: from the set size down to 'min',
        // with a little air left and right.
        public static void Fit(TextMeshProUGUI t, float min, float side = 14f)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = min;
            t.margin = new Vector4(side, 0f, side, 0f);
        }

        public static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        public static void Stretch(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }

        // ------------------------------------------------------------ generated sprites

        static Sprite _ring, _disc, _play;

        // Anti-aliased ring for radial gauges (Image.Type.Filled, Radial360).
        public static Sprite Ring => _ring != null ? _ring : (_ring = MakeSprite("UIKitRing", 256, (x, y, r) =>
            Mathf.Clamp01(Mathf.Min(r - 0.78f, 0.98f - r) * 128f)));

        public static Sprite Disc => _disc != null ? _disc : (_disc = MakeSprite("UIKitDisc", 64, (x, y, r) =>
            Mathf.Clamp01((0.98f - r) * 32f)));

        // Play triangle inside a thin circle, for the media placeholder.
        public static Sprite Play => _play != null ? _play : (_play = MakeSprite("UIKitPlay", 256, (x, y, r) =>
        {
            float circle = Mathf.Clamp01(Mathf.Min(r - 0.88f, 0.96f - r) * 128f);
            // triangle pointing right, centred slightly right of the middle
            float tx = x * 0.5f + 0.5f, ty = y * 0.5f + 0.5f;
            bool inTri = tx > 0.40f && tx < 0.68f && Mathf.Abs(ty - 0.5f) < (0.68f - tx) * 0.62f;
            return Mathf.Max(circle, inTri ? 1f : 0f);
        }));

        // Arrow pointing right: a shaft and a long pointed head, so the direction reads at once.
        static Sprite _arrow;
        public static Sprite Arrow => _arrow != null ? _arrow : (_arrow = MakeSprite("UIKitArrow", 256, (x, y, r) =>
        {
            float ay = Mathf.Abs(y);
            float shaft = Mathf.Min(Mathf.Min(x + 0.88f, 0.10f - x), 0.13f - ay);
            const float tip = 0.92f, baseX = 0.02f, half = 0.52f;
            float cos = Mathf.Cos(Mathf.Atan2(half, tip - baseX));
            float head = Mathf.Min((half * (tip - x) / (tip - baseX) - ay) * cos, x - baseX);
            return Mathf.Clamp01(Mathf.Max(shaft, head) * 128f);
        }));

        // Pointer: a long, slim isosceles triangle pointing right inside a thin ring - reads as
        // "this way" at a glance and stays calm next to the rest of the UI.
        static Sprite _pointer;
        public static Sprite Pointer => _pointer != null ? _pointer : (_pointer = MakeSprite("UIKitPointer", 256, (x, y, r) =>
        {
            float circle = Mathf.Min(r - 0.86f, 0.96f - r);
            const float tip = 0.64f, baseX = -0.44f, half = 0.27f;
            float cos = Mathf.Cos(Mathf.Atan2(half, tip - baseX));
            float tri = Mathf.Min((half * (tip - x) / (tip - baseX) - Mathf.Abs(y)) * cos, x - baseX);
            return Mathf.Clamp01(Mathf.Max(circle, tri) * 128f);
        }));

        delegate float Shape(float x, float y, float r);

        static Sprite MakeSprite(string name, int n, Shape shape)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = (i + 0.5f) / n * 2f - 1f, y = (j + 0.5f) / n * 2f - 1f;
                    float a = shape(x, y, Mathf.Sqrt(x * x + y * y));
                    px[j * n + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        public class FollowState
        {
            public bool placed, following;
            public Vector3 velocity;
        }

        // Smooth head-follow with a dead zone and hysteresis: the panel stays put until the view
        // has turned (or the head walked) far enough, then glides all the way back to its spot
        // in front and stops there - it never hovers on the edge of the dead zone, which made it
        // twitch with every bit of head sway. Upright (world up), never rolls with the head;
        // the turn towards the viewer is damped too.
        public static void LazyFollow(Transform panel, Transform head, float distance, float elevationDeg,
            float deadzoneDeg, float followTime, FollowState st, float azimuthDeg = 0f)
        {
            var fwd = head.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd = Quaternion.AngleAxis(azimuthDeg, Vector3.up) * fwd.normalized;
            var dir = Quaternion.AngleAxis(-elevationDeg, Vector3.Cross(Vector3.up, fwd)) * fwd;
            var goal = head.position + dir * distance;
            if (!st.placed)
            {
                panel.position = goal; st.placed = true; st.following = false; st.velocity = Vector3.zero;
                var l0 = goal - head.position; l0.y = 0f;
                if (l0.sqrMagnitude > 1e-5f) panel.rotation = Quaternion.LookRotation(l0.normalized, Vector3.up);
                return;
            }
            var cur = panel.position - head.position;
            float off = Vector3.Angle(new Vector3(cur.x, 0f, cur.z), fwd);
            // the height too: tracking drift (or standing up, crouching) would otherwise carry a
            // resting panel slowly up or down, out of its place under the view
            float offV = Vector3.Angle(cur, dir);
            float offEl = Mathf.Abs(Mathf.Asin(Mathf.Clamp(cur.normalized.y, -1f, 1f)) - Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f))) * Mathf.Rad2Deg;
            float dd = Mathf.Abs(cur.magnitude - distance);
            if (!st.following && (off > deadzoneDeg || offEl > 6f || dd > 0.20f)) st.following = true;
            if (st.following && offV < 2f && dd < 0.03f) { st.following = false; st.velocity = Vector3.zero; }
            if (st.following)
                panel.position = Vector3.SmoothDamp(panel.position, goal, ref st.velocity, Mathf.Max(0.05f, followTime));
            var look = panel.position - head.position; look.y = 0f;
            if (look.sqrMagnitude > 1e-5f)
                panel.rotation = Quaternion.Slerp(panel.rotation, Quaternion.LookRotation(look.normalized, Vector3.up),
                                                  1f - Mathf.Exp(-Time.deltaTime / 0.2f));
        }
    }
}
