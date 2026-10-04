using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // Less at once: parts of the cell appear when the story talks about them - the floor
    // zones with the safety layer, the QR post with the tracking, trolley and orange box with
    // the refill, the reserve box when the arm switches to it. Each pops in with a short
    // overshoot. Fallbacks make sure nothing the visitor needs stays hidden (refill due,
    // reserve in use, a minute gone), and an expert run shows everything from the start.
    public class StagedReveal : MonoBehaviour
    {
        [Serializable]
        public class Group
        {
            [Tooltip("Story beat or cell cue that reveals the group.")]
            public string key;
            public List<GameObject> objects = new List<GameObject>();
            [NonSerialized] public bool shown;
            [NonSerialized] public float t;
        }

        public KittingCell cell;
        public StoryDirector story;
        public List<Group> groups = new List<Group>();
        public float popTime = 0.6f;
        [Tooltip("Everything is shown at the latest this long after the cell was placed.")]
        public float showAllAfter = 75f;

        float _since = -1f;

        void OnEnable()
        {
            if (cell != null) cell.Cue += OnKey;
            if (story != null) story.BeatStarted += OnKey;
        }

        void OnDisable()
        {
            if (cell != null) cell.Cue -= OnKey;
            if (story != null) story.BeatStarted -= OnKey;
        }

        void OnKey(string key)
        {
            if (key == "restart") { HideAll(); return; }
            foreach (var g in groups) if (g.key == key) Show(g);
        }

        void HideAll()
        {
            _since = Time.time;
            bool expert = cell != null && cell.Expert;
            foreach (var g in groups)
            {
                g.shown = expert; g.t = expert ? 1f : 0f;
                foreach (var o in g.objects) if (o != null) { o.SetActive(expert); o.transform.localScale = Vector3.one; }
            }
        }

        void Show(Group g)
        {
            if (g.shown) return;
            g.shown = true; g.t = 0f;
            foreach (var o in g.objects) if (o != null) { o.transform.localScale = Vector3.one * 0.001f; o.SetActive(true); }
        }

        void Update()
        {
            if (cell == null || !cell.IsBound || _since < 0f) return;
            // fallbacks: never hide what the visitor needs right now
            if (cell.RefillDue) Show("refill");
            if (cell.Taught == cell.reserve) Show("reserve");
            if (Time.time - _since > showAllAfter) foreach (var g in groups) Show(g);

            foreach (var g in groups)
            {
                if (!g.shown || g.t >= 1f) continue;
                g.t = Mathf.Min(1f, g.t + Time.deltaTime / popTime);
                float s = EaseOutBack(g.t);
                foreach (var o in g.objects) if (o != null) o.transform.localScale = Vector3.one * Mathf.Max(0.001f, s);
            }
        }

        void Show(string key) { foreach (var g in groups) if (g.key == key) Show(g); }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.2f, c3 = c1 + 1f;
            float y = x - 1f;
            return 1f + c3 * y * y * y + c1 * y * y;
        }
    }
}
