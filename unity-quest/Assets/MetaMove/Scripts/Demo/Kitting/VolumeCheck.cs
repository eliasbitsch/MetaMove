using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MetaMove.UI.Kit;

namespace MetaMove.Demo.Kitting
{
    // The demo is narrated, and a headset handed from visitor to visitor is often muted. The
    // media volume is read from Android's AudioManager (Horizon OS is Android); while it is at
    // zero a card in the middle of the view shows a muted speaker and a photo of the headset
    // from below, its volume-up button glowing cyan with a thin callout line and "Volume +",
    // the way product manuals label a part (a drawn
    // headset stands in when the photo is not in the project). StoryDirector
    // holds the welcome until the sound is on (bounded), so nobody misses the first line.
    public class VolumeCheck : MonoBehaviour
    {
        public TMP_FontAsset font;
        public float pollEvery = 0.5f;
        [Tooltip("The headset from below, transparent background. Optional: a drawing is used without it.")]
        public Texture2D photo;
        [Tooltip("Volume-up button in the photo: centre (u, v from the top-left) and size, 0..1.")]
        public Vector4 rockerUV = new Vector4(0.740f, 0.794f, 0.116f, 0.041f);
        [Tooltip("Tilt of the button in the photo, degrees.")]
        public float rockerTilt = 2f;

        public static bool Muted { get; private set; }
        // Muted and the card still up: the welcome waits for it. Closing the card with its X
        // lets the demo go on silently; it comes back the next time the sound goes off.
        public static bool Blocking => Muted && !_dismissed;
        static bool _dismissed;
        RectTransform _close;
        OVRSkeleton[] _hands;

