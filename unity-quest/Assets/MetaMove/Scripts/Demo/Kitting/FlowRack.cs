using System.Collections;
using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // Flow rack (Durchlaufregal) in front of the robot: one supply level with three
    // box lanes that slope down towards the robot, one return level for empties that
    // slopes back to the loading side. Boxes are loaded at the back and roll forward
    // to the stop, where the robot picks from them (FIFO, as on a real line).
    //
    // Per lane the builder places three markers, children of this rack:
    //   entries[i]  supply lane, loading end   (where a carried box is set on)
    //   picks[i]    supply lane, pick end      (box resting against the stop)
    //   returns[i]  return lane, loading end   (empties end up here)
    // Marker up = lane surface normal, forward = downhill.
    public class FlowRack : MonoBehaviour
    {
        public Transform[] entries;
        public Transform[] picks;
        public Transform[] returns;

        [Tooltip("Carried boxes released inside this box (rack local) count as 'set on a lane'.")]
        public Bounds loadZone = new Bounds(new Vector3(0f, 1.05f, -0.40f), new Vector3(1.20f, 0.40f, 0.45f));

        public float rollAccel = 0.8f;     // m/s^2 down the lane
        public float rollMaxSpeed = 0.6f;  // m/s

        public int LaneCount => picks != null ? picks.Length : 0;

        public bool InLoadZone(Vector3 world) => loadZone.Contains(transform.InverseTransformPoint(world));

        // Lane closest to a world position, skipping occupied lanes.
        public int NearestLane(Vector3 world, params KittingTray[] occupants)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < LaneCount; i++)
            {
                if (IsOccupied(i, occupants)) continue;
                var local = transform.InverseTransformPoint(world);
                var lane = transform.InverseTransformPoint(entries[i].position);
                float d = Mathf.Abs(local.x - lane.x);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        bool IsOccupied(int lane, KittingTray[] occupants)
        {
            if (occupants == null) return false;
            foreach (var t in occupants)
            {
                if (t == null) continue;
                var local = picks[lane].InverseTransformPoint(t.transform.position);
                if (Mathf.Abs(local.x) < 0.15f && local.z > -0.9f && local.z < 0.9f && Mathf.Abs(local.y) < 0.2f) return true;
            }
            return false;
        }

        // Box set on at 'from', rolls to 'to' along the lane with gravity-like acceleration.
        public IEnumerator Roll(Transform box, Transform from, Transform to)
        {
            if (from != null) box.SetPositionAndRotation(from.position, from.rotation);   // null: glide from where it is
            float v = 0f;
            while (true)
            {
                float d = Vector3.Distance(box.position, to.position);
                v = Mathf.Min(v + rollAccel * Time.deltaTime, rollMaxSpeed);
                box.position = Vector3.MoveTowards(box.position, to.position, v * Time.deltaTime);
                box.rotation = Quaternion.RotateTowards(box.rotation, to.rotation, 90f * Time.deltaTime);
                if (d < 0.002f) break;
                yield return null;
            }
            box.SetPositionAndRotation(to.position, to.rotation);
        }
    }
}
