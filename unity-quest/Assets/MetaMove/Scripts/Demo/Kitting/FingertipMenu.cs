using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MetaMove.Safety;
using MetaMove.UI.Kit;

namespace MetaMove.Demo.Kitting
{
    // Palm-up menu on the left hand, poked with the right index finger.
    //
    //   [ Demo | Auto | Freestyle ]         (gear)    mode, one tap; gear = calibrate room
    //      (o)        (o)        (o)                  at most three round buttons with icon
    //     left       middle    Restart                and label - only what makes sense now
    //
    //   Demo    Teach (only while the arm waits to be shown a box, pulses when the spot is
    //           right) - Report (once there is one) - Restart
    //   Auto    the line just runs to show the speed scaling - Restart
    //   Freestyle  Guide / Teach here - Home (always: if the arm got tangled, it goes home) - Restart
    //
    // Restart and calibrate ask "Sure?" first. It is there when you look for it and gone when
    // you work, so it never covers the cell.
    public class FingertipMenu : MonoBehaviour
    {
        public KittingCell cell;
        public ProximitySpeedController speed;
        public OVRSkeleton leftHand, rightHand;
        public TMP_FontAsset font;

        [Range(0f, 1f)] public float palmUpThreshold = 0.55f;
        public float showDelay = 0.15f, hideDelay = 0.25f;
        public float worldWidth = 0.30f;      // round buttons of ~6.5 cm: easy to poke
        public float pokeDepth = 0.018f;      // m in front of / behind the panel counts as a press
        public float pressCooldown = 0.8f;
        public CellPlacer placer;
        [Tooltip("Found in the scene when empty.")]
        public ShiftReport report;

        const float W = 600f, H = 340f, Round = 128f, RowY = -186f;
        static readonly Color BtnBg = new Color(0.22f, 0.25f, 0.29f, 1f);   // stands out from the glass
        static readonly float[] SlotX = { -180f, 0f, 180f };

        Canvas _canvas;
        CanvasGroup _group;
        float _palmUpFor, _palmDownFor, _shown, _lastPress = -9f;
        bool _visible;
        OVRSkeleton _menuHand;
        float _restartArmed = -10f, _calibArmed = -10f;

        class Hit
        {
            public RectTransform rt; public Image bg, icon; public TextMeshProUGUI label;
            public System.Action act; public bool enabled = true, visible = true;
        }
        readonly Hit[] _seg = new Hit[3];
        readonly Hit[] _slot = new Hit[3];
        Hit _gear;
        TextMeshProUGUI _gearHint;

        void Start() => Build();

        // ------------------------------------------------------------ modes

        KittingCell.Freestyle ModeOf(int seg) => seg == 1 ? KittingCell.Freestyle.Auto : seg == 2 ? KittingCell.Freestyle.Teach : KittingCell.Freestyle.None;

        void SetMode(KittingCell.Freestyle mode)
        {
            if (cell.Mode == mode) return;
            cell.Mode = mode;
            cell.Expert = mode != KittingCell.Freestyle.None;   // freestyle: no story, no hints
            cell.Restart();
        }

        void Restart()
        {
            if (Time.time - _restartArmed < 3f) { _restartArmed = -10f; cell.Restart(); }
            else _restartArmed = Time.time;
        }

        void Calibrate()
        {
            if (Time.time - _calibArmed < 3f && placer != null) placer.Replace();
            else _calibArmed = Time.time;
        }

        // ------------------------------------------------------------ build

        void Build()
        {
            _canvas = UIKit.WorldCanvas(transform, "FingertipMenuCanvas", new Vector2(W, H), 8);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _canvas.transform.localScale = Vector3.one * (worldWidth / W);
            var root = (RectTransform)_canvas.transform;
            var bg = UIKit.Panel("Glass", root, UIKit.Glass, 40f, 4f);
            UIKit.Stretch(bg.rectTransform);
            if (report == null) report = FindAnyObjectByType<ShiftReport>();

            // mode switch: one track, three segments
            var track = UIKit.Panel("ModeTrack", root, new Color(1f, 1f, 1f, 0.06f), 32f, 0f, false);
            UIKit.Place(track.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -22f), new Vector2(462f, 66f));
            string[] segNames = { "Demo", "Auto", "Freestyle" };   // "Teach" stays the button's word only
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var h = new Hit { act = () => SetMode(ModeOf(k)) };
                h.bg = UIKit.Panel("Seg_" + segNames[i], root, new Color(0f, 0f, 0f, 0f), 28f, 0f, false);
                h.rt = h.bg.rectTransform;
                UIKit.Place(h.rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f + i * 152f, -26f), new Vector2(150f, 58f));
                h.label = UIKit.Text("Label", h.rt, font, 26f, UIKit.TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Stretch(h.label.rectTransform);
                h.label.text = segNames[i];
                UIKit.Fit(h.label, 18f, 6f);
                _seg[i] = h;
            }

