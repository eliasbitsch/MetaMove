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
        public float distance = 1.15f;
        public float elevation = 16f;       // degrees above the horizon: centred, above the robot
        public float azimuth = 0f;          // straight ahead, where the eye already is
        public float worldWidth = 0.56f;
        public float deadzone = 14f;
        public float followTime = 0.35f;
        public float fadeTime = 0.2f;
        [Header("Media")]
        public bool showPlaceholder = true;
        public string placeholderLabel = "TUTORIAL";

        const float W = 900f, MediaH = 340f, TitleH = 64f, TextH = 190f, Pad = 30f;
        const float H = Pad + MediaH + Pad * 0.6f + TitleH + TextH + Pad;

        Canvas _canvas;
        CanvasGroup _group;
        Image _mediaFrame, _mediaImage, _placeholderIcon, _accent;
        RawImage _video;
        TextMeshProUGUI _title, _caption, _placeholderText, _step;
        VideoPlayer _player;
        RenderTexture _rt;
        float _shown, _target, _pop = 1f;
        bool _placed;

        void Awake() => Build();

        [Header("Captions")]
        [Tooltip("A caption is shown one or two lines at a time, in step with the voice.")]
        public int chunkChars = 80;
        [Tooltip("Reading pace when there is no voice clip (s per character).")]
        public float secondsPerChar = 0.065f;

        string[] _chunks;
        float[] _chunkEnds;
        int _chunk = -1;
        float _captionStart;

        // speakSeconds: length of the spoken line; the caption chunks are spread over it.
        public void Show(string title, string caption, Sprite image = null, VideoClip clip = null, string step = "", float speakSeconds = 0f)
        {
            if (_canvas == null) Build();
            _title.text = title != null ? title.ToUpperInvariant() : "";
            SetCaption(caption ?? "", speakSeconds);
            _step.text = step ?? "";
            SetMedia(image, clip);
            _placed = false;   // every new step comes to the centre of the view
            _pop = 0f;
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

        void SetCaption(string text, float seconds)
        {
            _chunks = Chunk(text, chunkChars);
            int total = 0;
            foreach (var c in _chunks) total += c.Length;
            if (seconds <= 0f) seconds = total * secondsPerChar;
            _chunkEnds = new float[_chunks.Length];
            float t = 0f;
            for (int i = 0; i < _chunks.Length; i++)
            {
                t += seconds * _chunks[i].Length / Mathf.Max(1, total);
                _chunkEnds[i] = t;
            }
            _captionStart = Time.time;
            _chunk = -1;
            UpdateCaption();
        }

        void UpdateCaption()
        {
            if (_chunks == null || _chunks.Length == 0) { _caption.text = ""; return; }
            float e = Time.time - _captionStart;
            int i = 0;
            while (i < _chunks.Length - 1 && e > _chunkEnds[i]) i++;
            if (i == _chunk) return;
            _chunk = i;
            _caption.text = _chunks[i];
        }

        // Sentences, and sentences longer than maxChars split at a comma or colon near the middle.
        static string[] Chunk(string text, int maxChars)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var raw in System.Text.RegularExpressions.Regex.Split(text.Trim(), @"(?<=[.!?])\s+"))
            {
                var s = raw.Trim();
                while (s.Length > maxChars)
                {
                    int mid = s.Length / 2, cut = -1;
                    for (int d = 0; d < mid && cut < 0; d++)
                    {
                        foreach (int k in new[] { mid + d, mid - d })
                            if (k > 10 && k < s.Length - 10 && (s[k] == ',' || s[k] == ':' || s[k] == ';')) { cut = k + 1; break; }
                    }
                    if (cut < 0) break;
                    parts.Add(s.Substring(0, cut).Trim());
                    s = s.Substring(cut).Trim();
                }
                if (s.Length > 0) parts.Add(s);
            }
            return parts.ToArray();
        }

        void LateUpdate()
        {
            if (_canvas == null || Camera.main == null) return;
            if (_target > 0.5f) UpdateCaption();
            _shown = Mathf.MoveTowards(_shown, _target, Time.deltaTime / Mathf.Max(0.01f, fadeTime));
            _group.alpha = Mathf.SmoothStep(0f, 1f, _shown);
            _canvas.enabled = _shown > 0.001f;
            if (!_canvas.enabled) return;
            UIKit.LazyFollow(_canvas.transform, Camera.main.transform, distance, elevation, deadzone, followTime, ref _placed, azimuth);
            // scale-in while appearing, and a short pop on every new step so it is noticed
            _pop = Mathf.MoveTowards(_pop, 1f, Time.deltaTime / 0.35f);
            float pop = 1f + 0.06f * Mathf.Sin(_pop * Mathf.PI);
            _canvas.transform.localScale = Vector3.one * (worldWidth / W) * Mathf.Lerp(0.94f, 1f, _group.alpha) * pop;
            _accent.color = new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.55f + 0.45f * Mathf.Sin(_pop * Mathf.PI));
        }

        void Build()
        {
            _canvas = UIKit.WorldCanvas(transform, "StoryCardCanvas", new Vector2(W, H), 7);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            var root = (RectTransform)_canvas.transform;
            _canvas.transform.localScale = Vector3.one * (worldWidth / W);

            var bg = UIKit.Panel("Glass", root, new Color(UIKit.Glass.r, UIKit.Glass.g, UIKit.Glass.b, 0.95f), 46f, 5f);
            UIKit.Stretch(bg.rectTransform);
            // accent bar along the top edge: this card is the one to read
            _accent = UIKit.Panel("Accent", root, UIKit.Accent, 4f, 0f, false);
            UIKit.Place(_accent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(W * 0.4f, 8f));

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
            _title = UIKit.Text("Title", root, font, 34f, UIKit.Accent, TextAlignmentOptions.Bottom, FontStyles.Bold);
            UIKit.Place(_title.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(W - 2f * Pad - 20f, TitleH));
            _title.characterSpacing = 8f;
            _caption = UIKit.Text("Caption", root, font, 42f, UIKit.TextHi, TextAlignmentOptions.Top);
            UIKit.Place(_caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Pad), new Vector2(W - 2f * Pad - 20f, TextH));
            _caption.enableWordWrapping = true;
            _caption.enableAutoSizing = true; _caption.fontSizeMin = 32f; _caption.fontSizeMax = 48f;
            _caption.lineSpacing = 6f;

            SetMedia(null, null);
            _canvas.enabled = false;
        }

        void OnDestroy() { if (_rt != null) _rt.Release(); }
    }
}
