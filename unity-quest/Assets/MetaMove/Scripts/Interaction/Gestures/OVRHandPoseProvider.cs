// Meta XR adapter that implements IHandPoseProvider on top of the Oculus
// Interaction SDK (`Oculus.Interaction.Input.IHand`).
//
// The file is guarded by the METAMOVE_META_SDK scripting define symbol so the
// rest of the gesture stack compiles before the SDK is imported. After
// Package-Manager-importing `com.meta.xr.sdk.interaction`:
//   Project Settings → Player → Scripting Define Symbols → add METAMOVE_META_SDK
// …and this file becomes active.
#if METAMOVE_META_SDK
using Oculus.Interaction.Input;
using UnityEngine;

namespace MetaMove.Interaction.Gestures
{
    public class OVRHandPoseProvider : MonoBehaviour, IHandPoseProvider
    {
        [Tooltip("IHand component for the left hand (wire the Meta LeftHand Synthetic or LeftHand).")]
        public Hand leftHandRef;
        public Hand rightHandRef;
        public Transform headTransform;

        Vector3 _lastLeft, _lastRight;
        Vector3 _velLeft, _velRight;
        bool _initialised;
        readonly float[] _tmpCurl = new float[5];

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            if (_initialised)
            {
                if (leftHandRef && leftHandRef.GetRootPose(out var lp))
                    _velLeft = Vector3.Lerp(_velLeft, (lp.position - _lastLeft) / dt, 0.5f);
                if (rightHandRef && rightHandRef.GetRootPose(out var rp))
                    _velRight = Vector3.Lerp(_velRight, (rp.position - _lastRight) / dt, 0.5f);
            }
            if (leftHandRef && leftHandRef.GetRootPose(out var lp2)) _lastLeft = lp2.position;
            if (rightHandRef && rightHandRef.GetRootPose(out var rp2)) _lastRight = rp2.position;
            _initialised = true;
        }

        Hand HandRef(GestureRouter.Hand h) => h == GestureRouter.Hand.Left ? leftHandRef : rightHandRef;

        public bool IsTracked(GestureRouter.Hand h)
        {
            var hr = HandRef(h);
            return hr != null && hr.IsTrackedDataValid;
        }

        public Vector3 PalmPosition(GestureRouter.Hand h)
        {
            var hr = HandRef(h);
            if (hr != null && hr.GetJointPose(HandJointId.HandPalm, out var pose)) return pose.position;
            if (hr != null && hr.GetRootPose(out var root)) return root.position;
            return Vector3.zero;
        }

        public Vector3 PalmNormal(GestureRouter.Hand h)
        {
            var hr = HandRef(h);
            if (hr != null && hr.GetJointPose(HandJointId.HandPalm, out var pose))
            {
                // In Meta's hand rig the palm's local -Y axis points out of the
                // palm for both hands (follows the OpenXR convention). Adjust
                // here if a later SDK update changes the convention.
                return pose.rotation * Vector3.down;
            }
            return Vector3.up;
        }

        public Vector3 PalmVelocity(GestureRouter.Hand h) =>
            h == GestureRouter.Hand.Left ? _velLeft : _velRight;

        public void GetFingerCurl(GestureRouter.Hand h, float[] outCurl5)
        {
            // Approximate curl as the cosine between the proximal and distal
            // finger direction. 0 = extended, 1 = curled. Good enough for
            // beckon/fist classification; refine later with finger-state from
            // ShapeRecognition if needed.
            var hr = HandRef(h);
            for (int i = 0; i < 5 && i < outCurl5.Length; i++) outCurl5[i] = 0f;
            if (hr == null) return;

            _tmpCurl[0] = FingerCurl(hr, HandJointId.HandThumb1, HandJointId.HandThumb3);
            _tmpCurl[1] = FingerCurl(hr, HandJointId.HandIndex1, HandJointId.HandIndex3);
            _tmpCurl[2] = FingerCurl(hr, HandJointId.HandMiddle1, HandJointId.HandMiddle3);
            _tmpCurl[3] = FingerCurl(hr, HandJointId.HandRing1, HandJointId.HandRing3);
            _tmpCurl[4] = FingerCurl(hr, HandJointId.HandPinky1, HandJointId.HandPinky3);
            for (int i = 0; i < 5 && i < outCurl5.Length; i++) outCurl5[i] = _tmpCurl[i];
        }

        static float FingerCurl(Hand hr, HandJointId proximal, HandJointId distal)
        {
            if (!hr.GetJointPose(proximal, out var p)) return 0f;
            if (!hr.GetJointPose(distal, out var d)) return 0f;
            Vector3 pf = p.rotation * Vector3.forward;
            Vector3 df = d.rotation * Vector3.forward;
            float dot = Vector3.Dot(pf, df); // 1 = extended, -1 = fully curled
            return Mathf.Clamp01((1f - dot) * 0.5f);
        }

        public Vector3 HeadPosition => headTransform ? headTransform.position : Vector3.zero;
        public Vector3 HeadForward => headTransform ? headTransform.forward : Vector3.forward;
    }
}
#endif