            // calibrate: small gear in the corner, out of the way
            _gear = Round_("Calibrate", root, new Vector2(1f, 1f), new Vector2(-26f, -22f), 66f, UIIcons.Gear, null, Calibrate);
            _gearHint = UIKit.Text("GearHint", root, font, 20f, UIKit.Stop, TextAlignmentOptions.Right, FontStyles.Bold);
            UIKit.Place(_gearHint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -92f), new Vector2(260f, 26f));

            for (int i = 0; i < 3; i++)
                _slot[i] = Round_("Slot" + i, root, new Vector2(0.5f, 1f), new Vector2(SlotX[i], RowY + Round / 2f), Round, UIIcons.Restart, "", null);
            _canvas.enabled = false;
        }

        // A round button: disc, icon, label underneath (label null: no label).
        Hit Round_(string name, RectTransform root, Vector2 anchor, Vector2 pos, float size, Sprite icon, string label, System.Action act)
        {
            var h = new Hit { act = act };
            h.bg = new GameObject(name, typeof(Image)).GetComponent<Image>();
            h.bg.transform.SetParent(root, false);
            h.bg.sprite = UIKit.Disc; h.bg.color = BtnBg; h.bg.raycastTarget = false;
            h.rt = h.bg.rectTransform;
            UIKit.Place(h.rt, anchor, new Vector2(anchor.x == 1f ? 1f : 0.5f, 1f), pos, new Vector2(size, size));
            h.icon = new GameObject("Icon", typeof(Image)).GetComponent<Image>();
            h.icon.transform.SetParent(h.rt, false);
            h.icon.sprite = icon; h.icon.color = UIKit.TextHi; h.icon.raycastTarget = false;
            UIKit.Place(h.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.5f, size * 0.5f));
            if (label != null)
            {
                h.label = UIKit.Text("Label", h.rt, font, 24f, UIKit.TextHi, TextAlignmentOptions.Top, FontStyles.Bold);
                UIKit.Place(h.label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(176f, 30f));
                UIKit.Fit(h.label, 16f, 4f);
                h.label.text = label;
            }
            return h;
        }

        // ------------------------------------------------------------ per frame

        void Update()
        {
            if (_canvas == null || Camera.main == null || cell == null) return;
            var head = Camera.main.transform;

            Vector3 palm = Vector3.zero, normal = Vector3.zero;
            bool up = leftHand != null && PalmUp(leftHand, out palm, out normal, head);
            if (up) _menuHand = leftHand;
            if (up) { _palmUpFor += Time.deltaTime; _palmDownFor = 0f; } else { _palmDownFor += Time.deltaTime; _palmUpFor = 0f; }
            if (!_visible && _palmUpFor > showDelay) _visible = true;
            if (_visible && _palmDownFor > hideDelay) _visible = false;

            _shown = Mathf.MoveTowards(_shown, _visible ? 1f : 0f, Time.deltaTime / 0.15f);
            _group.alpha = _shown;
            _canvas.enabled = _shown > 0.001f;
            if (!_canvas.enabled) return;

            if (up)
            {
                var pos = palm + normal * 0.10f + Vector3.up * 0.06f;   // clear of the menu hand's fingers
                var look = pos - head.position; look.y = 0f;
                _canvas.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look.normalized, Vector3.up) * Quaternion.Euler(20f, 0f, 0f));
            }
            Refresh();
            Poke();
        }

        void Refresh()
        {
            // mode switch
            for (int i = 0; i < 3; i++)
            {
                bool on = cell.Mode == ModeOf(i);
                _seg[i].bg.color = on ? new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.85f) : new Color(0f, 0f, 0f, 0f);
                _seg[i].label.color = on ? new Color(0.05f, 0.07f, 0.09f, 1f) : UIKit.TextLo;
            }
            bool calibArmed = Time.time - _calibArmed < 3f;
            _gear.bg.color = calibArmed ? Pulse(UIKit.Stop, 8f) : BtnBg;
            _gearHint.text = calibArmed ? "Calibrate room? Tap again" : "";

            // the three slots, by mode and moment
            bool guiding = cell.State == KittingCell.Phase.Guide;
            bool ready = guiding && cell.TrayUnderTool() != null;
            bool restartArmed = Time.time - _restartArmed < 3f;
            switch (cell.Mode)
            {
                case KittingCell.Freestyle.None:
                    Set(_slot[0], guiding || cell.State == KittingCell.Phase.Align, UIIcons.Check, "Teach", () => cell.Teach(), guiding,
                        ready ? Pulse(UIKit.Ok, 6f) : new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.55f));
                    Set(_slot[1], report != null && report.HasReport, UIIcons.Report, "Report", () => report.Toggle(), true,
                        report != null && report.Showing ? new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.55f) : BtnBg);
                    break;
                case KittingCell.Freestyle.Auto:
                    Set(_slot[0], false, null, "", null, false, BtnBg);
                    Set(_slot[1], false, null, "", null, false, BtnBg);
                    break;
                case KittingCell.Freestyle.Teach:
                    if (guiding || cell.State == KittingCell.Phase.Align)
                        Set(_slot[0], true, UIIcons.Check, "Teach here", () => cell.Teach(), guiding,
                            ready ? Pulse(UIKit.Ok, 6f) : new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.45f));
                    else
                        Set(_slot[0], cell.IsBound, UIIcons.Guide, "Guide", () => cell.StartGuide(), true, new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, 0.45f));
                    Set(_slot[1], cell.IsBound, UIIcons.Home, cell.Parked ? "Resume" : "Home", () => cell.ParkHome(), true,
                        cell.Parked ? Pulse(UIKit.Warn, 5f) : BtnBg);
                    break;
            }
            Set(_slot[2], true, UIIcons.Restart, restartArmed ? "Sure?" : "Restart", Restart, true,
                restartArmed ? Pulse(UIKit.Stop, 8f) : BtnBg);
        }

        static Color Pulse(Color c, float hz) => new Color(c.r, c.g, c.b, 0.55f + 0.3f * Mathf.Sin(Time.time * hz));

        static void Set(Hit h, bool visible, Sprite icon, string label, System.Action act, bool enabled, Color bg)
        {
            h.visible = visible;
            if (h.rt.gameObject.activeSelf != visible) h.rt.gameObject.SetActive(visible);
            if (!visible) return;
            if (h.icon.sprite != icon) h.icon.sprite = icon;
            if (h.label != null && h.label.text != label) h.label.text = label;
            h.act = act; h.enabled = enabled;
            h.bg.color = bg;
            var fg = enabled ? UIKit.TextHi : UIKit.TextLo * 0.8f;
            h.icon.color = fg;
            if (h.label != null) h.label.color = fg;
        }

        IEnumerable<Hit> Hits()
        {
            foreach (var s in _seg) yield return s;
            yield return _gear;
            foreach (var s in _slot) if (s.visible) yield return s;
        }

        void Poke()
        {
            Vector3 tip;
            if (!Bone(rightHand, OVRSkeleton.BoneId.XRHand_IndexTip, OVRSkeleton.BoneId.Hand_IndexTip, out tip)) return;
            if (Time.time - _lastPress < pressCooldown) return;
            foreach (var h in Hits())
            {
                var local = h.rt.InverseTransformPoint(tip);
                var r = h.rt.rect;
                float depth = local.z * _canvas.transform.lossyScale.z;
                if (r.Contains(new Vector2(local.x, local.y)) && Mathf.Abs(depth) < pokeDepth)
                {
                    _lastPress = Time.time;
                    if (h.enabled && h.act != null) { h.act(); StartCoroutine(Flash(h)); }
                    Debug.Log("[FingertipMenu] " + (h.label != null ? h.label.text : h.rt.name) + (h.enabled ? "" : " (disabled)"));
                    break;
                }
            }
        }

        System.Collections.IEnumerator Flash(Hit h)
        {
            h.rt.localScale = Vector3.one * 0.92f;
            yield return new WaitForSeconds(0.12f);
            h.rt.localScale = Vector3.one;
        }

        // Test hook for the Editor / Simulator: press by label ("Demo", "Auto", "Freestyle", "Teach", "Teach here",
        // "Guide", "Home", "Report", "Restart", "Calibrate").
        public void Press(string name)
        {
            if (name == "Calibrate" || name == "Place") { Calibrate(); return; }
            if (name == "Restart") { Restart(); return; }
            if (_canvas != null) Refresh();
            foreach (var h in Hits())
                if (h.label != null && (h.label.text == name || (name == "Home" && h.label.text == "Resume")) && h.enabled && h.act != null) { h.act(); return; }
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
