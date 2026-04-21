using System;
using UnityEngine;

namespace MetaMove.Robot
{
    // Joint limits for the ABB GoFa CRB 15000 5kg / 950mm reach.
    // Values mirror crb15000_5_95.xacro (position + velocity + effort).
    // Edit here if the xacro is updated — single source of truth on the Unity side.
    [Serializable]
    public struct JointLimit
    {
        public string name;
        public float minDeg;
        public float maxDeg;
        public float maxVelDegSec;
        public float maxTorqueNm;
    }

    [CreateAssetMenu(menuName = "MetaMove/Robot/Joint Limits", fileName = "JointLimits_GoFa5_95")]
    public class JointLimits : ScriptableObject
    {
        public JointLimit[] joints = DefaultGoFa5_95();

        public static JointLimit[] DefaultGoFa5_95() => new[]
        {
            new JointLimit { name = "joint_1", minDeg = -180f, maxDeg = 180f, maxVelDegSec = 125f, maxTorqueNm = 80f },
            new JointLimit { name = "joint_2", minDeg = -95f,  maxDeg = 155f, maxVelDegSec = 125f, maxTorqueNm = 80f },
            new JointLimit { name = "joint_3", minDeg = -180f, maxDeg = 70f,  maxVelDegSec = 140f, maxTorqueNm = 40f },
            new JointLimit { name = "joint_4", minDeg = -180f, maxDeg = 180f, maxVelDegSec = 200f, maxTorqueNm = 15f },
            new JointLimit { name = "joint_5", minDeg = -135f, maxDeg = 135f, maxVelDegSec = 200f, maxTorqueNm = 15f },
            new JointLimit { name = "joint_6", minDeg = -400f, maxDeg = 400f, maxVelDegSec = 400f, maxTorqueNm = 6f  },
        };

        public JointLimit this[int i] => joints[i];
        public int Count => joints.Length;
    }
}
