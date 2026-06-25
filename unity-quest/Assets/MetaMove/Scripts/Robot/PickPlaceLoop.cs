using System.Collections.Generic;
using UnityEngine;
using MetaMove.Safety;

namespace MetaMove.Robot
{
    // Drives an IK target Transform through a pick & place waypoint loop. The
    // GoFa IK solver (GoFaCCDIK / GoFaDLSIK) already chases this target, so the
    // virtual robot follows automatically.
    //
    // Motion speed is multiplied by ProximitySpeedController.Factor (0..1):
    // human far -> full speed, human close -> freezes mid-motion. Optionally
    // carries a "held" object between the pick and place waypoints for a nice
    // visual.
    public class PickPlaceLoop : MonoBehaviour
    {
        [Header("Driven IK target")]
        public Transform target;                 // the IK solver's target
        public ProximitySpeedController speed;    // speed factor source (optional)

        [Header("Waypoints (looped in order)")]
        public List<Transform> waypoints = new List<Transform>();

        [Header("Motion")]
        public float baseSpeed = 0.35f;          // m/s at factor 1
        public float rotSpeed = 180f;            // deg/s at factor 1
        public float dwell = 0.4f;               // s pause at each waypoint
        public float arriveTol = 0.005f;         // m

        [Header("Held object (optional)")]
        public Transform heldObject;
        [Tooltip("Index where the object is grasped.")]
        public int pickIndex = 2;
        [Tooltip("Index where the object is released.")]
        public int placeIndex = 5;

        int _i = 0;
        float _dwellT = 0f;
        bool _holding = false;

        void Update()
        {
            if (target == null || waypoints == null || waypoints.Count < 2) return;
            var wp = waypoints[_i];
            if (wp == null) { _i = (_i + 1) % waypoints.Count; return; }

            float f = speed != null ? Mathf.Clamp01(speed.Factor) : 1f;

            target.position = Vector3.MoveTowards(target.position, wp.position, baseSpeed * f * Time.deltaTime);
            target.rotation = Quaternion.RotateTowards(target.rotation, wp.rotation, rotSpeed * f * Time.deltaTime);

            if (heldObject != null && _holding)
            {
                heldObject.position = target.position;
                heldObject.rotation = target.rotation;
            }

            if (Vector3.Distance(target.position, wp.position) <= arriveTol)
            {
                // dwell scaled by factor too -> fully frozen when human is close
                _dwellT += Time.deltaTime * f;
                if (_dwellT >= dwell)
                {
                    _dwellT = 0f;
                    OnArrive(_i);
                    _i = (_i + 1) % waypoints.Count;
                }
            }
        }

        void OnArrive(int idx)
        {
            if (heldObject == null) return;
            if (idx == pickIndex) _holding = true;
            else if (idx == placeIndex) _holding = false;
        }
    }
}
