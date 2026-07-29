using System.Collections;
using UnityEngine;

namespace MetaMove.Demo
{
    /// <summary>
    /// Places the robot in the room without any QR marker: a short delay after
    /// start (so head tracking has settled) the rig is dropped on the floor a
    /// fixed distance in front of wherever the user is looking.
    ///
    /// Re-callable at runtime via <see cref="PlaceNow"/> — wire it to a button
    /// or call it from the inspector if the robot ends up inside a wall.
    /// </summary>
    public class DemoRobotPlacer : MonoBehaviour
    {
        [Tooltip("Head transform. Defaults to CenterEyeAnchor / Camera.main.")]
        public Transform head;

        [Tooltip("Distance (m) in front of the user along the horizontal view direction.")]
        public float distance = 1.30f;

        [Tooltip("Assumed eye height (m). Floor = head height minus this.")]
        public float eyeHeight = 1.35f;

        [Tooltip("Extra yaw (deg) applied on top of the user's facing direction.")]
        public float yawOffsetDeg = 0f;

        [Tooltip("Seconds to wait after start before placing — lets tracking settle.")]
        public float placeDelay = 0.75f;

        public bool placeOnStart = true;

        IEnumerator Start()
        {
            if (!placeOnStart) yield break;
            yield return new WaitForSeconds(placeDelay);
            PlaceNow();
        }

        [ContextMenu("Place in front of user")]
        public void PlaceNow()
        {
            var h = ResolveHead();
            if (h == null)
            {
                Debug.LogWarning("[DemoRobotPlacer] No head transform found — leaving robot where it is.");
                return;
            }

            Vector3 forward = h.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 pos = h.position + forward * distance;
            pos.y = h.position.y - eyeHeight;

            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, Quaternion.LookRotation(forward).eulerAngles.y + yawOffsetDeg, 0f);
        }

        Transform ResolveHead()
        {
            if (head != null) return head;
            var anchor = GameObject.Find("CenterEyeAnchor");
            if (anchor != null) { head = anchor.transform; return head; }
            if (Camera.main != null) { head = Camera.main.transform; return head; }
            return null;
        }
    }
}
