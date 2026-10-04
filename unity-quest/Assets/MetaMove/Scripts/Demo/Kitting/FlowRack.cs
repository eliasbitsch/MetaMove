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
        [Tooltip("Lanes taken by stock boxes of the scenery, on both levels: never offered to the visitor.")]
        public bool[] blocked;

        [Tooltip("Carried boxes released inside this box (rack local) count as 'set on a lane'.")]
        public Bounds loadZone = new Bounds(new Vector3(0f, 1.05f, -0.40f), new Vector3(1.20f, 0.40f, 0.45f));

        public float rollAccel = 0.8f;     // m/s^2 down the lane
        public float rollMaxSpeed = 0.6f;  // m/s

        public int LaneCount => picks != null ? picks.Length : 0;

        public bool InLoadZone(Vector3 world) => loadZone.Contains(transform.InverseTransformPoint(world));

        // Lane closest to a world position, skipping occupied lanes.
        public int NearestLane(Vector3 world, params KittingTray[] occupants) => NearestLane(world, false, occupants);

        // ignoreBlocked: freestyle - every lane is the visitor's, as long as no box stands in it
        public int NearestLane(Vector3 world, bool ignoreBlocked, params KittingTray[] occupants)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < LaneCount; i++)
            {
                if (IsOccupied(i, occupants) || (!ignoreBlocked && IsBlocked(i))) continue;
                var local = transform.InverseTransformPoint(world);
                var lane = transform.InverseTransformPoint(entries[i].position);
                float d = Mathf.Abs(local.x - lane.x);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public bool IsBlocked(int lane) => blocked != null && lane < blocked.Length && blocked[lane];

        // Lower level (return lanes): nearest free one.
        public int NearestReturnLane(Vector3 world, params KittingTray[] occupants)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < LaneCount; i++)
            {
                if (IsOccupied(i, occupants, returns)) continue;
                float d = Mathf.Abs(transform.InverseTransformPoint(world).x - transform.InverseTransformPoint(returns[i].position).x);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // Released below the middle between the two levels: it is meant for the lower one.
        public bool IsLowerLevel(Vector3 world) => world.y < (returns[0].position.y + picks[0].position.y) * 0.5f;

        bool IsOccupied(int lane, KittingTray[] occupants) => IsOccupied(lane, occupants, picks);

        bool IsOccupied(int lane, KittingTray[] occupants, Transform[] level)
        {
            if (occupants == null) return false;
            foreach (var t in occupants)
            {
                if (t == null) continue;
                var local = level[lane].InverseTransformPoint(t.transform.position);
                if (Mathf.Abs(local.x) < 0.15f && local.z > -0.9f && local.z < 0.9f && Mathf.Abs(local.y) < 0.2f) return true;
            }
            return false;
        }

        // Box set on at 'from', rolls to 'to' along the lane with gravity-like acceleration.
        // A box let go by hand at the rack: into the target lane without cutting through the
        // rollers (see below).
        public IEnumerator RollFromOutside(Transform box, Transform to)
        {
            var outward = to.position - transform.position; outward.y = 0f;
            outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : -transform.forward;
            var front = to.position + outward * 0.35f;
            // Let go in the rack on the level it is meant for: it just rolls in. Otherwise it
            // comes out to the front at the height it was let go, then up or down to the lane's
            // mouth and in - never through a level, never in an arc over the rack.
            var l = transform.InverseTransformPoint(box.position);
            bool inside = Mathf.Abs(l.x) < 0.65f && Mathf.Abs(l.z) < 0.45f;
            bool sameLevel = IsLowerLevel(box.position) == IsLowerLevel(to.position);
            var pts = inside && sameLevel ? new Vector3[0]
                : new[] { new Vector3(front.x, box.position.y, front.z), front };
            foreach (var p in pts)
            {
                while ((box.position - p).sqrMagnitude > 1e-6f)
                {
                    box.position = Vector3.MoveTowards(box.position, p, rollMaxSpeed * Time.deltaTime);
                    box.rotation = Quaternion.RotateTowards(box.rotation, to.rotation, 120f * Time.deltaTime);
                    yield return null;
                }
            }
            yield return Roll(box, null, to);
        }

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
