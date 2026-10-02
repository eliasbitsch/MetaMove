using TMPro;
using UnityEngine;
using MetaMove.Safety;
using MetaMove.UI.Kit;

namespace MetaMove.Demo.Kitting
{
    // While the story explains how the cell knows where you are, the chain is drawn into the
    // room, live:
    //   QR plate  -> robot base      measured offset (the anchor is surveyed to the robot)
    //   headset   -> QR plate        SLAM tracking (the headset localises the anchor)
    //   robot base -> nearest of head/hands    the distance that sets the speed, in metres
    public class TrackingViz : MonoBehaviour
    {
        public KittingCell cell;
        public ProximitySpeedController speed;
        public StoryDirector story;
        public TMP_FontAsset font;
        public Material lineMaterial;
        [Tooltip("Story beat during which the chain is shown.")]
        public string beatKey = "tracking";

        static readonly Color Cyan = new Color(0.25f, 0.82f, 0.88f), White = new Color(0.95f, 0.97f, 1f), Amber = new Color(1f, 0.66f, 0.16f);

        Transform _qr;
        LineRenderer _offset, _slam, _dist;
        Tag _tOffset, _tSlam, _tDist;
        float _shown;

        void Start()
        {
            _offset = Line("Viz_Offset", Cyan, 0.010f);
            _slam = Line("Viz_Slam", White, 0.006f);
            _dist = Line("Viz_Distance", Amber, 0.012f);
            _tOffset = new Tag(transform, font, Cyan, "measured offset");
            _tSlam = new Tag(transform, font, White, "SLAM tracking");
            _tDist = new Tag(transform, font, Amber, "");
        }

        void LateUpdate()
        {
            bool on = story != null && story.Current == beatKey && cell != null && cell.IsBound && Camera.main != null;
            _shown = Mathf.MoveTowards(_shown, on ? 1f : 0f, Time.deltaTime / 0.5f);
            bool vis = _shown > 0.001f;
            _offset.enabled = _slam.enabled = _dist.enabled = vis;
            _tOffset.Show(vis); _tSlam.Show(vis); _tDist.Show(vis);
            if (!vis) return;

            if (_qr == null && cell.cellFrame != null)
                foreach (var t in cell.cellFrame.GetComponentsInChildren<Transform>(true))
                    if (t.name == "AnchorQR") { _qr = t; break; }
            if (_qr == null) return;

            var cam = Camera.main.transform;
            var robotBase = speed != null && speed.robotBase != null ? speed.robotBase.position : cell.cellFrame.position;
            var near = Nearest(robotBase, cam.position);
            float a = Mathf.SmoothStep(0f, 1f, _shown);
            // the lines draw themselves one after another
            Set(_offset, _qr.position, robotBase, Cyan, a, Mathf.Clamp01(_shown * 3f));
            Set(_slam, cam.position + cam.forward * 0.25f - cam.up * 0.12f, _qr.position, White, a * 0.8f, Mathf.Clamp01(_shown * 3f - 1f));
            var flat = new Vector3(near.x, robotBase.y, near.z);
            Set(_dist, robotBase, flat, Amber, a, Mathf.Clamp01(_shown * 3f - 2f));

            _tOffset.Place((_qr.position + robotBase) * 0.5f + Vector3.up * 0.06f, cam, a);
            _tSlam.Place(Vector3.Lerp(cam.position, _qr.position, 0.55f) + Vector3.up * 0.05f, cam, a * 0.9f);
            float d = speed != null && speed.Distance >= 0f ? speed.Distance : Vector3.Distance(new Vector3(robotBase.x, 0f, robotBase.z), new Vector3(near.x, 0f, near.z));
            _tDist.text.text = d.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " m";
            _tDist.Place((robotBase + flat) * 0.5f + Vector3.up * 0.08f, cam, a);
        }

        Vector3 Nearest(Vector3 robotBase, Vector3 fallback)
        {
            if (speed == null || speed.humanPoints == null) return fallback;
            Vector3 best = fallback; float bd = float.MaxValue;
            foreach (var p in speed.humanPoints)
            {
                if (p == null) continue;
                var v = p.position - robotBase; v.y = 0f;
                if (v.sqrMagnitude < bd) { bd = v.sqrMagnitude; best = p.position; }
            }
            return best;
        }

        LineRenderer Line(string name, Color c, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.widthMultiplier = width;
            lr.sharedMaterial = lineMaterial;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.enabled = false;
            return lr;
        }

        static void Set(LineRenderer lr, Vector3 from, Vector3 to, Color c, float alpha, float grow)
        {
            lr.SetPosition(0, from);
            lr.SetPosition(1, Vector3.Lerp(from, to, grow));
            c.a = alpha;
            lr.startColor = c; lr.endColor = c;
        }

        class Tag
        {
            readonly Transform _t;
            readonly CanvasGroup _g;
            public readonly TextMeshProUGUI text;

            public Tag(Transform parent, TMP_FontAsset font, Color color, string s)
            {
                var c = UIKit.WorldCanvas(parent, "VizTag", new Vector2(420f, 80f), 9);
                _t = c.transform;
                _t.localScale = Vector3.one * (0.16f / 420f);
                _g = c.gameObject.AddComponent<CanvasGroup>();
                var bg = UIKit.Panel("Bg", _t, UIKit.Glass, 30f, 2f);
                UIKit.Stretch(bg.rectTransform);
                text = UIKit.Text("Text", _t, font, 40f, color, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Stretch(text.rectTransform);
                text.text = s;
                _t.gameObject.SetActive(false);
            }

            public void Show(bool on) { if (_t.gameObject.activeSelf != on) _t.gameObject.SetActive(on); }

            public void Place(Vector3 pos, Transform cam, float alpha)
            {
                _t.position = pos;
                var look = pos - cam.position; look.y = 0f;
                if (look.sqrMagnitude > 1e-4f) _t.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                _g.alpha = alpha;
            }
        }
    }
}
