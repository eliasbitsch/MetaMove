using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MetaMove.Safety;
using MetaMove.UI.Kit;

namespace MetaMove.Demo.Kitting
{
    // Palm-up menu on either hand (left- and right-handers): status line plus Teach / Home /
    // Report / Restart / Expert / Place, poked with the index finger of the other hand. Expert restarts the
    // run without hints or narration - the second go, from memory. It is there when you look for it and gone when you work, so it
    // never covers the cell. Teach is only live while the cell is waiting to be shown a tray.
    public class FingertipMenu : MonoBehaviour
    {
        public KittingCell cell;
        public ProximitySpeedController speed;
        public OVRSkeleton leftHand, rightHand;
        public TMP_FontAsset font;

        [Range(0f, 1f)] public float palmUpThreshold = 0.55f;
        public float showDelay = 0.15f, hideDelay = 0.25f;
        public float worldWidth = 0.322f;   // six buttons of ~4.5 cm with ~8 mm gaps: pokeable live
        public float pokeDepth = 0.018f;      // m in front of / behind the panel counts as a press
        public float pressCooldown = 0.8f;

        const float W = 764f, H = 330f;
        Canvas _canvas;
        CanvasGroup _group;
        TextMeshProUGUI _status;
        readonly Button[] _buttons = new Button[6];
        public CellPlacer placer;
        [Tooltip("Found in the scene when empty.")]
        public ShiftReport report;
        float _placeArmed = -10f;   // Place needs a second tap within 3 s
        float _palmUpFor, _palmDownFor, _shown, _lastPress = -9f;
        bool _visible;
        OVRSkeleton _menuHand;   // the hand that holds the menu; the other one pokes

        class Button { public Image bg; public TextMeshProUGUI label; public RectTransform rt; public System.Action act; public bool enabled = true; }

        void Start() => Build();

        void Build()
        {
            _canvas = UIKit.WorldCanvas(transform, "FingertipMenuCanvas", new Vector2(W, H), 8);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _canvas.transform.localScale = Vector3.one * (worldWidth / W);
            var root = (RectTransform)_canvas.transform;
            var bg = UIKit.Panel("Glass", root, UIKit.Glass, 40f, 4f);
            UIKit.Stretch(bg.rectTransform);
            _status = UIKit.Text("Status", root, font, 30f, UIKit.TextHi, TextAlignmentOptions.Center);
            UIKit.Place(_status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(W - 40f, 70f));
            _status.enableWordWrapping = true;
            if (report == null) report = FindAnyObjectByType<ShiftReport>();
            string[] names = { "Teach", "Home", "Restart", "Expert", "Place", "Report" };
            int[] slot = { 0, 1, 3, 4, 5, 2 };   // left to right: Teach Home Report Restart Expert Place
            System.Action[] acts = { () => { if (cell.State == KittingCell.Phase.Guide) cell.Teach(); else cell.StartGuide(); }, () => cell.ParkHome(), () => cell.Restart(),
                                     () => { cell.Expert = !cell.Expert; cell.Restart(); },
                                     () => { if (Time.time - _placeArmed < 3f && placer != null) placer.Replace(); else _placeArmed = Time.time; },
                                     () => { if (report != null) report.Toggle(); } };
            for (int i = 0; i < 6; i++)
            {
                var b = new Button { act = acts[i] };
                b.bg = UIKit.Panel("Btn_" + names[i], root, UIKit.GlassLight, 34f, 3f);
                b.rt = b.bg.rectTransform;
                UIKit.Place(b.rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((slot[i] - 2.5f) * 124f, 26f), new Vector2(106f, 170f));
                b.label = UIKit.Text("Label", b.bg.transform, font, 25f, UIKit.TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Stretch(b.label.rectTransform);
                b.label.text = names[i];
                _buttons[i] = b;
            }
            _canvas.enabled = false;
        }

        void Update()
        {
            if (_canvas == null || Camera.main == null || cell == null) return;
            var head = Camera.main.transform;

            Vector3 palm, normal;
            // Keep the hand that already holds the menu; otherwise take whichever palm is up.
            bool up = false;
            if (_menuHand != null) up = PalmUp(_menuHand, out palm, out normal, head);
            else { palm = normal = Vector3.zero; }
            if (!up)
            {
                foreach (var h in new[] { leftHand, rightHand })
                    if (h != null && h != _menuHand && PalmUp(h, out palm, out normal, head)) { up = true; _menuHand = h; break; }
            }
            if (up) { _palmUpFor += Time.deltaTime; _palmDownFor = 0f; } else { _palmDownFor += Time.deltaTime; _palmUpFor = 0f; }
            if (!_visible && _palmUpFor > showDelay) _visible = true;
            if (_visible && _palmDownFor > hideDelay) _visible = false;

            _shown = Mathf.MoveTowards(_shown, _visible ? 1f : 0f, Time.deltaTime / 0.15f);
            _group.alpha = _shown;
            _canvas.enabled = _shown > 0.001f;
            if (!_canvas.enabled) return;

            if (up)
            {
                var pos = palm + normal * 0.10f + Vector3.up * 0.05f;   // clear of the menu hand's fingers
                var look = pos - head.position; look.y = 0f;
                _canvas.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look.normalized, Vector3.up) * Quaternion.Euler(20f, 0f, 0f));
            }
            Refresh();
            Poke();
        }

