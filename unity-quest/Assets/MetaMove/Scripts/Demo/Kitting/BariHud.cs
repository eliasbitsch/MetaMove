using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MetaMove.Safety;
using MetaMove.UI.Kit;

namespace MetaMove.Demo.Kitting
{
    // Operator HUD for the travel demo, replacing the lab's developer panel.
    //
    //   [ status pill ]   [ speed gauge ]   [ distance ]
    //    line state        ring + percent    metres + zone
    //
    // Sits low in the view and follows the head lazily. Colours carry the same meaning as
    // the floor rings and the signal tower: green clear, amber scaled, red standstill.
    public class BariHud : MonoBehaviour
    {
        public KittingCell cell;
        public ProximitySpeedController speed;
        public TMP_FontAsset font;

        public float distance = 0.95f;
        public float elevation = -24f;   // degrees below the horizon
        public float worldWidth = 0.50f;

        const float W = 980f, H = 190f;
        Canvas _canvas;
        Image _pill, _dot, _gaugeFill, _gaugeTrack, _zoneBar;
        TextMeshProUGUI _state, _detail, _pct, _speedLabel, _dist, _zone;
        bool _placed;
        float _shownFactor, _ssmShown = 1f;
        CanvasGroup _ssmGroup;
        RectTransform _pillRt;

        void Start() => Build();

        void Build()
        {
            _canvas = UIKit.WorldCanvas(transform, "BariHudCanvas", new Vector2(W, H), 6);
            var root = (RectTransform)_canvas.transform;
            _canvas.transform.localScale = Vector3.one * (worldWidth / W);

            // --- status pill (left)
            _pill = UIKit.Panel("StatusPill", root, UIKit.Glass, 44f);
            UIKit.Place(_pill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(380f, 150f));
            _dot = new GameObject("Dot", typeof(Image)).GetComponent<Image>();
            _dot.transform.SetParent(_pill.transform, false);
            _dot.sprite = UIKit.Disc; _dot.raycastTarget = false;
            UIKit.Place(_dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46f, 18f), new Vector2(26f, 26f));
            _state = UIKit.Text("State", _pill.transform, font, 40f, UIKit.TextHi, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            UIKit.Place(_state.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(76f, 18f), new Vector2(290f, 54f));
            _detail = UIKit.Text("Detail", _pill.transform, font, 26f, UIKit.TextLo, TextAlignmentOptions.MidlineLeft);
            UIKit.Place(_detail.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(76f, -30f), new Vector2(290f, 40f));

            _pillRt = _pill.rectTransform;
            // Gauge and distance belong to speed and separation monitoring; while the visitor
            // guides the arm by hand it is not active, so they fade out (one group).
            var ssm = new GameObject("SSM", typeof(RectTransform), typeof(CanvasGroup));
            ssm.transform.SetParent(root, false);
            UIKit.Stretch((RectTransform)ssm.transform);
            _ssmGroup = ssm.GetComponent<CanvasGroup>();

            // --- speed gauge (centre)
            var gauge = UIKit.Panel("Gauge", ssm.transform, UIKit.Glass, 95f);
            UIKit.Place(gauge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 190f));
            _gaugeTrack = Radial("Track", gauge.transform, new Color(1f, 1f, 1f, 0.10f), 1f);
            _gaugeFill = Radial("Fill", gauge.transform, UIKit.Ok, 0f);
            _pct = UIKit.Text("Pct", gauge.transform, font, 54f, UIKit.TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
            UIKit.Place(_pct.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(170f, 64f));
            _speedLabel = UIKit.Text("SpeedLabel", gauge.transform, font, 18f, UIKit.TextLo, TextAlignmentOptions.Center);
            UIKit.Place(_speedLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(120f, 26f));
            _speedLabel.text = "SPEED";
            _speedLabel.characterSpacing = 8f;

            // --- distance (right)
            var dp = UIKit.Panel("Distance", ssm.transform, UIKit.Glass, 44f);
            UIKit.Place(dp.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(380f, 150f));
            _dist = UIKit.Text("Metres", dp.transform, font, 50f, UIKit.TextHi, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            UIKit.Place(_dist.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-36f, 18f), new Vector2(300f, 60f));
            _zone = UIKit.Text("Zone", dp.transform, font, 24f, UIKit.TextLo, TextAlignmentOptions.MidlineRight);
            UIKit.Place(_zone.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-36f, -30f), new Vector2(300f, 36f));
            _zone.characterSpacing = 4f;
            _zoneBar = UIKit.Panel("ZoneBar", dp.transform, UIKit.Ok, 4f, 0f, false);
            UIKit.Place(_zoneBar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(8f, 96f));
        }

