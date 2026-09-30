using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MetaMove.UI.Kit
{
    // Narration surface, after the HRI study's story card: a glass card that travels with
    // the view, a media slot on top (tutorial picture or looping clip) and the spoken line
    // below, so the demo also reads with the sound off - and the room can follow on the
    // casting screen.
    //
    // Without media the slot shows a tutorial placeholder (play icon + label) when
    // showPlaceholder is on, so the layout is fixed from the start and the clips can be
    // dropped in later without the card changing size.
    public class StoryCard : MonoBehaviour
    {
        public TMP_FontAsset font;
        [Header("Placement")]
        public float distance = 1.05f;
        public float elevation = 9f;        // degrees above the horizon
        public float azimuth = -24f;        // left of the view, keeps the robot in sight
        public float worldWidth = 0.50f;
        public float deadzone = 14f;
        public float followTime = 0.35f;
        public float fadeTime = 0.2f;
        [Header("Media")]
        public bool showPlaceholder = true;
        public string placeholderLabel = "TUTORIAL";

        const float W = 900f, MediaH = 470f, TitleH = 56f, TextH = 170f, Pad = 30f;
        const float H = Pad + MediaH + Pad * 0.6f + TitleH + TextH + Pad;

        Canvas _canvas;
        CanvasGroup _group;
        Image _mediaFrame, _mediaImage, _placeholderIcon;
        RawImage _video;
        TextMeshProUGUI _title, _caption, _placeholderText, _step;
        VideoPlayer _player;
        RenderTexture _rt;
        float _shown, _target;
        bool _placed;

        void Awake() => Build();

        public void Show(string title, string caption, Sprite image = null, VideoClip clip = null, string step = "")
        {
            if (_canvas == null) Build();
            _title.text = title != null ? title.ToUpperInvariant() : "";
            _caption.text = caption ?? "";
            _step.text = step ?? "";
            SetMedia(image, clip);
            if (_target < 0.5f) _placed = false;   // re-appear in front of the viewer
            _target = 1f;
        }

        public void Hide() => _target = 0f;
        public bool Visible => _target > 0.5f;

        void SetMedia(Sprite image, VideoClip clip)
        {
            bool hasImage = image != null, hasClip = clip != null;
            _mediaImage.gameObject.SetActive(hasImage);
            _video.gameObject.SetActive(hasClip);
            bool ph = !hasImage && !hasClip && showPlaceholder;
            _placeholderIcon.gameObject.SetActive(ph);
            _placeholderText.gameObject.SetActive(ph);
            _mediaFrame.gameObject.SetActive(hasImage || hasClip || showPlaceholder);
            if (hasImage) _mediaImage.sprite = image;
            if (hasClip)
            {
                if (_player == null)
                {
                    _rt = new RenderTexture(960, 540, 0) { name = "StoryCardVideo" };
                    _player = gameObject.AddComponent<VideoPlayer>();
                    _player.renderMode = VideoRenderMode.RenderTexture;
                    _player.targetTexture = _rt;
                    _player.isLooping = true;
                    _player.audioOutputMode = VideoAudioOutputMode.None;
                    _video.texture = _rt;
                }
                if (_player.clip != clip) { _player.clip = clip; _player.Play(); }
            }
            else if (_player != null) _player.Stop();
        }

        void LateUpdate()
        {
            if (_canvas == null || Camera.main == null) return;
            _shown = Mathf.MoveTowards(_shown, _target, Time.deltaTime / Mathf.Max(0.01f, fadeTime));
            _group.alpha = Mathf.SmoothStep(0f, 1f, _shown);
            _canvas.enabled = _shown > 0.001f;
            if (!_canvas.enabled) return;
            UIKit.LazyFollow(_canvas.transform, Camera.main.transform, distance, elevation, deadzone, followTime, ref _placed, azimuth);
            // gentle scale-in while appearing
            _canvas.transform.localScale = Vector3.one * (worldWidth / W) * Mathf.Lerp(0.94f, 1f, _group.alpha);
        }

        void Build()
        {
            _canvas = UIKit.WorldCanvas(transform, "StoryCardCanvas", new Vector2(W, H), 7);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            var root = (RectTransform)_canvas.transform;
            _canvas.transform.localScale = Vector3.one * (worldWidth / W);

            var bg = UIKit.Panel("Glass", root, UIKit.Glass, 46f, 5f);
            UIKit.Stretch(bg.rectTransform);

            // media slot
            _mediaFrame = UIKit.Panel("Media", root, UIKit.GlassLight, 28f, 0f, false);
            UIKit.Place(_mediaFrame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -Pad), new Vector2(W - 2f * Pad, MediaH));
            _mediaImage = new GameObject("Image", typeof(Image)).GetComponent<Image>();
            _mediaImage.transform.SetParent(_mediaFrame.transform, false);
            _mediaImage.preserveAspect = true; _mediaImage.raycastTarget = false;
            UIKit.Stretch(_mediaImage.rectTransform, 8f);
            _video = new GameObject("Video", typeof(RawImage)).GetComponent<RawImage>();
            _video.transform.SetParent(_mediaFrame.transform, false);
            _video.raycastTarget = false;
            UIKit.Stretch(_video.rectTransform, 8f);
            _placeholderIcon = new GameObject("PlaceholderIcon", typeof(Image)).GetComponent<Image>();
            _placeholderIcon.transform.SetParent(_mediaFrame.transform, false);
            _placeholderIcon.sprite = UIKit.Play; _placeholderIcon.color = new Color(1f, 1f, 1f, 0.35f); _placeholderIcon.raycastTarget = false;
            UIKit.Place(_placeholderIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(150f, 150f));
            _placeholderText = UIKit.Text("PlaceholderLabel", _mediaFrame.transform, font, 26f, new Color(1f, 1f, 1f, 0.4f));
            UIKit.Place(_placeholderText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -86f), new Vector2(500f, 40f));
            _placeholderText.text = placeholderLabel; _placeholderText.characterSpacing = 10f;

            // step counter (top right, over the media)
            _step = UIKit.Text("Step", root, font, 22f, UIKit.TextLo, TextAlignmentOptions.TopRight);
            UIKit.Place(_step.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Pad - 22f, -Pad - 18f), new Vector2(200f, 30f));
            _step.characterSpacing = 4f;

            // title + caption
            float y = Pad + TextH;
            _title = UIKit.Text("Title", root, font, 28f, UIKit.Accent, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            UIKit.Place(_title.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(Pad + 10f, y), new Vector2(W - 2f * Pad - 20f, TitleH));
            _title.characterSpacing = 8f;
            _caption = UIKit.Text("Caption", root, font, 40f, UIKit.TextHi, TextAlignmentOptions.TopLeft);
            UIKit.Place(_caption.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(Pad + 10f, Pad), new Vector2(W - 2f * Pad - 20f, TextH));
            _caption.enableWordWrapping = true;
            _caption.enableAutoSizing = true; _caption.fontSizeMin = 26f; _caption.fontSizeMax = 40f;
            _caption.lineSpacing = 6f;

            SetMedia(null, null);
            _canvas.enabled = false;
        }

        void OnDestroy() { if (_rt != null) _rt.Release(); }
    }
}
