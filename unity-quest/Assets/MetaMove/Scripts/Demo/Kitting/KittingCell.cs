using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using MetaMove.Safety;

namespace MetaMove.Demo.Kitting
{
    // Director of the Bari kitting demo. Drives the local IK target through
    // pick & place cycles from the currently taught tray into the kit box, and
    // runs the story around it:
    //
    //   Running   pick from the taught tray, place into the kit, repeat
    //   Probe     taught tray is empty: go there once anyway and grip air
    //   Waiting   no tray the robot knows has parts; "starved" until tray B arrives
    //   Guide     tray B is on the table but not taught: the visitor pinch-drags
    //             the TCP roughly over it (grab interactors enabled)
    //   Align     Teach pressed near a tray: the robot centres itself on it
    //   -> Running from the new tray
    //
    // All motion is scaled by ProximitySpeedController.Factor, including dwell
    // times, so the arm freezes mid-motion when a person is inside dNear.
    //
    // The director writes the IK target only outside Guide; in Guide the lab's
    // PhantomGrabRelay owns it, so the two never fight over the target.
    public class KittingCell : MonoBehaviour
    {
        public enum Phase { Idle, Running, Probe, Waiting, Guide, Align }

        [Header("Runtime binding")]
        [Tooltip("Spawns the robot; the cell binds to it on onAnchorSpawned.")]
        public QrAnchorCalibrator calibrator;
        [Tooltip("Root of table, trays and kit box. Re-parented under the robot on Bind.")]
        public Transform cellFrame;
        // Set by CellPlacer: which way the cell front (+Z, towards the visitor) points.
        // Unset, the front faces the head at the moment of binding.
        [System.NonSerialized] public Vector3? frontOverride;

        [Header("Robot")]
        [Tooltip("IK target the local solver chases. Found at runtime if empty.")]
        public Transform ikTarget;
        [Tooltip("Robot TCP, carried parts are parented here. Found at runtime if empty.")]
        public Transform tcp;
        public ProximitySpeedController speed;
        [Tooltip("Home pose, child of this cell.")]
        public Transform home;

        [Header("Cell")]
        public KittingTray trayA;
        public KittingTray trayB;
        [Tooltip("Where tray B waits before the visitor brings it (≈3 m away).")]
        public Transform trayBSpawn;
        [Tooltip("Where the editor shortcut puts tray B, standing in for the visitor.")]
        public Transform trayBPlaced;
        [Tooltip("Carries finished kits out of the cell (optional).")]
        public KitConveyor conveyor;
        [Tooltip("Kit box slots, filled in order and emptied when full.")]
        public List<Transform> kitSlots = new List<Transform>();

        [Header("Motion")]
        [Tooltip("TCP speed at factor 1. No max-speed limiter in the travel demo: the arm runs at a " +
                 "realistic cobot pick-and-place pace so the slow-down is obvious.")]
        public float baseSpeed = 0.9f;         // m/s at factor 1 (GoFa max 2.2)
        public float acceleration = 3.0f;      // m/s^2, ramps in and out of every waypoint
        public float approachHeight = 0.12f;   // m above the grasp point
        [Tooltip("Flange above the part centre at grasp, until a real gripper defines the TCP.")]
        public float toolOffset = 0.07f;
        public float gripDwell = 0.25f;        // s at factor 1
        public float arriveTol = 0.004f;

        [Header("Teach")]
        [Tooltip("Horizontal radius around a tray centre in which Teach counts as 'over the tray'.")]
        public float teachRadius = 0.15f;
        [Tooltip("Max height of the TCP above the tray for Teach to count.")]
        public float teachMaxHeight = 0.35f;

        [Header("Manual guiding (lab rig)")]
        [Tooltip("Grab components enabled only in Guide.")]
        public Behaviour[] grabInteractors;
        [Tooltip("Renderers shown only in Guide (grab handle).")]
        public Renderer[] guideOnlyRenderers;

