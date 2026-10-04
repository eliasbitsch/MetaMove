using System.Collections.Generic;
using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // Poses a static ghost hand (Interaction SDK OpenXRGhost-Hand prefab, rest pose: fingers
    // along the hand's +Z, palm facing -Y) by bending its finger joints:
    //   grip  0..1  all four fingers and the thumb close as around a handle
    //   pinch 0..1  index and thumb meet, the other fingers stay relaxed
    // Bends are applied about the hand's lateral axis on top of the rest pose every frame,
    // so the two can be animated freely. PinchPoint is where index and thumb tips meet.
    [ExecuteAlways]
    public class GhostHandPose : MonoBehaviour
    {
        [Range(0f, 1f)] public float grip;
        [Range(0f, 1f)] public float pinch;
        public float gripDegPerJoint = 55f, pinchIndexDeg = 32f, pinchThumbDeg = 28f;

        readonly List<Transform> _fingers = new List<Transform>();   // proximal..distal, all but thumb
        readonly List<Transform> _index = new List<Transform>();
        readonly List<Transform> _thumb = new List<Transform>();
        readonly Dictionary<Transform, Quaternion> _rest = new Dictionary<Transform, Quaternion>();
        Transform _wrist, _indexTip, _thumbTip;
        float _thumbSide = 1f;   // +1 right hand (thumb on -X), -1 left hand

        public Vector3 PinchPoint => _indexTip != null && _thumbTip != null ? (_indexTip.position + _thumbTip.position) * 0.5f : transform.position;

        void OnEnable() => Collect();

        void Collect()
        {
            _fingers.Clear(); _index.Clear(); _thumb.Clear(); _rest.Clear();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (!n.StartsWith("XRHand_")) continue;
                if (n == "XRHand_Wrist") _wrist = t;
                if (n == "XRHand_IndexTip") _indexTip = t;
                if (n == "XRHand_ThumbTip") _thumbTip = t;
                bool joint = n.EndsWith("Proximal") || n.EndsWith("Intermediate") || n.EndsWith("Distal");
                if (!joint) continue;
                _rest[t] = t.localRotation;
                if (n.StartsWith("XRHand_Thumb")) _thumb.Add(t);
                else _fingers.Add(t);
                if (n.StartsWith("XRHand_Index")) _index.Add(t);
            }
            // parents before children, so each bend builds on the one above
            _fingers.Sort((a, b) => Depth(a).CompareTo(Depth(b)));
            _thumb.Sort((a, b) => Depth(a).CompareTo(Depth(b)));
            if (_wrist != null && _thumbTip != null)
                _thumbSide = transform.InverseTransformPoint(_thumbTip.position).x < 0f ? 1f : -1f;
        }

        static int Depth(Transform t) { int d = 0; while (t.parent != null) { t = t.parent; d++; } return d; }

        void LateUpdate() => Apply();

        public void Apply()
        {
            if (_wrist == null) { Collect(); if (_wrist == null) return; }
            foreach (var kv in _rest) kv.Key.localRotation = kv.Value;

            // axes of the prefab root (the rest pose is authored in it): fingers +Z curl towards palm -Y
            var lateral = transform.TransformDirection(Vector3.right);
            var up = transform.TransformDirection(Vector3.up);
            foreach (var t in _fingers)
            {
                float a = gripDegPerJoint * grip;
                if (_index.Contains(t)) a = Mathf.Max(a, pinchIndexDeg * pinch);
                if (a > 0.01f) t.rotation = Quaternion.AngleAxis(a, lateral) * t.rotation;
            }
            foreach (var t in _thumb)
            {
                // the thumb swings across towards the index and curls a little
                float swing = pinchThumbDeg * pinch + 20f * grip;
                float curl = 12f * pinch + 30f * grip;
                t.rotation = Quaternion.AngleAxis(swing * _thumbSide, up) * Quaternion.AngleAxis(curl, lateral) * t.rotation;
            }
        }
    }
}
