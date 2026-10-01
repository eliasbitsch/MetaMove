using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MetaMove.UI.Kit;

namespace MetaMove.Demo.Kitting
{
    // In-world hints: what to grab, and where it goes. At most two markers at a time, one
    // on the thing ("PICK UP") and one on its destination ("LOAD HERE"), each a label that
    // turns to the viewer, a bobbing arrow and a pulsing ring on the surface underneath.
    // Everything is read from the cell's state, so a hint never runs ahead of the story.
    public class KittingHints : MonoBehaviour
    {
        public KittingCell cell;
        public FlowRack rack;
        public TMP_FontAsset font;
        [Tooltip("Hints only after the welcome, so the first look goes to the robot.")]
        public float quietAfterBind = 10f;

        Marker _what, _where;
        float _boundAt = -1f;

        void Start()
        {
            _what = new Marker("Hint_What", transform, font, UIKit.Warn);
            _where = new Marker("Hint_Where", transform, font, UIKit.Accent);
        }

        void LateUpdate()
        {
            if (_what == null || cell == null || !cell.IsBound || cell.Expert || Camera.main == null) { Hide(); return; }
            if (_boundAt < 0f) _boundAt = Time.time;
            var head = Camera.main.transform.position;

            var cb = cell.trayB != null ? cell.trayB.GetComponent<CarryableTray>() : null;
            var ca = cell.trayA != null ? cell.trayA.GetComponent<CarryableTray>() : null;
            bool shownWhat = false, shownWhere = false;

            // 1. new batch: trolley -> rack
            if (cb != null && cell.RefillDue && !cell.TrayBInRack)
            {
                if (cb.State == CarryableTray.Phase.Waiting)
                { _what.Show(cell.trayB.transform, 0.15f, "PICK UP", 0.46f, head); shownWhat = true; }
                if (cb.State == CarryableTray.Phase.Held || cb.State == CarryableTray.Phase.Waiting)
                {
                    int lane = rack != null ? rack.NearestLane(rack.entries[rack.LaneCount - 1].position, cell.trayA) : -1;
                    if (lane >= 0) { _where.Show(rack.entries[lane], 0.0f, "LOAD HERE", 0.46f, head); shownWhere = true; }
                }
            }
            // 2. empty box: pick face -> return lane
            else if (ca != null && cell.AllowEmptyRemoval && !cell.TrayACleared)
            {
                if (ca.State == CarryableTray.Phase.Waiting)
                { _what.Show(cell.trayA.transform, 0.15f, "TAKE OUT", 0.46f, head); shownWhat = true; }
                int lane = rack != null ? rack.NearestLane(cell.trayA.transform.position) : -1;
                if (lane >= 0) { _where.Show(rack.returns[lane], 0.0f, "RETURN LANE", 0.46f, head); shownWhere = true; }
            }
            // 3. hand guiding: grab the tool, bring it over the new box, Teach
            else if (cell.State == KittingCell.Phase.Guide && cell.tcp != null)
            {
                if (!cell.GuideHeld)
                {
                    var handle = cell.GuideRelay != null ? cell.GuideRelay.transform : cell.tcp;
                    _what.Show(handle, 0.06f, "PINCH HERE", 0.14f, head); shownWhat = true;
                }
                bool over = cell.TrayUnderTool() != null;
                _where.Show(cell.trayB.transform, 0.15f, over ? "NOW PRESS TEACH" : "BRING IT HERE", 0.46f, head);
                shownWhere = true;
            }

            bool quiet = Time.time - _boundAt < quietAfterBind;
            if (!shownWhat || quiet) _what.Hide();
            if (!shownWhere || quiet) _where.Hide();
            _what.Tick(); _where.Tick();
        }

        void Hide() { if (_what != null) { _what.Hide(); _where.Hide(); _what.Tick(); _where.Tick(); } }

        void OnCueRestart() => _boundAt = -1f;
        void OnEnable() { if (cell != null) cell.Cue += Cue; }
        void OnDisable() { if (cell != null) cell.Cue -= Cue; }
        void Cue(string key) { if (key == "restart") OnCueRestart(); }

        // ---------------------------------------------------------------- marker

        class Marker
        {
            readonly Transform _root, _label, _arrow, _ring;
            readonly CanvasGroup _labelGroup;
            readonly Image _ringImg, _arrowImg;
            readonly TextMeshProUGUI _text;
            Transform _target;
            float _lift, _ringSize, _alpha, _goal;
            readonly Color _color;

            public Marker(string name, Transform parent, TMP_FontAsset font, Color color)
            {
                _color = color;
                _root = new GameObject(name).transform;
                _root.SetParent(parent, false);

                // label pill + arrow, facing the viewer
                var c = UIKit.WorldCanvas(_root, "Label", new Vector2(420f, 220f), 9);
                _label = c.transform;
                _label.localScale = Vector3.one * (0.22f / 420f);
                _labelGroup = c.gameObject.AddComponent<CanvasGroup>();
                var pill = UIKit.Panel("Pill", _label, UIKit.Glass, 40f, 3f);
                UIKit.Place(pill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(420f, 90f));
                _text = UIKit.Text("Text", pill.transform, font, 40f, color, TextAlignmentOptions.Center, FontStyles.Bold);
                UIKit.Stretch(_text.rectTransform);
                _text.characterSpacing = 6f;
                _arrowImg = new GameObject("Arrow", typeof(Image)).GetComponent<Image>();
                _arrowImg.transform.SetParent(_label, false);
                _arrowImg.sprite = UIKit.Play; _arrowImg.color = color; _arrowImg.raycastTarget = false;
                _arrow = _arrowImg.transform;
                UIKit.Place((RectTransform)_arrow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(90f, 90f));
                _arrow.localRotation = Quaternion.Euler(0f, 0f, -90f);   // play triangle points down

                // pulsing ring flat on the surface under the target
                var rc = UIKit.WorldCanvas(_root, "Ring", new Vector2(256f, 256f), 8);
                _ring = rc.transform;
                _ringImg = new GameObject("Ring", typeof(Image)).GetComponent<Image>();
                _ringImg.transform.SetParent(_ring, false);
                _ringImg.sprite = UIKit.Ring; _ringImg.raycastTarget = false;
                UIKit.Stretch(_ringImg.rectTransform);
                _root.gameObject.SetActive(false);
            }

            public void Show(Transform target, float lift, string text, float ringSize, Vector3 head)
            {
                _target = target; _lift = lift; _ringSize = ringSize; _goal = 1f;
                if (_text.text != text) _text.text = text;
                var surface = target.position;
                _ring.position = surface + target.up * 0.012f;
                _ring.rotation = Quaternion.LookRotation(-target.up, target.forward);   // canvas faces up
                float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 4f);
                _ring.localScale = Vector3.one * (ringSize * pulse / 256f);
                var top = surface + Vector3.up * (0.30f + lift + 0.03f * Mathf.Sin(Time.time * 3f));
                _label.position = top;
                var look = top - head; look.y = 0f;
                if (look.sqrMagnitude > 1e-4f) _label.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }

            public void Hide() => _goal = 0f;

            public void Tick()
            {
                _alpha = Mathf.MoveTowards(_alpha, _goal, Time.deltaTime / 0.25f);
                bool on = _alpha > 0.001f && _target != null;
                if (_root.gameObject.activeSelf != on) _root.gameObject.SetActive(on);
                if (!on) return;
                _labelGroup.alpha = _alpha;
                _ringImg.color = new Color(_color.r, _color.g, _color.b, _alpha * (0.55f + 0.35f * Mathf.Sin(Time.time * 4f)));
            }
        }
    }
}
