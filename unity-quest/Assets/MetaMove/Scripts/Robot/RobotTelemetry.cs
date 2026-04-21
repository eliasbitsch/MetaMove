using UnityEngine;
using MetaMove.Robot.EGM;

namespace MetaMove.Robot
{
    // Single pull-point for live robot state. Overlays + HUD subscribe here instead of
    // poking EgmClient directly, so we can swap the source (EGM, ROS, mock) from one place.
    public class RobotTelemetry : MonoBehaviour
    {
        public EgmClient egm;

        [Header("Latest")]
        public float[] jointDeg = new float[6];
        public float[] jointVelDegSec = new float[6];   // numerically differentiated
        public float[] jointTorqueNm = new float[6];    // not in standard EGM feedback; wired from RWS or estimated
        public Vector3 tcpPositionMeters;
        public Quaternion tcpRotation = Quaternion.identity;
        public bool motorsOn;
        public float hz;

        float[] _prevAngles = new float[6];
        float _prevT;
        bool _primed;

        void Update()
        {
            if (egm == null || !egm.TryGetLatest(out var fb)) return;

            int n = Mathf.Min(6, fb.joints.joints.Count);
            float t = Time.unscaledTime;
            float dt = _primed ? Mathf.Max(1e-4f, t - _prevT) : 0f;
            for (int i = 0; i < n; i++)
            {
                float deg = (float)fb.joints.joints[i];
                if (_primed) jointVelDegSec[i] = (deg - _prevAngles[i]) / dt;
                _prevAngles[i] = deg;
                jointDeg[i] = deg;
            }

            tcpPositionMeters = new Vector3(
                (float)(fb.cartesian.pos.x / 1000.0),
                (float)(fb.cartesian.pos.y / 1000.0),
                (float)(fb.cartesian.pos.z / 1000.0));
            var q = fb.cartesian.orient;
            tcpRotation = new Quaternion((float)q.u1, (float)q.u2, (float)q.u3, (float)q.u0);

            motorsOn = fb.motorState == 2;
            hz = egm.MeasuredHz;
            _prevT = t;
            _primed = true;
        }
    }
}
