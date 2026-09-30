using UnityEngine;

namespace MetaMove.Robot
{
    // Robotiq 2F-85, visual only. One driver value q (rad, 0 = open, 0.8 = closed) moves
    // the six linkage joints with the mimic factors of the ROS description
    // (robotiq_description/urdf/robotiq_2f_85_macro.urdf.xacro):
    //   left_knuckle  +q   right_knuckle  -q
    //   left_inner    +q   right_inner    -q
    //   left_tip      -q   right_tip      +q
    // All joints rotate about ROS (0,-1,0), which is Unity +X in the gripper frame;
    // ROS is right-handed, so the Unity angle is negated.
    public class RobotiqGripper : MonoBehaviour
    {
        public Transform leftKnuckle, rightKnuckle, leftInner, rightInner, leftTip, rightTip;
        [Tooltip("Point between the finger pads; parts are held here.")]
        public Transform tcp;

        [Range(0f, 0.8f)] public float q;
        public float speed = 2.5f;          // rad/s, ~0.3 s open -> closed
        public float closedOnPart = 0.52f;  // q at which the pads touch a Ø30 mm gear hub
        public float closedEmpty = 0.79f;   // fully closed (grip on air)

        float _target;
        Quaternion[] _rest;
        Transform[] _joints;
        static readonly float[] Mimic = { 1f, -1f, 1f, -1f, -1f, 1f };

        void Awake()
        {
            _joints = new[] { leftKnuckle, rightKnuckle, leftInner, rightInner, leftTip, rightTip };
            _rest = new Quaternion[_joints.Length];
            for (int i = 0; i < _joints.Length; i++) if (_joints[i] != null) _rest[i] = _joints[i].localRotation;
            _target = q;
            Apply();
        }

        public void Open() => _target = 0f;
        public void CloseOnPart() => _target = closedOnPart;
        public void CloseEmpty() => _target = closedEmpty;
        public bool Settled => Mathf.Abs(q - _target) < 1e-3f;

        void Update()
        {
            if (Mathf.Approximately(q, _target)) return;
            q = Mathf.MoveTowards(q, _target, speed * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            if (_joints == null) return;
            float deg = q * Mathf.Rad2Deg;
            for (int i = 0; i < _joints.Length; i++)
                if (_joints[i] != null)
                    _joints[i].localRotation = _rest[i] * Quaternion.AngleAxis(-deg * Mimic[i], Vector3.right);
        }
    }
}
