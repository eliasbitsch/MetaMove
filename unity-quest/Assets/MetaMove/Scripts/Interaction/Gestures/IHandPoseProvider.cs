using UnityEngine;

namespace MetaMove.Interaction.Gestures
{
    // SDK-free pose sampling contract. Meta XR / OVR adapter implements this by
    // reading Oculus.Interaction.Input.IHand; editor mock implements it from keyboard
    // or a driven Transform. Consumers (Swipe, Beckon, HoldStop, SpatialPinch)
    // depend only on this interface so the gesture stack compiles without Meta SDK.
    public interface IHandPoseProvider
    {
        bool IsTracked(GestureRouter.Hand hand);

        // World-space palm centre position (metres).
        Vector3 PalmPosition(GestureRouter.Hand hand);

        // World-space palm normal — points OUT of the palm, away from fingers.
        // Example: flat right hand held with fingers pointing forward, palm facing
        // down → normal = (0, -1, 0). Palm facing the user → normal points at the
        // user's head.
        Vector3 PalmNormal(GestureRouter.Hand hand);

        // World-space palm centre velocity (m/s), smoothed by the provider.
        Vector3 PalmVelocity(GestureRouter.Hand hand);

        // Curl amount 0..1 for each finger: 0 = fully extended, 1 = fully closed.
        // Order: thumb, index, middle, ring, little.
        void GetFingerCurl(GestureRouter.Hand hand, float[] outCurl5);

        // Head / centre-eye position and forward. Used by gesture code that needs
        // the user's reference frame (e.g. "palm towards user").
        Vector3 HeadPosition { get; }
        Vector3 HeadForward { get; }
    }
}
