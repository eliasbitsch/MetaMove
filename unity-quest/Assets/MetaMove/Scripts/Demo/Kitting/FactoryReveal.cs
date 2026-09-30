using System.Collections;
using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // The moment the cell is placed: the slab of factory floor spreads out from under the
    // robot, the wall rises out of the ground and the hall beam lowers in from above.
    // Runs once each time the cell frame becomes active (it stays hidden until placement).
    public class FactoryReveal : MonoBehaviour
    {
        public Transform floorIsland, wall, beam, portalMask;
        public float floorTime = 0.9f, wallTime = 0.8f, beamTime = 0.7f;

        void OnEnable() => StartCoroutine(Reveal());

        IEnumerator Reveal()
        {
            Vector3 wallPos = wall != null ? wall.localPosition : Vector3.zero;
            Vector3 beamPos = beam != null ? beam.localPosition : Vector3.zero;
            Vector3 maskPos = portalMask != null ? portalMask.localPosition : Vector3.zero;
            if (floorIsland != null) floorIsland.localScale = new Vector3(0.001f, 1f, 0.001f);
            if (wall != null) wall.localPosition = wallPos + Vector3.down * 2.9f;
            if (portalMask != null) portalMask.localPosition = maskPos + Vector3.down * 2.9f;
            if (beam != null) beam.localPosition = beamPos + Vector3.up * 1.5f;

            for (float t = 0f; t < 1f; t += Time.deltaTime / floorTime)
            {
                float e = 1f - Mathf.Pow(1f - t, 3f);            // ease out
                if (floorIsland != null) floorIsland.localScale = new Vector3(e, 1f, e);
                yield return null;
            }
            if (floorIsland != null) floorIsland.localScale = Vector3.one;

            for (float t = 0f; t < 1f; t += Time.deltaTime / wallTime)
            {
                float e = 1f - Mathf.Pow(1f - t, 3f);
                if (wall != null) wall.localPosition = wallPos + Vector3.down * 2.9f * (1f - e);
                if (portalMask != null) portalMask.localPosition = maskPos + Vector3.down * 2.9f * (1f - e);
                yield return null;
            }
            if (wall != null) wall.localPosition = wallPos;
            if (portalMask != null) portalMask.localPosition = maskPos;

            for (float t = 0f; t < 1f; t += Time.deltaTime / beamTime)
            {
                float e = 1f - Mathf.Pow(1f - t, 2f);
                if (beam != null) beam.localPosition = beamPos + Vector3.up * 1.5f * (1f - e);
                yield return null;
            }
            if (beam != null) beam.localPosition = beamPos;
        }
    }
}