        [Header("Editor / controller shortcuts")]
        public Key placeTrayKey = Key.R;        // stands in for the visitor carrying tray B
        public Key coarseGuideKey = Key.F;      // stands in for a coarse pinch over tray B
        public Key teachKey = Key.Enter;
        public Key restartKey = Key.Backspace;
        public OVRInput.Button teachButton = OVRInput.Button.Two;   // B until the fingertip menu exists
        [Tooltip("Fallback when a visitor cannot carry the tray: puts it down at trayBPlaced.")]
        public OVRInput.Button placeTrayButton = OVRInput.Button.Four;   // Y

        public Phase State { get; private set; } = Phase.Idle;
        public KittingTray Taught { get; private set; }
        public int KitsDone { get; private set; }
        public string Status { get; private set; } = "";
        public bool IsBound => _bound;

        // The empty tray A may be carried to the return lane once the robot no longer
        // needs it: after its failed probe, or once it runs from tray B.
        public bool AllowEmptyRemoval =>
            trayA != null && trayA.Remaining == 0 &&
            (State == Phase.Waiting || State == Phase.Guide || State == Phase.Align ||
             (State == Phase.Running && Taught == trayB));

        // Narration / HUD hook. Keys: low, starved, miss, guide, aligned, teach_fail, resume, restart.
        public event Action<string> Cue;

        Quaternion _graspRot;
        GameObject _held;
        int _slot;
        Coroutine _run;
        bool _trayBOnTable;
        bool _lowCued;
        bool _bound;
        MetaMove.Robot.RobotiqGripper _gripper;
        Pose? _aHome;
        Transform _holdAt;

        void OnEnable()
        {
            if (calibrator != null) calibrator.onAnchorSpawned.AddListener(Bind);
        }

        void Awake()
        {
            // Nothing of the cell shows until the robot has been placed (QR or by hand).
            if (calibrator != null && cellFrame != null && !_bound) cellFrame.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (calibrator != null) calibrator.onAnchorSpawned.RemoveListener(Bind);
        }

        // The robot is spawned at runtime by QrAnchorCalibrator (QR marker on the
        // device, editorAutoSpawn in the Editor). Hang the whole cell off it, so a
        // QR re-align moves table, trays and kit box together with the arm.
        public void Bind(GameObject robot)
        {
            var ik = robot != null ? robot.GetComponentInChildren<MetaMove.Robot.GoFaDLSIK>(true) : null;
            if (ik == null || ik.target == null || ik.endEffector == null)
            {
                Debug.LogError("[Kitting] spawned robot has no GoFaDLSIK with target + endEffector");
                return;
            }
            ikTarget = ik.target;
            tcp = ik.endEffector;
            var baseJoint = ik.joints[0].joint;

            // Cell front = towards the visitor, who stands in front of the robot when the
            // anchor is placed (QR scan or editor spawn). Falls back to the arm's rest reach.
            var head = Camera.main != null ? Camera.main.transform : null;
            Vector3 fwd = frontOverride ?? ((head != null ? head.position : tcp.position) - baseJoint.position);
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = ik.transform.forward;
            float surfaceY = ik.transform.position.y;   // robot foot = table top
            if (cellFrame != null)
            {
                cellFrame.gameObject.SetActive(true);
                cellFrame.SetPositionAndRotation(
                    new Vector3(baseJoint.position.x, surfaceY, baseJoint.position.z),
                    Quaternion.LookRotation(fwd.normalized, Vector3.up));
                cellFrame.SetParent(robot.transform, true);
            }
            if (speed != null) speed.robotBase = baseJoint;

            // The lab grab handle on the TCP becomes the guide handle.
            var relay = robot.GetComponentInChildren<MetaMove.Robot.PhantomGrabRelay>(true);
            if (relay != null)
            {
                var list = new List<Behaviour>();
                foreach (var b in relay.GetComponentsInChildren<Behaviour>(true)) list.Add(b);
                grabInteractors = list.ToArray();
                guideOnlyRenderers = relay.GetComponentsInChildren<Renderer>(true);
            }

            // Gripper: tool points straight down, parts are held between the finger pads,
            // and the flange stays one tool length above the grasp point.
            _gripper = robot.GetComponentInChildren<MetaMove.Robot.RobotiqGripper>(true);
            if (_gripper != null && _gripper.tcp != null)
            {
                toolOffset = Vector3.Distance(tcp.position, _gripper.tcp.position);
                _graspRot = Quaternion.FromToRotation(tcp.forward, Vector3.down) * tcp.rotation;
                _holdAt = _gripper.tcp;
                _gripper.Open();
            }
            else
            {
                _graspRot = ikTarget.rotation;
                _holdAt = tcp;
            }
            _aHome = new Pose(trayA.transform.localPosition, trayA.transform.localRotation);
            _bound = true;
            Debug.Log("[Kitting] bound to " + robot.name + " base=" + baseJoint.position.ToString("F2") + " surfaceY=" + surfaceY.ToString("F2"));
            Restart();
        }

