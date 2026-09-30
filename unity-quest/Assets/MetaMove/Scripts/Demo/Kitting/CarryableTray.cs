using System.Collections;
using Oculus.Interaction;
using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // A KLT the visitor really picks up and carries.
    //
    // Supply (tray B, beat 3): starts on the trolley ~3 m out. Released on the loading
    //   side of the flow rack it is set onto the nearest free supply lane and rolls
    //   forward to the stop; then the cell knows a new box has arrived.
    // Empty (tray A, beat 3b): grabbable only once the robot has given up on it. The
    //   visitor takes it out at the pick face - hands inside dNear, the arm stands still -
    //   and wherever it is released it goes onto the return lane as an empty.
    //
    // Released anywhere else, a supply box hovers for a few seconds (a lost hand track
    // looks like a release) and can be grabbed again, then glides back to where it came
    // from. Once it has arrived it is locked until Restart.
    [RequireComponent(typeof(Grabbable))]
    public class CarryableTray : MonoBehaviour
    {
        public enum Kind { Supply, Empty }
        public Kind kind = Kind.Supply;

        public KittingCell cell;
        public FlowRack rack;
        [Tooltip("Where the box waits at Restart (trolley for B, its lane for A).")]
        public Transform startPose;
        public float hoverSeconds = 4f;
        public float returnSpeed = 1.2f;   // m/s
        [Tooltip("Grab interactables; enabled only while the box may be carried.")]
        public Behaviour[] interactables;

        public enum Phase { Waiting, Held, Hovering, Returning, Rolling, Arrived }
        public Phase State { get; private set; } = Phase.Waiting;
        public int Lane { get; private set; } = -1;

        Grabbable _grabbable;
        float _releasedAt;

        void Awake() => _grabbable = GetComponent<Grabbable>();

        void Update()
        {
            // Empties become grabbable only when the robot is done with them.
            if (kind == Kind.Empty && State == Phase.Waiting)
                SetGrabbable(cell != null && cell.AllowEmptyRemoval);

            bool held = _grabbable.SelectingPointsCount > 0;
            if (held && (State == Phase.Waiting || State == Phase.Hovering || State == Phase.Returning))
            {
                State = Phase.Held;
                Debug.Log("[Kitting] " + name + " picked up");
            }
            else if (!held && State == Phase.Held)
            {
                OnReleased();
            }

            if (State == Phase.Hovering && Time.time - _releasedAt > hoverSeconds) State = Phase.Returning;
            if (State == Phase.Returning && startPose != null)
            {
                transform.position = Vector3.MoveTowards(transform.position, startPose.position, returnSpeed * Time.deltaTime);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, startPose.rotation, 180f * Time.deltaTime);
                if ((transform.position - startPose.position).sqrMagnitude < 1e-6f) State = Phase.Waiting;
            }
        }

        void OnReleased()
        {
            if (kind == Kind.Empty)
            {
                // An empty goes onto the return lane, whatever the visitor does with it.
                int lane = rack != null ? rack.NearestLane(transform.position) : -1;
                if (lane < 0) lane = 0;
                StartCoroutine(RollTo(null, rack.returns[lane], false));
                return;
            }
            if (rack != null && rack.InLoadZone(transform.position))
            {
                LoadIntoRack();
                return;
            }
            State = Phase.Hovering;
            _releasedAt = Time.time;
            Debug.Log("[Kitting] " + name + " released away from the rack - hovering, then back");
        }

        // Set the box onto the nearest free supply lane and let it roll to the stop.
        // Also used by the presenter's fallback button.
        public void LoadIntoRack()
        {
            if (rack == null) return;
            int lane = rack.NearestLane(transform.position, cell != null ? cell.trayA : null);
            if (lane < 0) { State = Phase.Hovering; _releasedAt = Time.time; return; }
            Lane = lane;
            StartCoroutine(RollTo(rack.entries[lane], rack.picks[lane], true));
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
            State = Phase.Arrived;
            SetGrabbable(false);
        }

        // Called by KittingCell.Restart after it has moved the box back.
        public void ResetToStart()
        {
            StopAllCoroutines();
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
