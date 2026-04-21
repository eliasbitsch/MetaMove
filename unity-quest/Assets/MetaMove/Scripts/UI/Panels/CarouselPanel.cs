using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;

namespace MetaMove.UI.Panels
{
    // Tutorial / onboarding panel. Hosts a list of "slides" (text + optional video)
    // and lets the user flip through them by swiping horizontally with the non-dominant
    // hand (fingertip velocity across the panel's local X axis). Also supports arrow
    // IconButtons and external OnNext()/OnPrev() hooks.
    //
    // Inherits WorldPanelBase so it's grabbable (translate-only) and fits the L2 panel
    // system — the radial's "Tutorial" wedge can open it via PanelManager.
    public class CarouselPanel : WorldPanelBase
    {
        [System.Serializable]
        public struct Slide
        {
            public string title;
            [TextArea(2, 8)] public string body;
            public Sprite image;                  // optional
            public VideoClip videoClip;           // optional
        }

        public Slide[] slides;

        [Header("UI")]
        public TMP_Text titleText;
        public TMP_Text bodyText;
        public Image imageView;                   // shown when slide.image != null
        public VideoPlayer videoPlayer;           // shown when slide.videoClip != null
        public RawImage videoRawImage;            // target for videoPlayer output
        public Button prevButton;
        public Button nextButton;
        public Transform dotStrip;                // container for index-dot UI
        public Image dotPrefab;                   // one dot per slide — instantiated into dotStrip

        [Header("Events")]
        public UnityEvent<int> onSlideChanged;

        int _index;
        readonly List<Image> _dots = new();

        protected void Start()
        {
            if (prevButton != null) prevButton.onClick.AddListener(OnPrev);
            if (nextButton != null) nextButton.onClick.AddListener(OnNext);
            BuildDots();
            Show(0);
        }

        public void OnNext() => Show(_index + 1);
        public void OnPrev() => Show(_index - 1);

        public void Show(int index)
        {
            if (slides == null || slides.Length == 0) return;
            _index = ((index % slides.Length) + slides.Length) % slides.Length;
            var s = slides[_index];
            if (titleText != null) titleText.text = s.title;
            if (bodyText != null) bodyText.text = s.body;

            bool hasImage = s.image != null;
            if (imageView != null) { imageView.enabled = hasImage; imageView.sprite = s.image; }

            bool hasVideo = s.videoClip != null;
            if (videoPlayer != null)
            {
                if (hasVideo) { videoPlayer.clip = s.videoClip; videoPlayer.Play(); }
                else videoPlayer.Stop();
            }
            if (videoRawImage != null) videoRawImage.enabled = hasVideo;

            for (int i = 0; i < _dots.Count; i++)
                _dots[i].color = i == _index ? Color.white : new Color(1f, 1f, 1f, 0.35f);

            onSlideChanged?.Invoke(_index);
        }

        void BuildDots()
        {
            if (dotStrip == null || dotPrefab == null || slides == null) return;
            foreach (Transform c in dotStrip) Destroy(c.gameObject);
            _dots.Clear();
            for (int i = 0; i < slides.Length; i++)
            {
                var d = Instantiate(dotPrefab, dotStrip);
                d.gameObject.SetActive(true);
                _dots.Add(d);
            }
        }

        // Swipe behaviour falls through to WorldPanelBase.HandleSwipe — it steps to the
        // neighbouring radial wedge, same as every other panel. Slide navigation within
        // the carousel is handled via prev/next buttons only (less ambiguity when the
        // carousel sits above the radial as a live preview of the current wedge).
    }
}
