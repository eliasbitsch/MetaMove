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

        // Smooth head-follow with a dead zone: the panel stays put until the view has turned
        // far enough, then eases after it. Upright (world up), never rolls with the head.
        public static void LazyFollow(Transform panel, Transform head, float distance, float elevationDeg,
            float deadzoneDeg, float followTime, ref bool placed, float azimuthDeg = 0f)
        {
            var fwd = head.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd = Quaternion.AngleAxis(azimuthDeg, Vector3.up) * fwd.normalized;
            var dir = Quaternion.AngleAxis(-elevationDeg, Vector3.Cross(Vector3.up, fwd)) * fwd;
            var goal = head.position + dir * distance;
            if (!placed) { panel.position = goal; placed = true; }
            var cur = panel.position - head.position;
            float off = Vector3.Angle(new Vector3(cur.x, 0f, cur.z), fwd);
            if (off > deadzoneDeg || Mathf.Abs(cur.magnitude - distance) > 0.15f)
                panel.position = Vector3.Lerp(panel.position, goal, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, followTime)));
            var look = panel.position - head.position; look.y = 0f;
            if (look.sqrMagnitude > 1e-5f) panel.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }
    }
}
