using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using MetaMove.Safety;
using MetaMove.UI.Kit;

namespace MetaMove.Demo.Kitting
{
    // Shift report: what the visitor did in this run, measured while it happened, shown on a
    // card once the line runs again - and appended to a CSV on the headset, so every demo
    // session is also a data set. In an expert run the previous run is shown alongside.
    //
    //   closest approach, time in the slow and stop zones
    //   production lost to proximity (s of full-speed work, and parts)
    //   time to bring the new batch, to clear the empty, to teach
    //   teach accuracy: how far the pinch was off and the robot corrected on its own
    public class ShiftReport : MonoBehaviour
    {
        public KittingCell cell;
        public ProximitySpeedController speed;
        public TMP_FontAsset font;
        [Tooltip("Seconds after the line runs again before the report comes up (after the closing line).")]
        public float showDelay = 14f, showDelayExpert = 3f;

        public class Run
        {
            public int number; public bool expert;
            public float total, minDist = float.MaxValue, slow, stop, lost, active;
            public int parts;
            public float refill = -1f, clear = -1f, teach = -1f, teachOffset = -1f;
            public float LostParts => parts > 0 && active - lost > 1f ? lost / ((active - lost) / parts) : 0f;
        }

        readonly List<Run> _done = new List<Run>();
        Run _run;
        float _t0, _lowAt = -1f, _missAt = -1f, _guideAt = -1f, _showAt = -1f;
        bool _clearedSeen;

        Canvas _canvas;
        CanvasGroup _group;
        TextMeshProUGUI _title, _sub, _footer;
        readonly List<TextMeshProUGUI[]> _rows = new List<TextMeshProUGUI[]>();
        readonly UIKit.FollowState _follow = new UIKit.FollowState();
        float _shown, _target;

        const float W = 860f, H = 700f, RowH = 64f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        void OnEnable() { if (cell != null) cell.Cue += OnCue; }
        void OnDisable() { if (cell != null) cell.Cue -= OnCue; }

        void Start() => Build();

        void OnCue(string key)
        {
            float now = Time.time;
            switch (key)
            {
                case "restart":
                    _run = new Run { number = _done.Count + 1, expert = cell.Expert };
                    _t0 = now; _lowAt = _missAt = _guideAt = _showAt = -1f; _clearedSeen = false;
                    _target = 0f;
                    break;
                case "low": if (_lowAt < 0f) _lowAt = now; break;
                case "arrived": if (_run != null && _lowAt >= 0f && _run.refill < 0f) _run.refill = now - _lowAt; break;
                case "miss": if (_missAt < 0f) _missAt = now; break;
                case "guide": if (_guideAt < 0f) _guideAt = now; break;
                case "teach":
                    if (_run != null && _guideAt >= 0f && _run.teach < 0f) { _run.teach = now - _guideAt; _run.teachOffset = cell.TeachOffset; }
                    break;
                case "resume":
                    if (_run != null && _showAt < 0f) _showAt = now + (cell.Expert ? showDelayExpert : showDelay);
                    break;
            }
        }

        void Update()
        {
            if (cell == null || _run == null || !cell.IsBound) return;

            if (_showAt < 0f)   // still measuring
            {
                float dt = Time.deltaTime;
                float f = speed != null ? Mathf.Clamp01(speed.Factor) : 1f;
                float d = speed != null ? speed.Distance : -1f;
                if (d >= 0f) _run.minDist = Mathf.Min(_run.minDist, d);
                bool moving = cell.State == KittingCell.Phase.Running || cell.State == KittingCell.Phase.Probe;
                if (moving)
                {
                    _run.active += dt;
                    _run.lost += (1f - f) * dt;
                    if (f <= 0.001f) _run.stop += dt; else if (f < 0.99f) _run.slow += dt;
                }
                if (!_clearedSeen && _missAt >= 0f && cell.TrayACleared) { _clearedSeen = true; _run.clear = Time.time - _missAt; }
            }
            else if (_target < 0.5f && Time.time >= _showAt)
            {
                Finish();
            }

            // card
            if (_canvas == null || Camera.main == null) return;
            _shown = Mathf.MoveTowards(_shown, _target, Time.deltaTime / 0.3f);
            _group.alpha = Mathf.SmoothStep(0f, 1f, _shown);
            _canvas.enabled = _shown > 0.001f;
            if (_canvas.enabled)
                UIKit.LazyFollow(_canvas.transform, Camera.main.transform, 1.1f, 6f, 16f, 0.35f, _follow);
        }

        void Finish()
        {
            _run.total = _showAt - _t0 - (cell.Expert ? showDelayExpert : showDelay);
            _run.parts = cell.PartsPlaced;
            if (_run.minDist == float.MaxValue) _run.minDist = -1f;
            var prev = _done.Count > 0 ? _done[_done.Count - 1] : null;
            _done.Add(_run);
            Fill(_run, prev);
            Log(_run);
            _follow.placed = false;
            _target = 1f;
            Debug.Log("[Report] run " + _run.number + (_run.expert ? " expert" : "") + " total=" + _run.total.ToString("0.0", Inv) +
                      " min=" + _run.minDist.ToString("0.00", Inv) + " lost=" + _run.lost.ToString("0.0", Inv) +
                      " teach=" + _run.teach.ToString("0.0", Inv) + " off=" + _run.teachOffset.ToString("0.000", Inv));
        }

        // ---------------------------------------------------------------- text

