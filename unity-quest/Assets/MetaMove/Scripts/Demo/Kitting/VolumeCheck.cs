using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MetaMove.UI.Kit;

namespace MetaMove.Demo.Kitting
{
    // The demo is narrated, and a headset handed from visitor to visitor is often muted. The
    // media volume is read from Android's AudioManager (Horizon OS is Android); while it is at
    // zero a card in the middle of the view shows a muted speaker and a drawn headset whose
    // volume button is being pressed by a fingertip, over and over. StoryDirector
    // holds the welcome until the sound is on (bounded), so nobody misses the first line.
    public class VolumeCheck : MonoBehaviour
    {
        public TMP_FontAsset font;
        public float pollEvery = 0.5f;

        public static bool Muted { get; private set; }

        Canvas _canvas;
        CanvasGroup _group;
        readonly UIKit.FollowState _follow = new UIKit.FollowState();
        float _next, _shown;
        RectTransform _finger, _rocker, _plus;
        Image _rockerImg, _fingerImg;
        TextMeshProUGUI _plusText;
        // headset drawing (px on the card): the rocker on the underside, right half
        const float HsX = 150f, HsY = -44f, HsW = 400f, HsH = 190f, RockX = 98f;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject _audio;
#endif

        void Start()
        {
            Build();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _audio = activity.Call<AndroidJavaObject>("getSystemService", "audio");
            }
            catch (System.Exception e) { Debug.LogWarning("[Volume] no AudioManager: " + e.Message); }
#endif
        }

        void Update()
        {
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + pollEvery;
                bool muted = ReadMuted();
                if (muted != Muted) Debug.Log("[Volume] " + (muted ? "muted - showing the hint" : "sound on"));
                Muted = muted;
            }
            if (_canvas == null || Camera.main == null) return;
            _shown = Mathf.MoveTowards(_shown, Muted ? 1f : 0f, Time.unscaledDeltaTime / 0.3f);
            _group.alpha = Mathf.SmoothStep(0f, 1f, _shown);
            _canvas.enabled = _shown > 0.001f;
            if (_canvas.enabled) { UIKit.LazyFollow(_canvas.transform, Camera.main.transform, 0.9f, -4f, 12f, 0.3f, _follow); Animate(Mathf.Repeat(Time.unscaledTime, 2f)); }
            else _follow.placed = false;
        }

        // 2 s loop: the fingertip comes up from below, presses the button, a "+" pops, it lets go.
        void Animate(float t)
        {
            float up = t < 0.5f ? Mathf.SmoothStep(0f, 1f, t / 0.5f) : t < 1.2f ? 1f : t < 1.6f ? Mathf.SmoothStep(1f, 0f, (t - 1.2f) / 0.4f) : 0f;
            bool pressed = t >= 0.5f && t < 1.2f;
            float bottom = HsY - 0.76f * HsH / 2f;   // the visor's lower edge in the drawing
            _finger.anchoredPosition = new Vector2(HsX + RockX, bottom - 18f - 40f * (1f - up) - (pressed ? 0f : 6f));
            _fingerImg.color = new Color(1f, 1f, 1f, 0.25f + 0.65f * Mathf.Min(1f, up * 1.5f));
            _rocker.anchoredPosition = new Vector2(HsX + RockX, bottom + 1f + (pressed ? 3f : 0f));
            _rockerImg.color = pressed ? UIKit.Warn : new Color(0.55f, 0.58f, 0.62f);
            float pop = pressed ? Mathf.Clamp01((t - 0.5f) / 0.25f) : t >= 1.2f ? Mathf.Clamp01(1f - (t - 1.2f) / 0.3f) : 0f;
            _plus.anchoredPosition = new Vector2(HsX + RockX + 64f, bottom - 14f + 18f * pop);
            _plus.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, pop);
            _plusText.color = new Color(UIKit.Warn.r, UIKit.Warn.g, UIKit.Warn.b, pop);
        }

        bool ReadMuted()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_audio == null) return false;
            try { return _audio.Call<int>("getStreamVolume", 3) <= 0; }   // 3 = STREAM_MUSIC
            catch { return false; }
#else
            return false;