        Canvas _canvas;
        CanvasGroup _group;
        readonly UIKit.FollowState _follow = new UIKit.FollowState();
        float _next, _shown;
        RectTransform _glow, _line;
        Image _glowImg, _rockerImg, _dot;
        TextMeshProUGUI _label;
        Vector2 _rockerAt, _lineFrom, _lineTo;   // card px
        float _draw;   // callout line drawn out 0..1 each time the card comes up
        const float W = 1040f, H = 520f;
        static readonly Vector2 PicAt = new Vector2(70f, -78f), PicSize = new Vector2(430f, 350f);   // 920 x 749 photo
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
                if (!muted) _dismissed = false;
            }
            if (_canvas == null || Camera.main == null) return;
            _shown = Mathf.MoveTowards(_shown, Blocking ? 1f : 0f, Time.unscaledDeltaTime / 0.3f);
            _group.alpha = Mathf.SmoothStep(0f, 1f, _shown);
            _canvas.enabled = _shown > 0.001f;
            if (_canvas.enabled)
            {
                // placed once in front, then it stays put so the X can be poked; only if the
                // visitor turns well away does it come round again
                if (!_follow.placed) UIKit.LazyFollow(_canvas.transform, Camera.main.transform, 0.9f, -4f, 12f, 0.3f, _follow);
                else
                {
                    var cam = Camera.main.transform;
                    var to = _canvas.transform.position - cam.position; to.y = 0f;
                    var f = cam.forward; f.y = 0f;
                    if (to.sqrMagnitude > 1e-4f && f.sqrMagnitude > 1e-4f && Vector3.Angle(to, f) > 55f) _follow.placed = false;
                }
                _draw = Mathf.MoveTowards(_draw, 1f, Time.unscaledDeltaTime / 0.6f);
                Animate(Mathf.Repeat(Time.unscaledTime, 2f));
                if (_shown > 0.9f && Poked(_close)) { _dismissed = true; Debug.Log("[Volume] hint closed"); }
            }
            else { _follow.placed = false; _draw = 0f; }
        }

        // A calm pulse on the button; the callout line draws itself out once, then stands.
        void Animate(float t)
        {
            float pulse = 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI);   // 2 s period
            _rockerImg.color = new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, Mathf.Lerp(0.45f, 0.90f, pulse));
            _glowImg.color = new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.18f * pulse);
            _glow.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.2f, 1f - pulse);
            float d = Mathf.SmoothStep(0f, 1f, _draw);
            var v = _lineTo - _lineFrom;
            _line.sizeDelta = new Vector2(v.magnitude * d, 3f);
            _label.alpha = Mathf.Clamp01((_draw - 0.7f) / 0.3f);
        }

        // An index fingertip of either hand pressed into the X.
        bool Poked(RectTransform r)
        {
            if (r == null) return false;
            if (_hands == null || _hands.Length == 0) _hands = FindObjectsByType<OVRSkeleton>(FindObjectsSortMode.None);
            foreach (var s in _hands)
            {
                if (s == null || !s.IsInitialized || !s.IsDataValid || s.Bones == null) continue;
                var id = s.GetSkeletonType().ToString().StartsWith("XRHand") ? OVRSkeleton.BoneId.XRHand_IndexTip : OVRSkeleton.BoneId.Hand_IndexTip;
                foreach (var b in s.Bones)
                {
                    if (b.Id != id) continue;
                    var local = r.InverseTransformPoint(b.Transform.position);
                    float depth = local.z * _canvas.transform.lossyScale.z;
                    if (r.rect.Contains(new Vector2(local.x, local.y)) && Mathf.Abs(depth) < 0.025f) return true;
                }
            }
            return false;
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
            var body = UIKit.Text("Body", root, font, 30f, UIKit.TextHi, TextAlignmentOptions.Center);
            UIKit.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(W - 80f, 44f));
            body.text = "This demo talks to you. Volume up: <b>underside, right</b>.";

            Img("Speaker", root, MutedSpeaker(), UIKit.Warn, new Vector2(-330f, PicAt.y), new Vector2(150f, 150f));

            // the headset from below; where its volume-up button sits on the card
            Vector2 uv;
            if (photo != null)
            {
                var pic = new GameObject("Headset", typeof(RawImage)).GetComponent<RawImage>();
                pic.transform.SetParent(root, false);
                pic.texture = photo; pic.raycastTarget = false;
                UIKit.Place(pic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), PicAt, PicSize);
                uv = new Vector2(rockerUV.x, rockerUV.y);
            }
            else
            {
                Img("Headset", root, Headset(), Color.white, PicAt, new Vector2(PicSize.x, PicSize.x / 2f));
                uv = new Vector2(0.75f, 0.5f + 0.38f * 0.5f);   // the drawing's lower edge, right half
            }
            var picH = photo != null ? PicSize.y : PicSize.x / 2f;
            _rockerAt = PicAt + new Vector2((uv.x - 0.5f) * PicSize.x, (0.5f - uv.y) * picH);
            var rs = photo != null ? new Vector2(rockerUV.z * PicSize.x, rockerUV.w * PicSize.y) : new Vector2(54f, 16f);

            _glowImg = Img("Glow", root, Capsule(), UIKit.Accent, _rockerAt, rs + new Vector2(26f, 22f));
            _glow = _glowImg.rectTransform;
            _rockerImg = Img("Rocker", root, Capsule(), UIKit.Accent, _rockerAt, rs + new Vector2(4f, 4f));
            _glow.localRotation = _rockerImg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rockerTilt);
            // close: an X in the top right corner, for when the demo should go on without sound
            var close = UIKit.Panel("Close", root, UIKit.GlassLight, 34f, 2f);
            _close = close.rectTransform;
            UIKit.Place(_close, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(68f, 68f));
            var x = UIKit.Text("X", _close, font, 40f, UIKit.TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Stretch(x.rectTransform);
            x.text = "×";

            // callout: dot on the button, a thin line up and out, the name of the button at its end
            _lineFrom = _rockerAt + new Vector2(rs.x * 0.5f + 4f, 2f);
            _lineTo = _rockerAt + new Vector2(156f, 58f);   // out past the strap, into the free space
            _dot = Img("CalloutDot", root, UIKit.Disc, UIKit.Accent, _lineFrom, new Vector2(10f, 10f));
            var line = new GameObject("CalloutLine", typeof(Image)).GetComponent<Image>();
            line.transform.SetParent(root, false);
            line.color = UIKit.Accent; line.raycastTarget = false;
            _line = line.rectTransform;
            UIKit.Place(_line, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), _lineFrom, new Vector2(0f, 3f));
            var dir = _lineTo - _lineFrom;
            _line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            _label = UIKit.Text("CalloutLabel", root, font, 28f, UIKit.Accent, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.Place(_label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), _lineTo + new Vector2(12f, 0f), new Vector2(170f, 40f));
            _label.text = "Volume +";
            _label.characterSpacing = 2f;
            _canvas.enabled = false;
        }

        // Editor preview: the card fully shown at loop time t (0..2 s).
        public Canvas Preview(float t)
        {
            if (_canvas == null) Build();
            _canvas.enabled = true; _group.alpha = 1f;
            _draw = 1f;
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
