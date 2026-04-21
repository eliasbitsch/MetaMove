using UnityEngine;

namespace MetaMove.UI
{
    // Show-off mode: positions three existing panels in a spatial triangle around the
    // user. Re-runs the arrangement whenever enabled so the geometry "locks" to the
    // user's current headset orientation at mode switch.
    //
    // Typical content:
    //   Left panel  = Telemetry / Status
    //   Front panel = Paths / Execution
    //   Right panel = Safety / Motors
    //
    // Grab-translation on individual panels still works afterwards.
    public class ControlCenterLayout : MonoBehaviour
    {
        public Transform userAnchor;
        public Transform leftPanel;
        public Transform frontPanel;
        public Transform rightPanel;

        [Header("Arrangement")]
        public float distanceMeters = 1.0f;
        public float sideAngleDegrees = 60f;     // left/right angle from forward
        public float eyeHeightMeters = 1.5f;
        public bool tiltInward = true;
        public float tiltDegrees = 25f;

        void OnEnable() => Rearrange();

        [ContextMenu("Rearrange Now")]
        public void Rearrange()
        {
            if (userAnchor == null && Camera.main != null) userAnchor = Camera.main.transform;
            if (userAnchor == null) return;

            Vector3 userPos = userAnchor.position;
            Vector3 userFwd = userAnchor.forward; userFwd.y = 0f;
            if (userFwd.sqrMagnitude < 1e-4f) userFwd = Vector3.forward; userFwd.Normalize();

            Place(frontPanel, userPos, userFwd, 0f, 0f);
            Place(leftPanel, userPos, userFwd, -sideAngleDegrees, tiltInward ? -tiltDegrees : 0f);
            Place(rightPanel, userPos, userFwd, +sideAngleDegrees, tiltInward ? +tiltDegrees : 0f);
        }

        void Place(Transform panel, Vector3 userPos, Vector3 userFwd, float yaw, float yawTilt)
        {
            if (panel == null) return;
            Quaternion q = Quaternion.AngleAxis(yaw, Vector3.up);
            Vector3 dir = q * userFwd;
            Vector3 pos = userPos + dir * distanceMeters;
            pos.y = userPos.y + (eyeHeightMeters - userPos.y);

            panel.position = pos;
            panel.rotation = Quaternion.LookRotation(panel.position - userPos, Vector3.up) *
                             Quaternion.Euler(0f, yawTilt, 0f);
        }
    }
}
