using Meta.XR.ImmersiveDebugger;
using UnityEngine;

namespace MetaMove.Safety
{
    // Fine-align the QR-spawned digital twin onto the real GoFa from inside the headset:
    // Immersive Debugger (menu button) -> "Twin alignment" -> X / Y / Z / yaw sliders.
    //
    // The offset moves the spawned robot inside its QR anchor, i.e. along the robot's own
    // axes (Y = up). Everything that hangs off the robot base moves with it - the pinch
    // target frame and the green safety box - so all stays consistent with the real arm.
    // Stored on the headset (PlayerPrefs) and re-applied on every spawn and app start.
    public class TwinAlignment : MonoBehaviour
    {
        const string Key = "MetaMove.TwinAlignment";

        [DebugMember(Tweakable = true, Min = -0.3f, Max = 0.3f, Category = "Twin alignment")]
        public float offsetX;
        [DebugMember(Tweakable = true, Min = -0.3f, Max = 0.3f, Category = "Twin alignment")]
        public float offsetYUp;
        [DebugMember(Tweakable = true, Min = -0.3f, Max = 0.3f, Category = "Twin alignment")]
        public float offsetZ;
        [DebugMember(Tweakable = true, Min = -15f, Max = 15f, Category = "Twin alignment")]
        public float yawDeg;

        public QrAnchorCalibrator calibrator;

        Transform _robot;
        Vector4 _applied = new Vector4(float.NaN, 0, 0, 0);
        float _lastChange = -1f;

        void Awake()
        {
            var s = PlayerPrefs.GetString(Key, "");
            var p = s.Split(';');
            if (p.Length == 4)
            {
                float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out offsetX);
                float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out offsetYUp);
                float.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out offsetZ);
                float.TryParse(p[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out yawDeg);
            }
            if (calibrator == null) calibrator = GetComponent<QrAnchorCalibrator>();
        }

        void OnEnable()
        {
            if (calibrator != null) calibrator.onAnchorSpawned.AddListener(OnSpawned);
        }

        void OnDisable()
        {
            if (calibrator != null) calibrator.onAnchorSpawned.RemoveListener(OnSpawned);
        }

        void OnSpawned(GameObject robot)
        {
            _robot = robot != null ? robot.transform : null;
            _applied.x = float.NaN;   // force apply
        }

        [DebugMember(Category = "Twin alignment")]
        public void ResetAlignment()
        {
            offsetX = offsetYUp = offsetZ = yawDeg = 0f;
        }

        void LateUpdate()
        {
            var want = new Vector4(offsetX, offsetYUp, offsetZ, yawDeg);
            if (_robot != null && want != _applied)
            {
                _robot.localPosition = new Vector3(offsetX, offsetYUp, offsetZ);
                _robot.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
                if (!float.IsNaN(_applied.x)) _lastChange = Time.unscaledTime;
                _applied = want;
            }
            // Save a moment after the last slider move, not on every frame of a drag.
            if (_lastChange > 0f && Time.unscaledTime - _lastChange > 1f)
            {
                _lastChange = -1f;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                PlayerPrefs.SetString(Key, string.Join(";", offsetX.ToString(ci), offsetYUp.ToString(ci),
                                                       offsetZ.ToString(ci), yawDeg.ToString(ci)));
                PlayerPrefs.Save();
            }
        }
    }
}
