using TMPro;
using UnityEngine;

namespace MetaMove.Safety
{
    // Fine-align the QR-spawned digital twin onto the real GoFa with the controller sticks.
    // Always active - no mode to switch on:
    //
    //   left stick              move horizontally, relative to where you look (forward = away)
    //   right stick up / down   height
    //   right stick left/right  turn about the vertical axis
    //   index trigger held      fine: 5x slower
    //
    // The values float above the robot base while you steer and for 3 s after; they are saved
    // 1 s after the last stick movement. The offset moves the spawned robot inside its QR
    // anchor, so everything that hangs off the robot base moves with it (pinch target frame,
    // green safety box). Stored on the headset (PlayerPrefs), re-applied on every spawn and start.
    // (The Immersive Debugger was the first idea for this; on SDK 205 / Unity 6.3 it broke the
    // URP pipeline every frame, so it stays off.)
    public class TwinAlignment : MonoBehaviour
    {
        const string Key = "MetaMove.TwinAlignment";

        public float offsetX, offsetYUp, offsetZ, yawDeg;
        public QrAnchorCalibrator calibrator;

        [Header("Sticks")]
        [Tooltip("Metres per second at full deflection.")]
        public float moveSpeed = 0.05f;
        [Tooltip("Degrees per second at full deflection.")]
        public float turnSpeed = 5f;
        [Tooltip("Speed factor while an index trigger is held.")]
        public float fineFactor = 0.2f;
        [Range(0f, 0.5f)] public float deadzone = 0.15f;

        Transform _robot;
        Transform _head;
        TextMeshPro _label;
        float _lastInput = -100f;
        bool _dirty;
        Vector4 _applied = new Vector4(float.NaN, 0, 0, 0);

        void Awake()
        {
            Load();
            if (calibrator == null) calibrator = GetComponent<QrAnchorCalibrator>();
        }

        void OnEnable() { if (calibrator != null) calibrator.onAnchorSpawned.AddListener(OnSpawned); }
        void OnDisable() { if (calibrator != null) calibrator.onAnchorSpawned.RemoveListener(OnSpawned); }

        void OnSpawned(GameObject robot)
        {
            _robot = robot != null ? robot.transform : null;
            _applied.x = float.NaN;
        }

        void Update()
        {
            if (_robot != null) Steer();
            if (_dirty && Time.unscaledTime - _lastInput > 1f) { Save(); _dirty = false; }
            Apply();
            UpdateLabel();
        }

        void Steer()
        {
            Vector2 l = Dead(OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick));
            Vector2 r = Dead(OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick));
            if (l == Vector2.zero && r == Vector2.zero) return;
            _lastInput = Time.unscaledTime;
            _dirty = true;

            float dt = Time.unscaledDeltaTime;
            bool fine = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) > 0.5f
                        || OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) > 0.5f;
            float k = fine ? fineFactor : 1f;

            // Horizontal: in the viewer's frame, flattened, then into the anchor's frame.
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (l != Vector2.zero && _head != null && _robot.parent != null)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(_head.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                Vector3 world = (fwd * l.y + right * l.x) * moveSpeed * k * dt;
                Vector3 local = _robot.parent.InverseTransformVector(world);
                offsetX += local.x;
                offsetZ += local.z;
            }
            offsetYUp += r.y * moveSpeed * k * dt;
            yawDeg += r.x * turnSpeed * k * dt;
        }

        Vector2 Dead(Vector2 v) => v.magnitude < deadzone ? Vector2.zero : v;

        void Apply()
        {
            var want = new Vector4(offsetX, offsetYUp, offsetZ, yawDeg);
            if (_robot == null || want == _applied) return;
            _robot.localPosition = new Vector3(offsetX, offsetYUp, offsetZ);
            _robot.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            _applied = want;
        }

        void UpdateLabel()
        {
            if (_robot == null) return;
            if (_label == null)
            {
                var go = new GameObject("TwinAlignLabel");
                go.transform.SetParent(_robot, false);
                _label = go.AddComponent<TextMeshPro>();
                _label.fontSize = 0.6f;
                _label.alignment = TextAlignmentOptions.Center;
                _label.color = new Color(1f, 0.85f, 0.2f);
                _label.rectTransform.sizeDelta = new Vector2(1.2f, 0.3f);
            }
            bool show = Time.unscaledTime - _lastInput < 3f;
            _label.gameObject.SetActive(show);
            if (!show) return;
            _label.transform.position = _robot.position + Vector3.up * 1.25f;
            if (_head != null)
                _label.transform.rotation = Quaternion.LookRotation(_label.transform.position - _head.position, Vector3.up);
            _label.text = "TWIN ALIGNMENT (saved automatically)\n" +
                          $"X {offsetX * 1000:+0;-0} mm   Y {offsetYUp * 1000:+0;-0} mm   " +
                          $"Z {offsetZ * 1000:+0;-0} mm   yaw {yawDeg:+0.0;-0.0} deg";
        }

        void Load()
        {
            var p = PlayerPrefs.GetString(Key, "").Split(';');
            if (p.Length != 4) return;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var st = System.Globalization.NumberStyles.Float;
            float.TryParse(p[0], st, ci, out offsetX);
            float.TryParse(p[1], st, ci, out offsetYUp);
            float.TryParse(p[2], st, ci, out offsetZ);
            float.TryParse(p[3], st, ci, out yawDeg);
        }

        void Save()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            PlayerPrefs.SetString(Key, string.Join(";", offsetX.ToString(ci), offsetYUp.ToString(ci),
                                                   offsetZ.ToString(ci), yawDeg.ToString(ci)));
            PlayerPrefs.Save();
            Debug.Log($"[TwinAlignment] saved X {offsetX:F3} Y {offsetYUp:F3} Z {offsetZ:F3} yaw {yawDeg:F1}");
        }

        public void ResetAlignment()
        {
            offsetX = offsetYUp = offsetZ = yawDeg = 0f;
            Save();
        }
    }
}