        Image Radial(string name, Transform parent, Color c, float fill)
        {
            var img = new GameObject(name, typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = UIKit.Ring; img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360; img.fillOrigin = (int)Image.Origin360.Top; img.fillClockwise = true;
            img.fillAmount = fill; img.color = c; img.raycastTarget = false;
            UIKit.Stretch(img.rectTransform, 10f);
            return img;
        }

        void LateUpdate()
        {
            if (_canvas == null || Camera.main == null) return;
            UIKit.LazyFollow(_canvas.transform, Camera.main.transform, distance, elevation, 18f, 0.25f, ref _placed);

            bool ssmActive = cell == null || (cell.State != KittingCell.Phase.Guide && cell.State != KittingCell.Phase.Align);
            _ssmShown = Mathf.MoveTowards(_ssmShown, ssmActive ? 1f : 0f, Time.deltaTime / 0.25f);
            _ssmGroup.alpha = _ssmShown;
            // the status pill slides to the centre while it is alone
            float x = Mathf.Lerp((380f - W) / 2f, 0f, 1f - _ssmShown);
            _pillRt.anchorMin = _pillRt.anchorMax = new Vector2(0.5f, 0.5f);
            _pillRt.pivot = new Vector2(0.5f, 0.5f);
            _pillRt.anchoredPosition = new Vector2(x, 0f);

            float f = speed != null ? Mathf.Clamp01(speed.Factor) : 1f;
            float d = speed != null ? speed.Distance : -1f;
            float near = speed != null ? speed.dNear : 0.6f, far = speed != null ? speed.dFar : 2f;
            _shownFactor = Mathf.Lerp(_shownFactor, f, 1f - Mathf.Exp(-Time.deltaTime / 0.08f));

            Color zc = d < 0f ? UIKit.TextLo : d <= near ? UIKit.Stop : d <= far ? UIKit.Warn : UIKit.Ok;
            _gaugeFill.fillAmount = _shownFactor;
            _gaugeFill.color = f <= 0.001f ? UIKit.Stop : f < 0.99f ? UIKit.Warn : UIKit.Ok;
            _pct.text = Mathf.RoundToInt(_shownFactor * 100f) + "<size=60%>%</size>";

            _dist.text = d < 0f ? "--" : d.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "<size=60%> m</size>";
            _zone.text = d < 0f ? "" : d <= near ? "STOP ZONE" : d <= far ? "SLOW ZONE" : "CLEAR";
            _zone.color = zc; _zoneBar.color = zc;

            string state, detail; Color sc;
            Describe(f, out state, out detail, out sc);
            _state.text = state; _detail.text = detail; _dot.color = sc;
            // soft pulse on the dot when something needs attention
            if (sc != UIKit.Ok) _dot.color = Color.Lerp(sc, sc * 0.45f, 0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
        }

        void Describe(float f, out string state, out string detail, out Color c)
        {
            if (cell == null || !cell.IsBound) { state = "Place cell"; detail = "Aim at the floor, pinch & hold"; c = UIKit.Accent; return; }
            var tray = cell.Taught;
            string parts = tray != null ? tray.label + " · " + tray.Remaining + " parts" : "";
            switch (cell.State)
            {
                case KittingCell.Phase.Running:
                    if (f <= 0.001f) { state = "Standstill"; detail = "Person in reach"; c = UIKit.Stop; }
                    else if (f < 0.99f) { state = "Running"; detail = "Slowed · " + parts; c = UIKit.Warn; }
                    else { state = "Running"; detail = parts; c = tray != null && tray.Remaining <= 2 ? UIKit.Warn : UIKit.Ok; }
                    break;
                case KittingCell.Phase.Probe: state = "Checking"; detail = "Gripping " + (tray != null ? tray.label : ""); c = UIKit.Warn; break;
                case KittingCell.Phase.Waiting: state = "No parts"; detail = "Bring the next batch"; c = UIKit.Stop; break;
                case KittingCell.Phase.Guide: state = "Hand guiding"; detail = "Speed monitoring paused"; c = UIKit.Accent; break;
                case KittingCell.Phase.Align: state = "Aligning"; detail = "Locating the new tray"; c = UIKit.Accent; break;
                default: state = "Idle"; detail = ""; c = UIKit.TextLo; break;
            }
        }
    }
}
