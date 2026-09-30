using System.Collections.Generic;
using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // A parts tray as a work object (ABB: wobj). The nests are grasp frames in the
    // tray's own frame, so re-teaching the tray's location re-teaches every nest at
    // once - which is exactly what the visitor does with one coarse pinch in beat 6.
    //
    // Each nest holds at most one part. The part GameObjects are owned by the tray;
    // KittingCell borrows one while it carries it and hands it back on Refill().
    public class KittingTray : MonoBehaviour
    {
        public string label = "Tray A";

        [Tooltip("Grasp frames, children of this tray. Order = pick order.")]
        public List<Transform> nests = new List<Transform>();

        [Tooltip("One part per nest, same order. Null = empty nest.")]
        public List<GameObject> parts = new List<GameObject>();

        [Tooltip("How many nests are filled on Refill(). -1 = all.")]
        public int fillCount = -1;

        [Tooltip("Height of the grasp point above a part's pivot (its foot). Gears are gripped at the hub.")]
        public float graspHeight = 0.03f;

        [Tooltip("Shown while the robot has located this tray (fine alignment done).")]
        public GameObject recognizedHighlight;

        public int Remaining
        {
            get
            {
                int n = 0;
                for (int i = 0; i < parts.Count; i++)
                    if (parts[i] != null && parts[i].activeSelf && parts[i].transform.parent == nests[i]) n++;
                return n;
            }
        }

        // Next nest that still holds its part, or -1.
        public int NextFilled()
        {
            for (int i = 0; i < nests.Count && i < parts.Count; i++)
                if (parts[i] != null && parts[i].activeSelf && parts[i].transform.parent == nests[i]) return i;
            return -1;
        }

        // First nest in pick order, used by the robot's "probe" at a tray it believes
        // is still there. Returns the nest even when it is empty.
        public Transform FirstNest => nests.Count > 0 ? nests[0] : transform;

        public Vector3 Center => transform.position;

        public void Refill()
        {
            int fill = fillCount < 0 ? parts.Count : Mathf.Min(fillCount, parts.Count);
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p == null) continue;
                p.transform.SetParent(nests[i], false);
                p.transform.localPosition = Vector3.zero;
                p.transform.localRotation = Quaternion.identity;
                p.SetActive(i < fill);
            }
        }

        public void SetRecognized(bool on)
        {
            if (recognizedHighlight != null) recognizedHighlight.SetActive(on);
        }
    }
}
