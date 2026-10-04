using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MetaMove.Safety;
using MetaMove.UI.Kit;

namespace MetaMove.Demo.Kitting
{
    // Operator HUD for the travel demo, replacing the lab's developer panel.
    //
    //   [ speed gauge ] [ distance ]
    //    ring + percent  metres + zone
    //
    // Deliberately little: no line-state text, the story card says what happens.
    // With a 'mount' it is a fixed panel of the cell (on a stand beside the robot, like the
    // operator panel of a real cell), swivelling to face the viewer, and never in the way; without one it sits low in the
    // view and follows the head lazily. Colours carry the same meaning as
    // the floor rings and the signal tower: green clear, amber scaled, red standstill.
    public class BariHud : MonoBehaviour
    {
        public KittingCell cell;
        public ProximitySpeedController speed;
        public TMP_FontAsset font;

        public float distance = 0.95f;
        public float elevation = -33f;   // degrees below the horizon, under the story card
        public float worldWidth = 0.306f;   // head-following: same element size as the wider HUD with the status pill (0.50 m over 980)
        [Tooltip("Fixed place in the cell. Empty: follows the head.")]
        public Transform mount;
        public float mountWidth = 0.90f;   // on the stand: readable from the visitor's spot, ~2.5 m

        const float W = 600f, H = 230f, GaugeX = -203f, DistX = 105f;
        Canvas _canvas;
        Image _gaugeFill, _gaugeTrack, _zoneBar;
        TextMeshProUGUI _pct, _speedLabel, _dist, _zone;
        readonly UIKit.FollowState _follow = new UIKit.FollowState();
        float _shownFactor, _ssmShown = 1f;
        CanvasGroup _ssmGroup;

        void Start() => Build();

        void Build()
        {
            _canvas = UIKit.WorldCanvas(transform, "BariHudCanvas", new Vector2(W, H), 6);
            var root = (RectTransform)_canvas.transform;
            _canvas.transform.localScale = Vector3.one * (worldWidth / W);
            if (mount != null)
            {
                _canvas.transform.SetParent(mount, false);
                _canvas.transform.localPosition = Vector3.zero;
                _canvas.transform.localRotation = Quaternion.identity;
                _canvas.transform.localScale = Vector3.one * (mountWidth / W);
                // a housing behind both read-outs, so it reads as one panel of the cell
                var housing = UIKit.Panel("Housing", root, new Color(0.10f, 0.11f, 0.13f, 1f), 48f, 4f);
                UIKit.Stretch(housing.rectTransform, -18f);
                housing.transform.SetAsFirstSibling();
            }

            // Gauge and distance belong to speed and separation monitoring; while the visitor
            // guides the arm by hand it is not active, so they fade out (one group).
            var ssm = new GameObject("SSM", typeof(RectTransform), typeof(CanvasGroup));
            ssm.transform.SetParent(root, false);
            UIKit.Stretch((RectTransform)ssm.transform);
            _ssmGroup = ssm.GetComponent<CanvasGroup>();

            // --- speed gauge (centre)
            var gauge = UIKit.Panel("Gauge", ssm.transform, UIKit.Glass, 95f);
            UIKit.Place(gauge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(GaugeX, 0f), new Vector2(190f, 190f));
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
            UIKit.Place(dp.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(DistX, 0f), new Vector2(380f, 150f));
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
            // nothing to show while the cell is being placed
            bool placed = cell == null || cell.IsBound;
            if (_canvas.enabled != placed) _canvas.enabled = placed;
            if (!placed) return;
            if (mount != null)
            {
                // on its stand like a monitor on a swivel: turns about the vertical towards the
                // viewer, a little behind the head so it never jitters
                var to = _canvas.transform.position - Camera.main.transform.position; to.y = 0f;
                if (to.sqrMagnitude > 1e-4f)
                    _canvas.transform.rotation = Quaternion.Slerp(_canvas.transform.rotation,
                        Quaternion.LookRotation(to.normalized, Vector3.up), 1f - Mathf.Exp(-Time.deltaTime / 0.6f));
            }
            else UIKit.LazyFollow(_canvas.transform, Camera.main.transform, distance, elevation, 18f, 0.25f, _follow);

            bool ssmActive = cell == null || (cell.State != KittingCell.Phase.Guide && cell.State != KittingCell.Phase.Align);
            _ssmShown = Mathf.MoveTowards(_ssmShown, ssmActive ? 1f : 0f, Time.deltaTime / 0.25f);
            _ssmGroup.alpha = _ssmShown;

            float f = speed != null ? Mathf.Clamp01(speed.Factor) : 1f;
            float d = speed != null ? speed.Distance : -1f;
            float near = speed != null ? speed.dNear : 0.6f, far = speed != null ? speed.dFar : 2f;
            _shownFactor = Mathf.Lerp(_shownFactor, f, 1f - Mathf.Exp(-Time.deltaTime / 0.08f));

            Color zc = d < 0f ? UIKit.TextLo : d <= near ? UIKit.Stop : d <= far ? UIKit.Warn : UIKit.Ok;
            _gaugeFill.fillAmount = _shownFactor;
            _gaugeFill.color = f <= 0.001f ? UIKit.Stop : f < 0.99f ? UIKit.Warn : UIKit.Ok;
            _pct.text = Mathf.RoundToInt(_shownFactor * 100f) + "<size=60%>%</size>";

            _dist.text = d < 0f ? "--" : d.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "<size=60%> m</size>";
            _zone.text = d < 0f ? "" : d <= near ? "STOP ZONE" : d <= far ? "SPEED SCALING" : "FULL SPEED";
            _zone.color = zc; _zoneBar.color = zc;
        }
    }
}
