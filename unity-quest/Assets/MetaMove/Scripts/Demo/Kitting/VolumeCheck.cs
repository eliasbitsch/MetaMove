using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MetaMove.UI.Kit;

namespace MetaMove.Demo.Kitting
{
    // The demo is narrated, and a headset handed from visitor to visitor is often muted. The
    // media volume is read from Android's AudioManager (Horizon OS is Android); while it is at
    // zero a card in the middle of the view says where the volume button is. StoryDirector
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
            if (_canvas.enabled) UIKit.LazyFollow(_canvas.transform, Camera.main.transform, 0.9f, -4f, 12f, 0.3f, _follow);
            else _follow.placed = false;
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
            const float W = 760f, H = 300f;
            _canvas = UIKit.WorldCanvas(transform, "VolumeHintCanvas", new Vector2(W, H), 11);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _canvas.transform.localScale = Vector3.one * (0.46f / W);
            var root = (RectTransform)_canvas.transform;
            var bg = UIKit.Panel("Glass", root, new Color(UIKit.Glass.r, UIKit.Glass.g, UIKit.Glass.b, 0.96f), 40f, 4f);
            UIKit.Stretch(bg.rectTransform);
            var bar = UIKit.Panel("Accent", root, UIKit.Warn, 4f, 0f, false);
            UIKit.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(W * 0.4f, 8f));
            var title = UIKit.Text("Title", root, font, 44f, UIKit.Warn, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(W - 60f, 60f));
            title.text = "SOUND IS OFF";
            title.characterSpacing = 8f;
            var body = UIKit.Text("Body", root, font, 34f, UIKit.TextHi, TextAlignmentOptions.Center);
            UIKit.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(W - 80f, 110f));
            body.enableWordWrapping = true;
            body.text = "This demo talks to you. Press the volume button on the underside of the headset, right side.";
            // arrow towards the lower right, where the button is
            var arrow = new GameObject("Arrow", typeof(Image)).GetComponent<Image>();
            arrow.transform.SetParent(root, false);
            arrow.sprite = UIKit.Play; arrow.color = UIKit.Warn; arrow.raycastTarget = false;
            UIKit.Place(arrow.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-70f, 52f), new Vector2(64f, 64f));
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            _canvas.enabled = false;
        }
    }
}