#endif
        }

        void Build()
        {
            const float W = 900f, H = 420f;
            _canvas = UIKit.WorldCanvas(transform, "VolumeHintCanvas", new Vector2(W, H), 11);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _canvas.transform.localScale = Vector3.one * (0.46f / 760f);   // same text size as before
            var root = (RectTransform)_canvas.transform;
            var bg = UIKit.Panel("Glass", root, new Color(UIKit.Glass.r, UIKit.Glass.g, UIKit.Glass.b, 0.96f), 40f, 4f);
            UIKit.Stretch(bg.rectTransform);
            var bar = UIKit.Panel("Accent", root, UIKit.Warn, 4f, 0f, false);
            UIKit.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(W * 0.4f, 8f));
            var title = UIKit.Text("Title", root, font, 44f, UIKit.Warn, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(W - 60f, 60f));
            title.text = "SOUND IS OFF";
            title.characterSpacing = 8f;

            // muted speaker, left
            Img("Speaker", root, MutedSpeaker(), UIKit.Warn, new Vector2(-250f, HsY), new Vector2(170f, 170f));
            // the headset from the front, its volume button on the underside, right
            Img("Headset", root, Headset(), Color.white, new Vector2(HsX, HsY), new Vector2(HsW, HsH));
            var rock = Img("Rocker", root, Capsule(), new Color(0.55f, 0.58f, 0.62f), Vector2.zero, new Vector2(64f, 18f));
            _rocker = rock.rectTransform; _rockerImg = rock;
            var fin = Img("Fingertip", root, UIKit.Disc, Color.white, Vector2.zero, new Vector2(30f, 30f));
            _finger = fin.rectTransform; _fingerImg = fin;
            _plusText = UIKit.Text("Plus", root, font, 64f, UIKit.Warn, TextAlignmentOptions.Center, FontStyles.Bold);
            _plusText.text = "+";
            _plus = _plusText.rectTransform;
            UIKit.Place(_plus, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));

            var body = UIKit.Text("Body", root, font, 30f, UIKit.TextHi, TextAlignmentOptions.Center);
            UIKit.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(W - 80f, 44f));
            body.text = "This demo talks to you. Volume up: <b>underside, right</b>.";
            _canvas.enabled = false;
        }

        // Editor preview: the card fully shown at loop time t (0..2 s).
        public Canvas Preview(float t)
        {
            if (_canvas == null) Build();
            _canvas.enabled = true; _group.alpha = 1f;
            Animate(t);
            return _canvas;
        }

        Image Img(string name, RectTransform root, Sprite sprite, Color c, Vector2 pos, Vector2 size)
        {
            var img = new GameObject(name, typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(root, false);
            img.sprite = sprite; img.color = c; img.raycastTarget = false;
            UIKit.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            return img;
        }

        // ---------------------------------------------------------------- drawings
        // x, y in -1..1 (x across the width, y up); 4x4 supersampled for clean edges.

        delegate Color Paint(float x, float y);

        static Sprite Draw(string name, int w, int h, Paint paint)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    var acc = new Color(0f, 0f, 0f, 0f);
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float x = (i + (sx + 0.5f) / 4f) / w * 2f - 1f, y = (j + (sy + 0.5f) / 4f) / h * 2f - 1f;
                            var c = paint(x, y);
                            acc += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                        }
                    acc /= 16f;
                    px[j * w + i] = acc.a > 0f ? new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a) : acc;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        // rounded box |x| < hx, |y| < hy, corner radius r (in the same units)
        static bool RBox(float x, float y, float hx, float hy, float r)
        {
            float dx = Mathf.Max(Mathf.Abs(x) - (hx - r), 0f), dy = Mathf.Max(Mathf.Abs(y) - (hy - r), 0f);
            return dx * dx + dy * dy <= r * r;
        }

        static Sprite MutedSpeaker() => Draw("MutedSpeaker", 192, 192, (x, y) =>
        {
            bool body = x > -0.80f && x < -0.48f && Mathf.Abs(y) < 0.24f;
            bool cone = x >= -0.50f && x < 0.02f && Mathf.Abs(y) < 0.24f + (x + 0.50f) * 0.80f;
            float u = x - 0.52f;
            bool cross = Mathf.Abs(u) < 0.30f && Mathf.Abs(y) < 0.30f && (Mathf.Abs(u - y) < 0.10f || Mathf.Abs(u + y) < 0.10f);
            return body || cone || cross ? Color.white : new Color(1f, 1f, 1f, 0f);
        });

        // Quest 3 from the front, abstracted: light visor, three dark sensor pills, strap stubs.
        // Drawn on a 2:1 sprite; y is scaled so the shapes keep their proportions.
        static Sprite Headset() => Draw("Headset", 400, 200, (x, y) =>
        {
            float yy = y * 0.5f;   // to the x scale
            var shell = new Color(0.86f, 0.88f, 0.90f, 1f);
            var dark = new Color(0.16f, 0.17f, 0.19f, 1f);
            if (RBox(x, yy - 0.02f, 0.92f, 0.36f, 0.20f))
            {
                foreach (float cx in new[] { -0.56f, 0f, 0.56f })
                    if (RBox(x - cx, yy - 0.02f, cx == 0f ? 0.05f : 0.065f, cx == 0f ? 0.12f : 0.17f, 0.05f)) return dark;
                bool rim = !RBox(x, yy - 0.02f, 0.895f, 0.335f, 0.18f);
                return rim ? new Color(0.70f, 0.72f, 0.75f, 1f) : shell;
            }
            if (Mathf.Abs(x) > 0.92f && Mathf.Abs(x) < 1.0f && Mathf.Abs(yy - 0.02f) < 0.08f) return new Color(0.30f, 0.31f, 0.34f, 1f);   // strap
            return new Color(1f, 1f, 1f, 0f);
        });

        static Sprite Capsule() => Draw("Capsule", 128, 36, (x, y) =>
            RBox(x, y * 36f / 128f, 0.96f, 0.26f, 0.26f) ? Color.white : new Color(1f, 1f, 1f, 0f));
    }
}