        void Refresh()
        {
            float f = speed != null ? speed.Factor : 1f;
            float d = speed != null ? speed.Distance : -1f;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string state = (!cell.IsBound ? "Not placed" : cell.State.ToString()) + (cell.Expert ? " · expert" : "");
            _status.text = "<b>" + state + "</b>   " + Mathf.RoundToInt(f * 100f) + "%   " + (d < 0f ? "--" : d.ToString("0.00", inv) + " m");
            // Teach while guiding; otherwise the same button starts guiding by hand.
            _buttons[0].enabled = cell.IsBound && cell.State != KittingCell.Phase.Align;
            _buttons[0].label.text = cell.State == KittingCell.Phase.Guide ? "Teach" : "Guide";
            _buttons[1].label.text = cell.Parked ? "Resume" : "Home";
            // Teach lights up once the tool is over a tray it would accept.
            bool ready = cell.State == KittingCell.Phase.Guide && cell.TrayUnderTool() != null;
            bool armed = Time.time - _placeArmed < 3f;
            _buttons[4].label.text = armed ? "Sure?" : "Place";
            // the last shift report, again or away; there is none before the first run ends
            _buttons[5].enabled = report != null && report.HasReport;
            for (int i = 0; i < 6; i++)
            {
                var b = _buttons[i];
                b.label.color = b.enabled ? UIKit.TextHi : UIKit.TextLo * 0.8f;
                float pulse = ready && i == 0 ? 0.35f + 0.35f * Mathf.Sin(Time.time * 6f) : 0f;
                b.bg.color = i == 0 && b.enabled ? new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.55f + pulse)
                           : i == 3 && cell.Expert ? new Color(UIKit.Warn.r, UIKit.Warn.g, UIKit.Warn.b, 0.6f)
                           : i == 4 && armed ? new Color(UIKit.Stop.r, UIKit.Stop.g, UIKit.Stop.b, 0.5f + 0.3f * Mathf.Sin(Time.time * 8f))
                           : i == 5 && report != null && report.Showing ? new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.45f)
                           : i == 1 && cell.Parked ? new Color(UIKit.Warn.r, UIKit.Warn.g, UIKit.Warn.b, 0.45f + 0.3f * Mathf.Sin(Time.time * 5f))
                           : UIKit.GlassLight;
            }
        }

        void Poke()
        {
            Vector3 tip;
            var poker = _menuHand == leftHand ? rightHand : leftHand;
            if (!Bone(poker, OVRSkeleton.BoneId.XRHand_IndexTip, OVRSkeleton.BoneId.Hand_IndexTip, out tip)) return;
            if (Time.time - _lastPress < pressCooldown) return;
            foreach (var b in _buttons)
            {
                var local = b.rt.InverseTransformPoint(tip);
                var r = b.rt.rect;
                float depth = local.z * _canvas.transform.lossyScale.z;
                if (r.Contains(new Vector2(local.x, local.y)) && Mathf.Abs(depth) < pokeDepth)
                {
                    _lastPress = Time.time;
                    if (b.enabled) { b.act(); StartCoroutine(Flash(b)); }
                    Debug.Log("[FingertipMenu] " + b.label.text + (b.enabled ? "" : " (disabled)"));
                    break;
                }
            }
        }

        System.Collections.IEnumerator Flash(Button b)
        {
            b.rt.localScale = Vector3.one * 0.92f;
            yield return new WaitForSeconds(0.12f);
            b.rt.localScale = Vector3.one;
        }

        // Test hook for the Editor / Simulator: press a button by name.
        public void Press(string name)
        {
            foreach (var b in _buttons) if ((b.label.text == name || (name == "Home" && b.label.text == "Resume") || (name == "Place" && b.label.text == "Sure?")) && b.enabled) { b.act(); return; }
        }

        bool PalmUp(OVRSkeleton hand, out Vector3 palm, out Vector3 normal, Transform head)
        {
            palm = normal = Vector3.zero;
            Vector3 wrist, index1, pinky1;
            if (!Bone(hand, OVRSkeleton.BoneId.XRHand_Wrist, OVRSkeleton.BoneId.Hand_WristRoot, out wrist)) return false;
            if (!Bone(hand, OVRSkeleton.BoneId.XRHand_IndexProximal, OVRSkeleton.BoneId.Hand_Index1, out index1)) return false;
            if (!Bone(hand, OVRSkeleton.BoneId.XRHand_LittleProximal, OVRSkeleton.BoneId.Hand_Pinky1, out pinky1)) return false;
            palm = (wrist + index1 + pinky1) / 3f;
            normal = Vector3.Cross(index1 - wrist, pinky1 - wrist).normalized;
            if (Vector3.Dot(normal, head.position - palm) < 0f) normal = -normal; // the side you can see
            return Vector3.Dot(normal, Vector3.up) > palmUpThreshold;
        }

        // OpenXR (XRHand_*) and legacy (Hand_*) bone ids share numeric values, so the id set
        // is chosen by the skeleton's actual type, never by trying one and then the other.
        static bool Bone(OVRSkeleton s, OVRSkeleton.BoneId xrId, OVRSkeleton.BoneId legacyId, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (s == null || !s.IsInitialized || !s.IsDataValid || s.Bones == null) return false;
            var id = s.GetSkeletonType().ToString().StartsWith("XRHand") ? xrId : legacyId;
            foreach (var b in s.Bones) if (b.Id == id) { pos = b.Transform.position; return true; }
            return false;
        }
    }
}