        static string S(float s) => s < 0f ? "--" : s >= 60f ? ((int)(s / 60f)) + ":" + ((int)(s % 60f)).ToString("00") + " min" : s.ToString("0", Inv) + " s";
        static string M(float m) => m < 0f ? "--" : m.ToString("0.00", Inv) + " m";

        // value, plus the previous run small and grey when there is one
        static string V(string now, string before) =>
            before == null ? now : now + "<size=62%><color=#8C99A6>   was " + before + "</color></size>";

        void Fill(Run r, Run p)
        {
            _title.text = "SHIFT REPORT";
            _sub.text = "Run " + r.number + (r.expert ? "  ·  expert, no hints" : "  ·  guided");
            var lines = new[]
            {
                new[] { "Shift completed in", V(S(r.total), p != null ? S(p.total) : null) },
                new[] { "Closest to the arm", V(M(r.minDist), p != null ? M(p.minDist) : null) },
                new[] { "Arm slowed / stopped", V(S(r.slow) + " / " + S(r.stop), p != null ? S(p.slow) + " / " + S(p.stop) : null) },
                new[] { "Production lost to proximity", V(S(r.lost) + "  ~ " + r.LostParts.ToString("0.0", Inv) + " parts", p != null ? S(p.lost) : null) },
                new[] { "New batch brought in", V(S(r.refill), p != null ? S(p.refill) : null) },
                new[] { "Empty box cleared", V(S(r.clear), p != null ? S(p.clear) : null) },
                new[] { "Robot taught in", V(S(r.teach), p != null ? S(p.teach) : null) },
                new[] { "Your pinch was off by", V(r.teachOffset < 0f ? "--" : (r.teachOffset * 100f).ToString("0.0", Inv) + " cm, the robot corrected it",
                                                   p != null && p.teachOffset >= 0f ? (p.teachOffset * 100f).ToString("0.0", Inv) + " cm" : null) },
            };
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i][0].text = i < lines.Length ? lines[i][0] : "";
                _rows[i][1].text = i < lines.Length ? lines[i][1] : "";
            }
            _footer.text = r.expert || p != null
                ? "Logged as run " + r.number + ". Same task, second time: what stayed?"
                : "Logged as run " + r.number + ". Try it again in expert mode: hand menu, Expert.";
        }

        void Build()
        {
            _canvas = UIKit.WorldCanvas(transform, "ShiftReportCanvas", new Vector2(W, H), 7);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _canvas.transform.localScale = Vector3.one * (0.58f / W);
            var root = (RectTransform)_canvas.transform;
            var bg = UIKit.Panel("Glass", root, new Color(UIKit.Glass.r, UIKit.Glass.g, UIKit.Glass.b, 0.96f), 46f, 5f);
            UIKit.Stretch(bg.rectTransform);
            var accent = UIKit.Panel("Accent", root, UIKit.Accent, 4f, 0f, false);
            UIKit.Place(accent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(W * 0.4f, 8f));

            _title = UIKit.Text("Title", root, font, 40f, UIKit.Accent, TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -34f), new Vector2(W - 88f, 50f));
            _title.characterSpacing = 8f;
            _sub = UIKit.Text("Sub", root, font, 26f, UIKit.TextLo, TextAlignmentOptions.Left);
            UIKit.Place(_sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -86f), new Vector2(W - 88f, 36f));

            float y = -140f;
            for (int i = 0; i < 8; i++)
            {
                if (i > 0)
                {
                    var line = UIKit.Panel("Rule", root, new Color(1f, 1f, 1f, 0.06f), 1f, 0f, false);
                    UIKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(W - 88f, 2f));
                }
                var l = UIKit.Text("Label", root, font, 26f, UIKit.TextLo, TextAlignmentOptions.MidlineLeft);
                UIKit.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(44f, y - RowH / 2f), new Vector2(330f, RowH));
                var v = UIKit.Text("Value", root, font, 30f, UIKit.TextHi, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                UIKit.Place(v.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-44f, y - RowH / 2f), new Vector2(W - 88f - 340f, RowH));
                v.enableWordWrapping = true; v.enableAutoSizing = true; v.fontSizeMin = 20f; v.fontSizeMax = 30f;
                _rows.Add(new[] { l, v });
                y -= RowH;
            }
            _footer = UIKit.Text("Footer", root, font, 24f, UIKit.Accent, TextAlignmentOptions.Center);
            UIKit.Place(_footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(W - 88f, 40f));
            _canvas.enabled = false;
        }

        // ---------------------------------------------------------------- CSV

        void Log(Run r)
        {
            try
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, "metamove_runs.csv");
                bool fresh = !System.IO.File.Exists(path);
                using (var w = new System.IO.StreamWriter(path, true))
                {
                    if (fresh)
                        w.WriteLine("timestamp,run,expert,total_s,min_dist_m,slow_s,stop_s,lost_s,lost_parts,parts,refill_s,clear_s,teach_s,teach_offset_m");
                    w.WriteLine(string.Join(",", new[]
                    {
                        System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", Inv), r.number.ToString(Inv), r.expert ? "1" : "0",
                        F(r.total), F(r.minDist), F(r.slow), F(r.stop), F(r.lost), F(r.LostParts), r.parts.ToString(Inv),
                        F(r.refill), F(r.clear), F(r.teach), r.teachOffset.ToString("0.000", Inv),
                    }));
                }
                Debug.Log("[Report] logged to " + path);
            }
            catch (System.Exception e) { Debug.LogWarning("[Report] could not write the run log: " + e.Message); }
        }

        static string F(float v) => v.ToString("0.0", Inv);
    }
}
