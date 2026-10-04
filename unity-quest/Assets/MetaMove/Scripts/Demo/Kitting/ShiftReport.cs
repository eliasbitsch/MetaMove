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
    // plus two plots (RunPlots): speed and distance over the shift, and the visitor's path
    // from above - also saved on the headset as PNG and as a CSV trace.
    public class ShiftReport : MonoBehaviour
    {
        public KittingCell cell;
        public ProximitySpeedController speed;
        public TMP_FontAsset font;
        [Tooltip("Spoken report lines (keys report_*), assembled per run. Clips from narration.txt.")]
        public List<StoryDirector.Beat> lines = new List<StoryDirector.Beat>();
        [Tooltip("Pinch offset (m) below which it counts as spot on, and above which as rough.")]
        public float pinchExact = 0.03f, pinchRough = 0.08f;
        [Tooltip("Seconds from 'show the box' to Teach above which the visitor took their time.")]
        public float teachSlow = 25f;
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
        readonly List<RunPlots.Sample> _trace = new List<RunPlots.Sample>();
        readonly List<RunPlots.Mark> _marks = new List<RunPlots.Mark>();
        readonly List<float> _markX = new List<float>();
        float _nextSample;
        bool _stopMarked;
        UnityEngine.UI.RawImage _timeline, _path;
        readonly List<TextMeshProUGUI> _markLabels = new List<TextMeshProUGUI>();
        TextMeshProUGUI _tEnd, _zoneStop, _zoneSlow, _zoneFree, _ringStop, _ringSlow;
        // plot column on the card: aligned with the number rows, timeline on top, path below
        const float PlotX = 892f, PlotW = 564f, TlH = 180f, TlY = -170f, PathS = 224f, PathY = -428f;
        const float MaxDist = 3f, Split = 0.62f;   // timeline: distance axis 0..MaxDist m in the lower Split
        Run _run;
        float _t0, _lowAt = -1f, _missAt = -1f, _guideAt = -1f, _showAt = -1f;
        bool _clearedSeen;

        Canvas _canvas;
        CanvasGroup _group;
        TextMeshProUGUI _title, _sub, _footer;
        readonly List<TextMeshProUGUI[]> _rows = new List<TextMeshProUGUI[]>();
        readonly List<UnityEngine.UI.Image> _rowHl = new List<UnityEngine.UI.Image>();
        readonly float[] _hlAmount = new float[8];
        int[] _hl = new int[0];   // rows the voice is talking about right now

        // which card rows each spoken line is about
        static int[] RowsFor(string key)
        {
            switch (key)
            {
                case "report_far": case "report_slowed": case "report_stopped": return new[] { 1, 2, 3 };
                case "report_pinch_exact": case "report_pinch_close": case "report_pinch_rough": return new[] { 7 };
                case "report_teach_slow": case "report_teach_fast": return new[] { 6 };
                case "report_outro": return new[] { 0, 3 };
                case "report_outro_expert": return new[] { 0, 4, 5, 6 };
                default: return new int[0];
            }
        }
        readonly UIKit.FollowState _follow = new UIKit.FollowState();
        float _shown, _target;

        const float W = 1500f, H = 760f, RowH = 64f, ColW = 860f;   // numbers left (ColW), plots right
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        void OnEnable() { if (cell != null) cell.Cue += OnCue; }
        void OnDisable() { if (cell != null) cell.Cue -= OnCue; }

        AudioSource _voice;
        [Tooltip("The story's narrator: the report never talks over it.")]
        public AudioSource storyVoice;
        // True while the report is being read out; the story holds its next line meanwhile.
        public static bool Speaking { get; private set; }

        void Start()
        {
            Build();
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0f;
        }

        void OnCue(string key)
        {
            float now = Time.time;
            switch (key)
            {
                case "restart":
                    _run = new Run { number = _done.Count + 1, expert = cell.Expert };
                    _t0 = now; _lowAt = _missAt = _guideAt = _showAt = -1f; _clearedSeen = false;
                    _trace.Clear(); _marks.Clear(); _nextSample = 0f; _stopMarked = false;
                    _target = 0f;
                    StopAllCoroutines();
                    Speaking = false;
                    if (_voice != null) _voice.Stop();
                    _hl = new int[0];
                    break;
                case "low": if (_lowAt < 0f) { _lowAt = now; Mark("Refill asked"); } break;
                case "arrived": if (_run != null && _lowAt >= 0f && _run.refill < 0f) { _run.refill = now - _lowAt; Mark("Box in"); } break;
                case "miss": if (_missAt < 0f) _missAt = now; break;
                case "guide": if (_guideAt < 0f) _guideAt = now; break;
                case "teach":
                    if (_run != null && _guideAt >= 0f && _run.teach < 0f) { _run.teach = now - _guideAt; _run.teachOffset = cell.TeachOffset; Mark("Teach"); }
                    break;
                case "resume":
                    if (_run != null && _showAt < 0f) _showAt = now + (cell.Expert ? showDelayExpert : showDelay);
                    break;
            }
        }

        void Mark(string label) { if (_run != null) _marks.Add(new RunPlots.Mark { t = Time.time - _t0, label = label }); }

        void Sample(float f, float d)
        {
            if (Time.time < _nextSample || Camera.main == null || cell.cellFrame == null) return;
            _nextSample = Time.time + 0.2f;
            var fr = cell.cellFrame;
            var l = Quaternion.Inverse(fr.rotation) * (Camera.main.transform.position - fr.position);
            _trace.Add(new RunPlots.Sample { t = Time.time - _t0, dist = d, factor = f, x = l.x, z = l.z });
            if (!_stopMarked && f <= 0.001f && cell.State == KittingCell.Phase.Running) { _stopMarked = true; Mark("Stop"); }
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
                Sample(f, d);
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
            {
                UIKit.LazyFollow(_canvas.transform, Camera.main.transform, 1.1f, 6f, 16f, 0.35f, _follow);
                for (int i = 0; i < _rows.Count; i++)
                {
                    bool on = System.Array.IndexOf(_hl, i) >= 0;
                    _hlAmount[i] = Mathf.MoveTowards(_hlAmount[i], on ? 1f : 0f, Time.deltaTime / 0.25f);
                    float h = Mathf.SmoothStep(0f, 1f, _hlAmount[i]);
                    _rowHl[i].color = new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.16f * h);
                    _rows[i][0].color = Color.Lerp(UIKit.TextLo, UIKit.Accent, h);
                    _rows[i][1].rectTransform.localScale = Vector3.one * (1f + 0.07f * h);
                }
            }
        }

        void Finish()
        {
            _run.total = _showAt - _t0 - (cell.Expert ? showDelayExpert : showDelay);
            _run.parts = cell.PartsPlaced;
            if (_run.minDist == float.MaxValue) _run.minDist = -1f;
            var prev = _done.Count > 0 ? _done[_done.Count - 1] : null;
            _done.Add(_run);
            Fill(_run, prev);
            Plots(_run);
            Log(_run);
            _follow.placed = false;
            _target = 1f;
            StartCoroutine(Speak(Verdict(_run, prev)));
            Debug.Log("[Report] run " + _run.number + (_run.expert ? " expert" : "") + " total=" + _run.total.ToString("0.0", Inv) +
                      " min=" + _run.minDist.ToString("0.00", Inv) + " lost=" + _run.lost.ToString("0.0", Inv) +
                      " teach=" + _run.teach.ToString("0.0", Inv) + " off=" + _run.teachOffset.ToString("0.000", Inv));
        }

        // ---------------------------------------------------------------- spoken verdict

        List<string> Verdict(Run r, Run p)
        {
            var k = new List<string> { "report_intro" };
            k.Add(r.stop > 0.5f ? "report_stopped" : r.slow > 1f ? "report_slowed" : "report_far");
            if (r.teachOffset >= 0f)
                k.Add(r.teachOffset < pinchExact ? "report_pinch_exact" : r.teachOffset > pinchRough ? "report_pinch_rough" : "report_pinch_close");
            if (r.teach >= 0f) k.Add(r.teach > teachSlow ? "report_teach_slow" : "report_teach_fast");
            k.Add(r.expert || p != null ? "report_outro_expert" : "report_outro");
            return k;
        }

        System.Collections.IEnumerator Speak(List<string> keys)
        {
            yield return new WaitForSeconds(1.0f);   // let the card land first
            Speaking = true;
            foreach (var key in keys)
            {
                while (storyVoice != null && storyVoice.isPlaying) yield return null;   // one voice at a time
                var line = lines.Find(b => b.key == key);
                if (line == null || line.voice == null) continue;
                _voice.clip = line.voice;
                _voice.Play();
                _hl = RowsFor(key);
                Debug.Log("[Report] says " + key);
                yield return new WaitForSeconds(line.voice.length + 1.2f);
            }
            _hl = new int[0];
            Speaking = false;
        }

        void OnDestroy() => Speaking = false;

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
            _canvas.transform.localScale = Vector3.one * (0.58f / 860f);   // same text size as before; the card grew to the right
            var root = (RectTransform)_canvas.transform;
            var bg = UIKit.Panel("Glass", root, new Color(UIKit.Glass.r, UIKit.Glass.g, UIKit.Glass.b, 0.96f), 46f, 5f);
            UIKit.Stretch(bg.rectTransform);
            var accent = UIKit.Panel("Accent", root, UIKit.Accent, 4f, 0f, false);
            UIKit.Place(accent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(W * 0.4f, 8f));

            _title = UIKit.Text("Title", root, font, 40f, UIKit.Accent, TextAlignmentOptions.Left, FontStyles.Bold);
            UIKit.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -34f), new Vector2(ColW - 88f, 50f));
            _title.characterSpacing = 8f;
            _sub = UIKit.Text("Sub", root, font, 26f, UIKit.TextLo, TextAlignmentOptions.Left);
            UIKit.Place(_sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -86f), new Vector2(ColW - 88f, 36f));

            float y = -140f;
            for (int i = 0; i < 8; i++)
            {
                if (i > 0)
                {
                    var line = UIKit.Panel("Rule", root, new Color(1f, 1f, 1f, 0.06f), 1f, 0f, false);
                    UIKit.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(44f, y), new Vector2(ColW - 88f, 2f));
                }
                var hl = UIKit.Panel("Highlight", root, new Color(0f, 0f, 0f, 0f), 14f, 0f, false);
                UIKit.Place(hl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, y - RowH / 2f), new Vector2(ColW - 56f, RowH - 6f));
                _rowHl.Add(hl);
                var l = UIKit.Text("Label", root, font, 26f, UIKit.TextLo, TextAlignmentOptions.MidlineLeft);
                UIKit.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(44f, y - RowH / 2f), new Vector2(330f, RowH));
                var v = UIKit.Text("Value", root, font, 30f, UIKit.TextHi, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
                UIKit.Place(v.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(ColW - 44f, y - RowH / 2f), new Vector2(ColW - 88f - 340f, RowH));
                v.enableWordWrapping = true; v.enableAutoSizing = true; v.fontSizeMin = 20f; v.fontSizeMax = 30f;
                _rows.Add(new[] { l, v });
                y -= RowH;
            }
            BuildPlots(root);
            _footer = UIKit.Text("Footer", root, font, 24f, UIKit.Accent, TextAlignmentOptions.Center);
            UIKit.Place(_footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(W - 88f, 40f));
            _canvas.enabled = false;
        }

        // ---------------------------------------------------------------- plots

        void BuildPlots(RectTransform root)
        {
            var panel = UIKit.Panel("PlotPanel", root, new Color(1f, 1f, 1f, 0.035f), 24f, 0f, false);
            UIKit.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(PlotX - 18f, -126f), new Vector2(PlotW + 36f, 540f));

            // shared colour key, top right next to the title
            Legend(root, UIKit.Disc, RunPlots.SpeedColour(1f), "full speed", new Vector2(W - 44f - 3 * 150f, -58f), 14f);
            Legend(root, UIKit.Disc, RunPlots.SpeedColour(0.25f), "slowed", new Vector2(W - 44f - 2 * 150f + 10f, -58f), 14f);
            Legend(root, UIKit.Disc, RunPlots.SpeedColour(0f), "stopped", new Vector2(W - 44f - 150f + 10f, -58f), 14f);

            Caption(root, "ARM SPEED  \u00b7  YOUR DISTANCE", new Vector2(PlotX, -140f));
            _timeline = Img("Timeline", root, new Vector2(PlotX, TlY), new Vector2(PlotW, TlH));
            // zone names inside the distance bands (heights set per run, the zones are configurable)
            _zoneStop = Tag(_timeline.transform, RunPlots.SpeedColour(0f));
            _zoneSlow = Tag(_timeline.transform, RunPlots.SpeedColour(0.25f));
            _zoneFree = Tag(_timeline.transform, RunPlots.SpeedColour(1f));
            // time axis and the story's moments under the plot
            var t0 = UIKit.Text("T0", _timeline.transform, font, 16f, UIKit.TextLo, TextAlignmentOptions.TopLeft);
            UIKit.Place(t0.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, -4f), new Vector2(60f, 20f));
            t0.text = "0:00";
            _tEnd = UIKit.Text("TEnd", _timeline.transform, font, 16f, UIKit.TextLo, TextAlignmentOptions.TopRight);
            UIKit.Place(_tEnd.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, -4f), new Vector2(80f, 20f));
            for (int i = 0; i < 6; i++)
            {
                var m = UIKit.Text("Mark", _timeline.transform, font, 16f, UIKit.Accent, TextAlignmentOptions.Top);
                UIKit.Place(m.rectTransform, Vector2.zero, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(130f, 20f));
                m.gameObject.SetActive(false);
                _markLabels.Add(m);
            }

            Caption(root, "YOUR WALK, FROM ABOVE  ·  COLOURED BY ARM SPEED", new Vector2(PlotX, PathY + 30f));
            _path = Img("Path", root, new Vector2(PlotX, PathY), new Vector2(PathS, PathS));
            float lx = PlotX + PathS + 28f, ly = PathY - 40f;
            Legend(root, UIKit.Disc, RunPlots.ArmGrey, "the arm", new Vector2(lx, ly), 14f);
            Legend(root, UIKit.Disc, RunPlots.White, "you, at the start", new Vector2(lx, ly - 34f), 12f);
            Legend(root, UIKit.Disc, RunPlots.Cyan, "you, at the end", new Vector2(lx, ly - 68f), 12f);
            _ringStop = Legend(root, UIKit.Ring, RunPlots.SpeedColour(0f), "", new Vector2(lx, ly - 102f), 16f);
            _ringSlow = Legend(root, UIKit.Ring, RunPlots.SpeedColour(0.25f), "", new Vector2(lx, ly - 136f), 16f);
        }

        void Caption(RectTransform root, string text, Vector2 pos)
        {
            var t = UIKit.Text("Caption", root, font, 19f, UIKit.TextLo, TextAlignmentOptions.Left);
            UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(PlotW, 26f));
            t.text = text;
            t.characterSpacing = 4f;
        }

        TextMeshProUGUI Tag(Transform parent, Color c)
        {
            var t = UIKit.Text("Zone", parent, font, 15f, new Color(c.r, c.g, c.b, 0.95f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.Place(t.rectTransform, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(200f, 18f));
            t.characterSpacing = 3f;
            return t;
        }

        // a coloured mark (dot or ring) and its words; returns the words for later text
        TextMeshProUGUI Legend(RectTransform root, Sprite sprite, Color c, string text, Vector2 pos, float size)
        {
            var dot = new GameObject("Key", typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            dot.transform.SetParent(root, false);
            dot.sprite = sprite; dot.color = c; dot.raycastTarget = false;
            UIKit.Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(pos.x + 8f, pos.y), new Vector2(size, size));
            var t = UIKit.Text("KeyText", root, font, 18f, UIKit.TextLo, TextAlignmentOptions.MidlineLeft);
            UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(pos.x + 24f, pos.y), new Vector2(220f, 24f));
            t.text = text;
            return t;
        }

        UnityEngine.UI.RawImage Img(string name, RectTransform root, Vector2 pos, Vector2 size)
        {
            var img = new GameObject(name, typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
            img.transform.SetParent(root, false);
            img.raycastTarget = false;
            UIKit.Place(img.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            img.color = new Color(1f, 1f, 1f, 0f);
            return img;
        }

        void Plots(Run r, bool save = true)
        {
            float near = speed != null ? speed.dNear : 0.6f, far = speed != null ? speed.dFar : 2f;
            try
            {
                Free(_timeline.texture); Free(_path.texture);
                var tl = RunPlots.Timeline(_trace, _marks, near, far, MaxDist, 1128, 420, _markX);
                var pa = RunPlots.Path(_trace, near, far, 600);
                _timeline.texture = tl; _timeline.color = Color.white;
                _path.texture = pa; _path.color = Color.white;

                float total = _trace.Count > 0 ? _trace[_trace.Count - 1].t : 0f;
                _tEnd.text = ((int)(total / 60f)) + ":" + ((int)(total % 60f)).ToString("00");
                // band centres in plot units: the distance strip is the lower Split of the image
                float strip = TlH * Split - 3f;
                System.Func<float, float> Y = m => Mathf.Clamp01(m / MaxDist) * strip;
                _zoneStop.text = "STOP  < " + near.ToString("0.0", Inv) + " m";
                _zoneSlow.text = "SLOWED  < " + far.ToString("0.0", Inv) + " m";
                _zoneFree.text = "FREE";
                _zoneStop.rectTransform.anchoredPosition = new Vector2(8f, Y(near) / 2f);
                _zoneSlow.rectTransform.anchoredPosition = new Vector2(8f, (Y(near) + Y(far)) / 2f);
                _zoneFree.rectTransform.anchoredPosition = new Vector2(8f, (Y(far) + strip) / 2f);
                _ringStop.text = "stop zone, " + near.ToString("0.0", Inv) + " m";
                _ringSlow.text = "slow zone, " + far.ToString("0.0", Inv) + " m";

                for (int i = 0; i < _markLabels.Count; i++)
                {
                    bool on = i < _marks.Count;
                    _markLabels[i].gameObject.SetActive(on);
                    if (!on) continue;
                    _markLabels[i].text = _marks[i].label;
                    // clear of the time labels at both ends; neighbours alternate between two lines
                    float x = Mathf.Clamp(_markX[i] * PlotW, 105f, PlotW - 105f);
                    _markLabels[i].rectTransform.anchoredPosition = new Vector2(x, -4f - (i % 2) * 20f);
                }
                if (!save) return;
                string dir = Application.persistentDataPath, stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "run" + r.number + "_" + stamp + "_timeline.png"), tl.EncodeToPNG());
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "run" + r.number + "_" + stamp + "_path.png"), pa.EncodeToPNG());
                using (var w = new System.IO.StreamWriter(System.IO.Path.Combine(dir, "run" + r.number + "_" + stamp + "_trace.csv")))
                {
                    w.WriteLine("t_s,distance_m,speed_factor,head_x_m,head_z_m");
                    foreach (var p in _trace)
                        w.WriteLine(F2(p.t) + "," + F2(p.dist) + "," + F2(p.factor) + "," + F2(p.x) + "," + F2(p.z));
                    foreach (var m in _marks) w.WriteLine("# " + F2(m.t) + " " + m.label);
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Report] plots failed: " + e.Message); }
        }

        static void Free(Texture t)
        {
            if (t == null) return;
            if (Application.isPlaying) Destroy(t); else DestroyImmediate(t);
        }

        // Editor preview: the card for a made-up run, fully shown, nothing saved.
        public Canvas PreviewCard(Run r, Run prev, List<RunPlots.Sample> trace, List<RunPlots.Mark> marks)
        {
            if (_canvas == null) Build();
            _trace.Clear(); _trace.AddRange(trace);
            _marks.Clear(); _marks.AddRange(marks);
            Fill(r, prev);
            Plots(r, false);
            _canvas.enabled = true;
            _group.alpha = 1f;
            return _canvas;
        }

        static string F2(float v) => v.ToString("0.00", Inv);

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
