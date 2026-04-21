using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetaMove.Robot.Paths
{
    // Persistent container for a named robot path. A path is an ordered list of
    // waypoints, each a pose (position + rotation) in robot-base frame.
    // Stored as ScriptableObject so it can be edited in the editor and serialized.

    [Serializable]
    public struct Waypoint
    {
        public string label;
        public Vector3 positionMeters;     // in robot base frame
        public Vector3 eulerDeg;           // RPY
        [Range(0f, 1f)] public float ikScore;   // last-known IK solvability (0 = fail, 1 = ok)
    }

    [CreateAssetMenu(menuName = "MetaMove/Robot/Path", fileName = "NewPath")]
    public class PathData : ScriptableObject
    {
        public string displayName = "New Path";
        public List<Waypoint> waypoints = new();

        [Header("Execution")]
        [Range(0.01f, 1f)] public float speedFraction = 0.5f;
        public bool loop;

        public int Count => waypoints.Count;
        public Waypoint this[int i] => waypoints[i];
    }
}
