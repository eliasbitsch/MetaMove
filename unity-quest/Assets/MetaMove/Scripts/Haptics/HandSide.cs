using UnityEngine;

namespace MetaMove.Haptics
{
    // Resolves which glove (Left/Right) performed an action by comparing the action's
    // world contact position to the OVR hand anchors. Lets a poke/grab buzz ONLY the
    // acting hand instead of both gloves.
    public static class HandSide
    {
        static Transform _left, _right;

        static void EnsureRefs()
        {
            if (_left == null)
            {
                var go = GameObject.Find("LeftHandAnchor");
                if (go != null) _left = go.transform;
            }
            if (_right == null)
            {
                var go = GameObject.Find("RightHandAnchor");
                if (go != null) _right = go.transform;
            }
        }

        // Glove whose anchor is closest to worldPos. Falls back to Both if the anchors
        // can't be resolved (so the user still feels something).
        public static BHapticsAdapter.Glove Nearest(Vector3 worldPos)
        {
            EnsureRefs();
            if (_left == null || _right == null) return BHapticsAdapter.Glove.Both;
            float dl = (worldPos - _left.position).sqrMagnitude;
            float dr = (worldPos - _right.position).sqrMagnitude;
            return dr <= dl ? BHapticsAdapter.Glove.Right : BHapticsAdapter.Glove.Left;
        }
    }
}
