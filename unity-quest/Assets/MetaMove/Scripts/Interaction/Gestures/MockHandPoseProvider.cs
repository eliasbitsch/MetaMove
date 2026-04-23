using UnityEngine;

namespace MetaMove.Interaction.Gestures
{
    // Editor-friendly IHandPoseProvider that reads two child Transforms as the
    // hands. Useful for testing the gesture controllers (Swipe, Beckon,
    // HoldStop, SpatialPinch) without the Meta SDK on a desktop.
    //
    // Drag two empties under the rig, set them as LeftHand / RightHand here,
    // animate their position + rotation in the scene — the controllers react.
    // Velocity is differentiated from successive Transform samples.
    //
    // Finger curl is driven by the per-hand float arrays below. Hook up keyboard
    // bindings in MockHandKeyboardDriver for interactive tests.
    public class MockHandPoseProvider : MonoBehaviour, IHandPoseProvider
    {
        public Transform leftHand;
        public Transform rightHand;
        public Transform head;

        [Header("Curl (thumb, index, middle, ring, little) 0=open 1=closed")]
        public float[] leftCurl = new float[5];
        public float[] rightCurl = new float[5];
        public bool leftTracked = true;
        public bool rightTracked = true;

        Vector3 _lastLeft, _lastRight;
        Vector3 _velLeft, _velRight;
        bool _initialised;

        void Reset() { if (head == null && Camera.main != null) head = Camera.main.transform; }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            if (_initialised)
            {
                if (leftHand) _velLeft = Vector3.Lerp(_velLeft, (leftHand.position - _lastLeft) / dt, 0.5f);
                if (rightHand) _velRight = Vector3.Lerp(_velRight, (rightHand.position - _lastRight) / dt, 0.5f);
            }
            if (leftHand) _lastLeft = leftHand.position;
            if (rightHand) _lastRight = rightHand.position;
            _initialised = true;
        }

        public bool IsTracked(GestureRouter.Hand h) =>
            h == GestureRouter.Hand.Left ? (leftTracked && leftHand != null)
                                         : (rightTracked && rightHand != null);

        public Vector3 PalmPosition(GestureRouter.Hand h) =>
            (h == GestureRouter.Hand.Left ? leftHand : rightHand)?.position ?? Vector3.zero;

        // Convention: the Transform's up (+Y) points out of the palm.
        public Vector3 PalmNormal(GestureRouter.Hand h) =>
            (h == GestureRouter.Hand.Left ? leftHand : rightHand)?.up ?? Vector3.up;

        public Vector3 PalmVelocity(GestureRouter.Hand h) =>
            h == GestureRouter.Hand.Left ? _velLeft : _velRight;

        public void GetFingerCurl(GestureRouter.Hand h, float[] outCurl5)
        {
            var src = h == GestureRouter.Hand.Left ? leftCurl : rightCurl;
            for (int i = 0; i < 5 && i < outCurl5.Length; i++) outCurl5[i] = src != null && i < src.Length ? src[i] : 0f;
        }

        public Vector3 HeadPosition => head ? head.position : Vector3.zero;
        public Vector3 HeadForward => head ? head.forward : Vector3.forward;
    }
}
