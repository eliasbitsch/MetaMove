using System.Collections;
using Oculus.Interaction;
using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // A KLT the visitor really picks up and carries.
    //
    // Supply (tray B): starts on the trolley ~3 m out. Released on the loading side of the
    //   flow rack it is set onto the nearest free supply lane and rolls forward to the stop;
    //   then the cell knows a new box has arrived.
    // Empty (tray A): grabbable only once the robot has given up on it. The visitor takes it
    //   out at the pick face - hands inside dNear, the arm stands still - and released near
    //   the rack it goes onto the return lane.
    //
    // Released anywhere else the box drops with gravity, lands, and after a moment fades back
    // in where it belongs: a supply box on its trolley, an empty on the return lane. It can be
    // caught again while it falls. Once it has arrived it is locked until Restart.
    [RequireComponent(typeof(Grabbable))]
    public class CarryableTray : MonoBehaviour
    {
        public enum Kind { Supply, Empty }
        public Kind kind = Kind.Supply;

        public KittingCell cell;
        public FlowRack rack;
        [Tooltip("Where the box waits at Restart (trolley for B, its lane for A).")]
        public Transform startPose;
        [Tooltip("An empty released this close to the rack (m, horizontal) goes straight onto the return lane.")]
        public float returnReach = 0.7f;
        [Tooltip("Seconds a dropped box lies on the floor before it comes back.")]
        public float respawnDelay = 1.5f;
        [Tooltip("Grab interactables; enabled only while the box may be carried.")]
        public Behaviour[] interactables;

        public enum Phase { Waiting, Held, Falling, Respawning, Rolling, Arrived }
        public Phase State { get; private set; } = Phase.Waiting;
        public int Lane { get; private set; } = -1;

        Grabbable _grabbable;
        Rigidbody _rb;
        float _fallStart, _restSince;

        void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            _rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            // Empties become grabbable only when the robot is done with them.
            if (kind == Kind.Empty && State == Phase.Waiting)
                SetGrabbable(cell != null && cell.AllowEmptyRemoval);

            bool held = _grabbable.SelectingPointsCount > 0;
            if (held && (State == Phase.Waiting || State == Phase.Falling))
            {
                State = Phase.Held;
                Debug.Log("[Kitting] " + name + " picked up");
            }
            else if (!held && State == Phase.Held)
            {
                OnReleased();
            }

            if (State == Phase.Falling) WatchFall();
        }

        void OnReleased()
        {
            if (kind == Kind.Empty)
            {
                if (rack != null && NearRack()) { SendToReturn(); return; }
                Drop();
                return;
            }
            if (rack != null && rack.InLoadZone(transform.position))
            {
                LoadIntoRack();
                return;
            }
            Drop();
        }

        bool NearRack()
        {
            var local = rack.transform.InverseTransformPoint(transform.position);
            return Mathf.Abs(local.x) < 0.6f + returnReach && Mathf.Abs(local.z) < 0.4f + returnReach;
        }

        // ---------------------------------------------------------------- physics drop

        void Drop()
        {
            State = Phase.Falling;
            _fallStart = Time.time; _restSince = -1f;
            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
                _rb.WakeUp();
            }
            Debug.Log("[Kitting] " + name + " dropped");
        }

        void WatchFall()
        {
            bool resting = _rb == null || _rb.IsSleeping() || _rb.linearVelocity.sqrMagnitude < 0.0025f;
            if (resting && _restSince < 0f) _restSince = Time.time;
            if (!resting) _restSince = -1f;
            bool lost = cell != null && cell.cellFrame != null && transform.position.y < cell.cellFrame.position.y - 2f;
            if ((_restSince > 0f && Time.time - _restSince > respawnDelay) || Time.time - _fallStart > 6f || lost)
                StartCoroutine(Respawn());
        }

        IEnumerator Respawn()
        {
            State = Phase.Respawning;
            Freeze();
            SetGrabbable(false);
            // shrink away where it lies, grow back where it belongs
            yield return Scale(1f, 0f, 0.25f);
            if (kind == Kind.Empty && rack != null)
            {
                int lane = ReturnLane();
                transform.SetPositionAndRotation(rack.returns[lane].position, rack.returns[lane].rotation);
                yield return Scale(0f, 1f, 0.3f);
                State = Phase.Arrived;
                Debug.Log("[Kitting] " + name + " respawned on the return lane");
                yield break;
            }
            if (startPose != null) transform.SetPositionAndRotation(startPose.position, startPose.rotation);
            yield return Scale(0f, 1f, 0.3f);
            State = Phase.Waiting;
            SetGrabbable(true);
            Debug.Log("[Kitting] " + name + " respawned at its start");
        }

        IEnumerator Scale(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                transform.localScale = Vector3.one * Mathf.SmoothStep(from, to, t / seconds);
                yield return null;
            }
            transform.localScale = Vector3.one * to;
        }

        void Freeze()
        {
            if (_rb == null) return;
            if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }

        // ---------------------------------------------------------------- rack

        // Set the box onto the nearest free supply lane and let it roll to the stop.
        // Also used by the presenter's fallback button.
        public void LoadIntoRack()
        {
            if (rack == null) return;
            int lane = rack.NearestLane(transform.position, cell != null ? cell.trayA : null);
            if (lane < 0) { Drop(); return; }
            Lane = lane;
            Freeze();
            StartCoroutine(RollTo(rack.entries[lane], rack.picks[lane], true));
        }

        // The empty onto the return lane, gliding from where it is. Presenter fallback too.
        public void SendToReturn()
        {
            if (rack == null) return;
            Freeze();
            Lane = ReturnLane();
            StartCoroutine(RollTo(null, rack.returns[Lane], false));
        }

        int ReturnLane()
        {
            int lane = rack.NearestLane(transform.position);
            return lane < 0 ? 1 : lane;
        }

        IEnumerator RollTo(Transform from, Transform to, bool notifyCell)
        {
            State = Phase.Rolling;
            SetGrabbable(false);
            Debug.Log("[Kitting] " + name + " rolling to " + to.name);
            yield return rack.Roll(transform, from, to);
            State = Phase.Arrived;
            if (notifyCell && cell != null) cell.PlaceTrayB(null);
        }

        // Placed directly (no rack): lock it where it is.
        public void MarkPlaced()
        {
            Freeze();
            State = Phase.Arrived;
            SetGrabbable(false);
        }

        // Called by KittingCell.Restart after it has moved the box back.
        public void ResetToStart()
        {
            StopAllCoroutines();
            Freeze();
            transform.localScale = Vector3.one;
            if (startPose != null) transform.SetPositionAndRotation(startPose.position, startPose.rotation);
            State = Phase.Waiting;
            Lane = -1;
            SetGrabbable(kind == Kind.Supply);
        }

        void SetGrabbable(bool on)
        {
            if (interactables == null) return;
            foreach (var b in interactables) if (b != null && b.enabled != on) b.enabled = on;
        }
    }
}