        void Start()
        {
            if (!_bound && calibrator == null)
            {
                // Scene without QR spawn: bind to whatever solver is present.
                var ik = FindAnyObjectByType<MetaMove.Robot.GoFaDLSIK>(FindObjectsInactive.Include);
                if (ik != null) Bind(ik.transform.root.gameObject);
            }
        }

        float F => speed != null ? Mathf.Clamp01(speed.Factor) : 1f;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb[placeTrayKey].wasPressedThisFrame) PlaceTrayB(trayBPlaced);
                if (kb[coarseGuideKey].wasPressedThisFrame) CoarseGuideOverTrayB();
                if (kb[teachKey].wasPressedThisFrame) Teach();
                if (kb[restartKey].wasPressedThisFrame) Restart();
            }
            if (OVRInput.GetDown(teachButton)) Teach();
            if (OVRInput.GetDown(placeTrayButton) && !_trayBOnTable) PlaceTrayB(trayBPlaced);
        }

        // ---------------------------------------------------------------- public API

        public void Restart()
        {
            if (!_bound) return;
            if (_run != null) StopCoroutine(_run);
            if (_held != null) { _held.SetActive(false); _held = null; }
            if (_gripper != null) _gripper.Open();
            trayA.Refill();
            trayB.Refill();
            trayA.SetRecognized(false);
            trayB.SetRecognized(false);
            if (_aHome.HasValue)
                trayA.transform.SetLocalPositionAndRotation(_aHome.Value.position, _aHome.Value.rotation);
            var carryA = trayA.GetComponent<CarryableTray>();
            if (carryA != null) carryA.ResetToStart();
            if (trayBSpawn != null) trayB.transform.SetPositionAndRotation(trayBSpawn.position, trayBSpawn.rotation);
            var carry = trayB.GetComponent<CarryableTray>();
            if (carry != null) carry.ResetToStart();
            _trayBOnTable = false;
            _lowCued = false;
            ClearKit();
            KitsDone = 0;
            Taught = trayA;
            SetGuide(false);
            if (ikTarget != null && home != null) ikTarget.SetPositionAndRotation(home.position, _graspRot);
            Emit("restart", "Line running");
            _run = StartCoroutine(StartAfterSettle());
        }

        // The visitor has set tray B down. In the headset this is called by the
        // tray's grab release; in the editor by the R shortcut.
        public void PlaceTrayB(Transform pose)
        {
            if (pose != null)
            {
                var carry = trayB.GetComponent<CarryableTray>();
                if (carry != null && carry.rack != null) { carry.LoadIntoRack(); return; }   // rolls in, calls back
                trayB.transform.SetPositionAndRotation(pose.position, pose.rotation);
                if (carry != null) carry.MarkPlaced();
            }
            _trayBOnTable = true;
            Debug.Log("[Kitting] tray B placed at " + trayB.transform.position.ToString("F3"));
            Emit("arrived", "New batch in the rack");
            if (State == Phase.Waiting) EnterGuide();
        }

        // Home from the fingertip menu: stop the line and park the arm; Restart resumes.
        public void ParkHome()
        {
            if (!_bound) return;
            if (_run != null) StopCoroutine(_run);
            SetGuide(false);
            State = Phase.Idle;
            _run = StartCoroutine(MoveTo(home, Vector3.zero));
            Emit("home", "Parked at home");
        }

        // Teach: accept the tray under the TCP as the new work object.
        public void Teach()
        {
            if (State != Phase.Guide) return;
            var p = ikTarget.position;
            KittingTray hit = null;
            foreach (var t in new[] { trayB, trayA })
            {
                if (t == null || t.Remaining == 0) continue;
                var d = p - t.Center;
                float horiz = new Vector2(d.x, d.z).magnitude;
                if (horiz <= teachRadius && d.y >= -0.02f && d.y <= teachMaxHeight) { hit = t; break; }
            }
            if (hit == null)
            {
                Emit("teach_fail", "No tray under the gripper");
                return;
            }
            SetGuide(false);
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(AlignThenRun(hit));
        }

        // Editor stand-in for a coarse pinch: park the TCP a few cm off tray B.
        public void CoarseGuideOverTrayB()
        {
            if (State != Phase.Guide || ikTarget == null) return;
            var off = new Vector3(0.06f, 0.18f, -0.05f);
            ikTarget.position = trayB.Center + off;
            Debug.Log("[Kitting] coarse guide -> " + ikTarget.position.ToString("F3"));
        }

        // ---------------------------------------------------------------- loop

        // Let the anchor settle (QrAnchorCalibrator lifts it a couple of frames after
        // spawn) before the first move; start from home.
        IEnumerator StartAfterSettle()
        {
            yield return new WaitForSeconds(0.5f);
            ikTarget.SetPositionAndRotation(home.position, _graspRot);
            yield return RunLoop();
        }

        IEnumerator RunLoop()
        {
            State = Phase.Running;
            while (true)
            {
                int n = Taught.NextFilled();
                if (n < 0)
                {
                    yield return Probe(Taught);
                    yield return WaitForTray();
                    yield break;   // Guide takes over; AlignThenRun restarts the loop
                }
                if (!_lowCued && Taught == trayA && Taught.Remaining <= 2)
                {
                    _lowCued = true;
                    Emit("low", Taught.label + " running low");
                }
                yield return PickPlace(Taught, n);
            }
        }

        IEnumerator PickPlace(KittingTray tray, int nest)
        {
            var n = tray.nests[nest];
            float up = toolOffset + tray.graspHeight + approachHeight;
            float at = toolOffset + tray.graspHeight;
            yield return MoveAlong(n, up);
            yield return MoveAlong(n, at);
            if (_gripper != null) _gripper.CloseOnPart();
            yield return Dwell(gripDwell);
            _held = tray.parts[nest];
            _held.transform.SetParent(_holdAt != null ? _holdAt : ikTarget, true);
            yield return MoveAlong(n, up);

            var slot = kitSlots[_slot];
            yield return MoveAlong(slot, up);
            yield return MoveAlong(slot, at);
            if (_gripper != null) _gripper.Open();
            yield return Dwell(gripDwell);
            _held.transform.SetParent(slot, true);
            _held.transform.localPosition = Vector3.zero;
            _held.transform.localRotation = Quaternion.identity;
            _held = null;
            yield return MoveAlong(slot, up);

            _slot++;
            if (_slot >= kitSlots.Count)
            {
                if (conveyor != null) conveyor.Ship(kitSlots);   // the full kit leaves on the conveyor
                KitsDone++;
                Debug.Log("[Kitting] kit " + KitsDone + " complete");
                ClearKit();
            }
        }

        // Taught tray is empty: the robot does not know that yet. It goes there,
        // grips air, and comes back up.
        IEnumerator Probe(KittingTray tray)
        {
            State = Phase.Probe;
            var n = tray.FirstNest;
            float up = toolOffset + tray.graspHeight + approachHeight;
            float at = toolOffset + tray.graspHeight;
            yield return MoveAlong(n, up);
            yield return MoveAlong(n, at);
            if (_gripper != null) _gripper.CloseEmpty();   // fingers close all the way: nothing there
            yield return Dwell(gripDwell * 2f);
            Emit("miss", tray.label + " is empty");
            if (_gripper != null) _gripper.Open();
            yield return MoveAlong(n, up);
            yield return MoveTo(home, Vector3.zero);
        }

        IEnumerator WaitForTray()
        {
            State = Phase.Waiting;
            if (_trayBOnTable) { EnterGuide(); yield break; }
            Emit("starved", "Line starved - waiting for parts");
            while (State == Phase.Waiting) yield return null;
        }

        void EnterGuide()
        {
            State = Phase.Guide;
            SetGuide(true);
            Emit("guide", "New tray - location not taught");
        }

        IEnumerator AlignThenRun(KittingTray tray)
        {
            State = Phase.Align;
            // The visible correction: from wherever the visitor left the TCP to the
            // tray's own frame. This is the "fine alignment" the machine contributes.
            yield return MoveAlong(tray.transform, toolOffset + tray.graspHeight + approachHeight);
            tray.SetRecognized(true);
            Taught = tray;
            Emit("aligned", tray.label + " taught");
            yield return Dwell(0.6f);
            Emit("resume", "Line running from " + tray.label);
            _run = StartCoroutine(RunLoop());
        }

        // ---------------------------------------------------------------- motion

        // Goal is re-evaluated every frame: the anchor under the whole cell can move
        // (editor spawn lift, QR re-align) while the arm is on its way.
        // Move to 'above' metres along the goal's own up axis, tool aligned with it.
        // Nests on the flow rack are tilted with the lane; kit slots are level.
        IEnumerator MoveAlong(Transform goal, float above)
        {
            yield return MoveTo(goal, Vector3.zero, above);
        }

        IEnumerator MoveTo(Transform goal, Vector3 worldOffset, float alongUp = 0f)
        {
            float v = 0f;
            while (true)
            {
                var p = goal.position + worldOffset + goal.up * alongUp;
                var rotGoal = alongUp != 0f ? Quaternion.FromToRotation(Vector3.up, goal.up) * _graspRot : _graspRot;
                float dist = Vector3.Distance(ikTarget.position, p);
                // Trapezoid profile: accelerate, cruise at baseSpeed * factor, brake into the
                // goal. The SSM factor scales the cruise speed and brakes immediately.
                float vMax = baseSpeed * F;
                float vBrake = Mathf.Sqrt(2f * acceleration * dist);
                v = Mathf.Min(Mathf.Min(v + acceleration * Time.deltaTime, vMax), vBrake);
                ikTarget.position = Vector3.MoveTowards(ikTarget.position, p, v * Time.deltaTime);
                ikTarget.rotation = Quaternion.RotateTowards(ikTarget.rotation, rotGoal, 360f * F * Time.deltaTime);
                if ((ikTarget.position - p).sqrMagnitude <= arriveTol * arriveTol) yield break;
                yield return null;
            }
        }

        IEnumerator Dwell(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime * F; yield return null; }
        }

        // ---------------------------------------------------------------- helpers

        void ClearKit()
        {
            _slot = 0;
            foreach (var s in kitSlots)
                for (int i = s.childCount - 1; i >= 0; i--)
                    s.GetChild(i).gameObject.SetActive(false);
            // parts stay parented to the slot until the next Refill() reclaims them
        }

        void SetGuide(bool on)
        {
            if (grabInteractors != null)
                foreach (var b in grabInteractors) if (b != null) b.enabled = on;
            if (guideOnlyRenderers != null)
                foreach (var r in guideOnlyRenderers) if (r != null) r.enabled = on;
        }

        void Emit(string key, string status)
        {
            Status = status;
            Debug.Log("[Kitting] " + State + " cue=" + key + " : " + status);
            Cue?.Invoke(key);
        }
    }
}
