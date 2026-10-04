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
    //   Probe     taught tray is empty: go there once anyway and grip air,
    //             then carry on from the reserve box (a known location) if it has parts
    //   Waiting   no tray the robot knows has parts; "starved" until tray B arrives
    //             and the empty tray A has been taken out
    //   Guide     tray B is in the rack but not taught: the visitor pinch-drags
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
        // The arm's base joint (set when the robot is bound); the speed monitoring measures to
        // the tool (tcp), the chain anchor -> base -> joints -> tool is what TrackingViz draws.
        public Transform BaseJoint { get; private set; }
        public ProximitySpeedController speed;
        [Tooltip("Home pose, child of this cell.")]
        public Transform home;

        [Header("Cell")]
        public KittingTray trayA;
        public KittingTray trayB;
        [Tooltip("Reserve box on a lane the robot already knows. Keeps the line running while the " +
                 "visitor clears the empty tray A - the arm is moving when the hands come close.")]
        public KittingTray reserve;
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
        [Tooltip("m/s^2: how fast the arm brakes when the speed monitoring asks for less - quick, but a ramp, never a freeze mid-move.")]
        public float ssmBrake = 3.5f;
        public float approachHeight = 0.12f;   // m above the grasp point
        [Tooltip("Height above the table top (TCP) for every move between tray and kit. Clears the " +
                 "KLT rim (0.26 m on the rack) with the gear hanging below the fingers.")]
        public float transitHeight = 0.40f;
        [Tooltip("Slower, visibly careful approach while locating a newly taught tray.")]
        [Range(0.1f, 1f)] public float alignSpeedScale = 0.35f;
        [Tooltip("Flange above the part centre at grasp, until a real gripper defines the TCP.")]
        public float toolOffset = 0.07f;
        public float gripDwell = 0.25f;        // s at factor 1
        public float arriveTol = 0.004f;
        [Tooltip("A waypoint counts as reached only when the real flange is this close to the target: " +
                 "no cutting corners through box walls, no gripping beside the part.")]
        public float settleTol = 0.006f;
        [Tooltip("Give up waiting for the solver after this long at speed factor 1 (s).")]
        public float settleTimeout = 0.6f;

        [Header("Teach")]
        [Tooltip("Horizontal radius around a tray centre in which Teach counts as 'over the tray'.")]
        public float teachRadius = 0.15f;
        [Tooltip("Max height of the TCP above the tray for Teach to count.")]
        public float teachMaxHeight = 0.55f;   // the guide filter keeps the tool above the tray rim

        [Header("Story pacing")]
        [Tooltip("Tray A parts left when the refill is asked for: early enough that the visitor walks " +
                 "up while the arm is still working, so the slow-down is seen, not just a stop.")]
        public int lowAt = 6;

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
        [Tooltip("Second run: no hints, no narration - the visitor does it from memory.")]
        public bool Expert;
        // Freestyle, from the hand menu: no story, no tasks, no report.
        //   Auto   the line just runs - empty boxes fill up again - to show the speed scaling
        //   Teach  starts in free guiding: pinch, guide to any box, Teach; Guide again any time
        public enum Freestyle { None, Auto, Teach }
        public Freestyle Mode;
        public int PartsPlaced { get; private set; }
        // Parked from the hand menu (Home). Home again resumes where the line stopped.
        public bool Parked { get; private set; }
        // Guiding started from the hand menu (not by the story): no narration, any full box.
        public bool FreeGuide { get; private set; }
        // Horizontal distance (m) from the tool to the tray centre when Teach was pressed:
        // how coarse the visitor's pinch was, and how far the robot corrected on its own.
        public float TeachOffset { get; private set; } = -1f;
        // The refill has been asked for (tray A low) or the line is already starved.
        public bool RefillDue => _lowCued || State == Phase.Waiting;
        public bool TrayBInRack => _trayBOnTable;
        public Quaternion GraspRotation => _graspRot;
        public MetaMove.Robot.PhantomGrabRelay GuideRelay { get; private set; }
        public bool GuideHeld => GuideRelay != null && GuideRelay.grabbable != null && GuideRelay.grabbable.SelectingPointsCount > 0;

        // Tray A no longer stands in the pick lane: carried to the return lane (or never carryable).
        public bool TrayACleared
        {
            get
            {
                var c = trayA != null ? trayA.GetComponent<CarryableTray>() : null;
                return c == null || c.State == CarryableTray.Phase.Rolling || c.State == CarryableTray.Phase.Arrived;
            }
        }

        // The empty tray A may be carried to the return lane once the robot no longer
        // needs it: after its failed probe, or once it runs from tray B.
        public bool AllowEmptyRemoval =>
            trayA != null && trayA.Remaining == 0 &&
            (State == Phase.Waiting || State == Phase.Guide || State == Phase.Align ||
             (State == Phase.Running && Taught != trayA));

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
        Phase _beforePark;
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
            BaseJoint = baseJoint;

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
            // Separation is measured to the tool, the part of the arm that moves towards you:
            // headset -> anchor -> base, then the arm's own joint angles (IK) out to the tool.
            if (speed != null) speed.robotBase = tcp;

            // The lab grab handle on the TCP becomes the guide handle.
            var relay = robot.GetComponentInChildren<MetaMove.Robot.PhantomGrabRelay>(true);
            GuideRelay = relay;
            if (relay != null)
            {
                // Relative drag: the tool starts from where it is, it never jumps to the hand.
                relay.attractToHand = false;
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
            if (OVRInput.GetDown(placeTrayButton))
            {
                // Presenter fallback, one step per press: new batch in, then the empty out.
                if (!_trayBOnTable) PlaceTrayB(trayBPlaced);
                else if (!TrayACleared && AllowEmptyRemoval) trayA.GetComponent<CarryableTray>()?.SendToReturn();
            }
        }

        // ---------------------------------------------------------------- public API

        public void Restart()
        {
            if (!_bound) return;
            if (_run != null) StopCoroutine(_run);
            _speedScale = 1f;
            Parked = false;
            FreeGuide = false;
            if (_held != null) { _held.SetActive(false); _held = null; }
            if (_gripper != null) _gripper.Open();
            trayA.Refill();
            trayB.Refill();
            if (reserve != null) { reserve.Refill(); reserve.SetRecognized(false); }
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
            PartsPlaced = 0;
            TeachOffset = -1f;
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
        }

        // Home from the hand menu: park the arm. A toggle, so a stray press never ends the
        // demo - Home again (labelled Resume) picks up exactly where the line stopped.
        public void ParkHome()
        {
            if (!_bound) return;
            if (Parked) { Resume(); return; }
            _beforePark = State;
            if (_run != null) StopCoroutine(_run);
            ReturnHeldPart();
            _speedScale = 1f;
            SetGuide(false);
            Parked = true;
            State = Phase.Idle;
            _run = StartCoroutine(MoveTo(home, Vector3.zero));
            Emit("home", "Parked - Home again to resume");
        }

        public void Resume()
        {
            if (!_bound || !Parked) return;
            Parked = false;
            if (_run != null) StopCoroutine(_run);
            switch (_beforePark)
            {
                case Phase.Guide:
                case Phase.Align:
                    // back to guiding without replaying its narration
                    State = Phase.Guide;
                    SetGuide(true);
                    break;
                case Phase.Waiting:
                    _run = StartCoroutine(WaitForTray());
                    break;
                case Phase.Idle:
                    break;
                default:
                    _run = StartCoroutine(RunLoop());
                    break;
            }
            Emit("unpark", "Resumed");
        }

        // Hand menu "Guide": stop the line at any time and guide the tool by hand to whichever
        // box should be picked from next; Teach over it and the line runs from there.
        public void StartGuide()
        {
            if (!_bound || State == Phase.Guide || State == Phase.Align) return;
            if (_run != null) StopCoroutine(_run);
            ReturnHeldPart();
            _speedScale = 1f;
            Parked = false;
            FreeGuide = true;
            _run = StartCoroutine(GuideFromReady());
        }

        IEnumerator GuideFromReady()
        {
            State = Phase.Waiting;
            yield return MoveReady();
            State = Phase.Guide;
            SetGuide(true);
            Emit("guide_free", "Guide the tool to a box, then Teach");
        }

        // A part in the gripper when the line is interrupted goes back into its nest.
        void ReturnHeldPart()
        {
            if (_gripper != null) _gripper.Open();
            if (_held == null) return;
            foreach (var t in new[] { trayA, trayB, reserve })
            {
                if (t == null) continue;
                int i = t.parts.IndexOf(_held);
                if (i < 0) continue;
                _held.transform.SetParent(t.nests[i], false);
                _held.transform.localPosition = Vector3.zero;
                _held.transform.localRotation = Quaternion.identity;
                break;
            }
            _held = null;
        }

        // Teach: accept the tray under the TCP as the new work object.
        public void Teach()
        {
            if (State != Phase.Guide) return;
            var hit = TrayUnderTool();
            if (hit == null)
            {
                Emit("teach_fail", "No tray under the gripper");
                return;
            }
            var d = ikTarget.position - hit.Center;
            bool free = FreeGuide;
            FreeGuide = false;
            if (!free) TeachOffset = new Vector2(d.x, d.z).magnitude;
            // free guiding stays out of the story and the shift report: its own cue names
            Emit(free ? "teach_free" : "teach", "Teach accepted, " + (new Vector2(d.x, d.z).magnitude * 100f).ToString("0.0") + " cm off");
            SetGuide(false);
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(AlignThenRun(hit, free));
        }

        // The tray Teach would accept right now, or null. Also drives the hints.
        public KittingTray TrayUnderTool()
        {
            if (ikTarget == null) return null;
            var p = ikTarget.position;
            foreach (var t in new[] { trayB, trayA, reserve })
            {
                if (t == null || t.Remaining == 0) continue;
                var d = p - t.Center;
                float horiz = new Vector2(d.x, d.z).magnitude;
                if (horiz <= teachRadius && d.y >= -0.02f && d.y <= teachMaxHeight) return t;
            }
            return null;
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
            // let the factory build itself around the visitor first; the arm starts once the hall stands
            var reveal = cellFrame != null ? cellFrame.GetComponent<FactoryReveal>() : null;
            if (reveal != null) while (!reveal.Done) yield return null;
            ikTarget.SetPositionAndRotation(home.position, _graspRot);
            if (Mode == Freestyle.Teach) { FreeGuide = true; yield return GuideFromReady(); yield break; }
            yield return RunLoop();
        }

        IEnumerator RunLoop()
        {
            State = Phase.Running;
            while (true)
            {
                // Working from the reserve: as soon as the new batch is in the rack and the empty
                // is out, finish here and ask to be shown the new box.
                if (Taught == reserve && _trayBOnTable && TrayACleared)
                {
                    yield return MoveReady();
                    State = Phase.Waiting;
                    EnterGuide();
                    yield break;
                }
                // The line never starves while the visitor takes their time: an empty reserve is
                // pushed out and a full one rolls in on its lane, so the arm keeps moving and
                // the slow-down still shows whenever they come close.
                if (Taught == reserve && reserve.NextFilled() < 0 && !(_trayBOnTable && TrayACleared))
                {
                    yield return MoveTo(home, Vector3.zero);
                    yield return RestockReserve();
                    continue;
                }
                int n = Taught.NextFilled();
                if (n < 0 && Mode != Freestyle.None)
                {
                    // freestyle: nobody brings boxes - the empty one fills up again and on it goes
                    yield return MoveTo(home, Vector3.zero);
                    yield return new WaitForSeconds(0.6f);
                    Taught.Refill();
                    Emit("restock_free", Taught.label + " refilled");
                    yield return new WaitForSeconds(0.4f);
                    continue;
                }
                if (n < 0)
                {
                    if (Taught != reserve) yield return Probe(Taught);
                    if (reserve != null && Taught != reserve && reserve.NextFilled() >= 0)
                    {
                        // The empty box does not stop the line: the arm switches to the reserve.
                        Taught = reserve;
                        State = Phase.Running;
                        Emit("reserve", "Running from the reserve box");
                        continue;
                    }
                    yield return MoveTo(home, Vector3.zero);
                    yield return WaitForTray();
                    yield break;   // Guide takes over; AlignThenRun restarts the loop
                }
                if (Mode == Freestyle.None && !_lowCued && Taught == trayA && Taught.Remaining <= lowAt && !IntroOver())
                {
                    // not while the anchor is being explained: wait at home until the story is through
                    yield return MoveTo(home, Vector3.zero);
                    float since = Time.time;
                    while (!IntroOver() && Time.time - since < 45f) yield return null;
                }
                if (Mode == Freestyle.None && !_lowCued && Taught == trayA && Taught.Remaining <= lowAt)
                {
                    _lowCued = true;
                    Emit("low", Taught.label + " running low");
                }
                yield return PickPlace(Taught, n);
            }
        }

        StoryDirector _story;
        bool IntroOver()
        {
            if (Expert) return true;
            if (_story == null) _story = FindAnyObjectByType<StoryDirector>();
            return _story == null || !_story.isActiveAndEnabled || _story.IntroDone;
        }

        IEnumerator PickPlace(KittingTray tray, int nest)
        {
            var n = tray.nests[nest];
            float up = toolOffset + tray.graspHeight + approachHeight;
            float at = toolOffset + tray.graspHeight;
            // Over the box at transit height, then straight down into it: the gripper and the
            // gear never cut through a box wall.
            yield return MoveOver(n);
            yield return MoveAlong(n, up);
            // pre-close above the box: the open 2F-85 is wider than the gap to the box wall
            if (_gripper != null) _gripper.Approach();
            yield return MoveAlong(n, at);
            if (_gripper != null) _gripper.CloseOnPart();
            yield return Dwell(gripDwell);
            _held = tray.parts[nest];
            _held.transform.SetParent(_holdAt != null ? _holdAt : ikTarget, true);
            // centre the part between the fingers (graspHeight above its foot), so a millimetre
            // the solver is off never shows as a grip beside the gear
            if (_holdAt != null)
                _held.transform.position += _holdAt.position - (_held.transform.position + _held.transform.up * tray.graspHeight);
            yield return MoveAlong(n, up);
            yield return MoveOver(n);

            // a kit left full by an interruption is shipped before the next part goes in
            if (_slot >= kitSlots.Count) ShipKit();
            var slot = kitSlots[_slot];
            yield return MoveOver(slot);
            yield return MoveAlong(slot, at);
            if (_gripper != null) _gripper.Open();
            yield return Dwell(gripDwell);
            _held.transform.SetParent(slot, true);
            _held.transform.localPosition = Vector3.zero;
            _held.transform.localRotation = Quaternion.identity;
            _held = null;
            PartsPlaced++;
            _slot++;   // counted the moment the part is down: an interruption now never reuses the slot
            yield return MoveOver(slot);
            if (_slot >= kitSlots.Count) ShipKit();
        }

        void ShipKit()
        {
            if (conveyor != null) conveyor.Ship(kitSlots);   // the full kit leaves on the conveyor
            KitsDone++;
            Debug.Log("[Kitting] kit " + KitsDone + " complete");
            ClearKit();
        }

        // Taught tray is empty: the robot does not know that yet. It goes there,
        // grips air, and comes back up.
        IEnumerator Probe(KittingTray tray)
        {
            State = Phase.Probe;
            var n = tray.FirstNest;
            float up = toolOffset + tray.graspHeight + approachHeight;
            float at = toolOffset + tray.graspHeight;
            yield return MoveOver(n);
            yield return MoveAlong(n, up);
            if (_gripper != null) _gripper.Approach();
            yield return MoveAlong(n, at);
            if (_gripper != null) _gripper.CloseEmpty();   // fingers close all the way: nothing there
            yield return Dwell(gripDwell * 2f);
            Emit("miss", tray.label + " is empty");
            if (_gripper != null) _gripper.Approach();   // open only as far as the box allows
            yield return MoveAlong(n, up);
            if (_gripper != null) _gripper.Open();
            yield return MoveOver(n);
            State = Phase.Running;
        }

        IEnumerator WaitForTray()
        {
            State = Phase.Waiting;
            if (!_trayBOnTable) Emit("starved", "Line starved - waiting for parts");
            // Guide only once the new batch is in AND the empty is out of the pick lane:
            // one thing to do at a time.
            while (State == Phase.Waiting)
            {
                if (_trayBOnTable && TrayACleared) { yield return MoveReady(); EnterGuide(); yield break; }
                yield return null;
            }
        }

        void EnterGuide()
        {
            State = Phase.Guide;
            SetGuide(true);
            Emit("guide", "New tray - location not taught");
        }

        IEnumerator AlignThenRun(KittingTray tray, bool free = false)
        {
            State = Phase.Align;
            // The visible correction: from wherever the visitor left the TCP to the
            // tray's own frame. This is the "fine alignment" the machine contributes.
            _speedScale = alignSpeedScale;
            yield return MoveAlong(tray.transform, toolOffset + tray.graspHeight + approachHeight);
            _speedScale = 1f;
            tray.SetRecognized(true);
            Taught = tray;
            Emit(free ? "aligned_free" : "aligned", tray.label + " taught");
            yield return Dwell(2.0f);   // let the "taught" moment land before the line runs again
            Emit(free ? "resume_free" : "resume", "Line running from " + tray.label);
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

        [Tooltip("Lane entry marker the restocked reserve rolls in from (set by the builder).")]
        public Transform reserveEntry;

        IEnumerator RestockReserve()
        {
            if (reserve == null) yield break;
            var pick = new Pose(reserve.transform.position, reserve.transform.rotation);
            var rack = reserve.GetComponentInParent<FlowRack>() ?? FindAnyObjectByType<FlowRack>();
            Emit("restock", "Reserve restocked");
            // the empty sinks away, a full box rolls down the lane to the stop
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) { reserve.transform.localScale = Vector3.one * (1f - t / 0.4f); yield return null; }
            reserve.Refill();
            if (reserveEntry != null) reserve.transform.SetPositionAndRotation(reserveEntry.position, reserveEntry.rotation);
            reserve.transform.localScale = Vector3.one;
            var stop = new GameObject("ReservePick").transform;
            stop.SetPositionAndRotation(pick.position, pick.rotation);
            if (rack != null && reserveEntry != null) yield return rack.Roll(reserve.transform, null, stop);
            else reserve.transform.SetPositionAndRotation(pick.position, pick.rotation);
            Destroy(stop.gameObject);
        }

        // A relaxed pose over the front of the table, well inside the arm's range: hand guiding
        // always starts from here, never from wherever the last cycle ended.
        IEnumerator MoveReady()
        {
            float v = 0f;
            while (true)
            {
                var f = cellFrame;
                var p = f.position + f.rotation * new Vector3(0f, transitHeight + toolOffset, 0.30f);
                if (Step(p, _graspRot, ref v)) yield break;
                yield return null;
            }
        }

        // Straight above the goal at transit height (cell frame, table top = 0). The tool takes
        // the goal's tilt (rack lanes slope ~8 deg) up here, during the long move, so going
        // down into the box and back up is a pure translation - no turn at the box.
        IEnumerator MoveOver(Transform goal)
        {
            float v = 0f;
            var rot = Quaternion.FromToRotation(Vector3.up, goal.up) * _graspRot;
            while (true)
            {
                var p = goal.position;
                p.y = (cellFrame != null ? cellFrame.position.y : 0f) + transitHeight + toolOffset;
                if (Step(p, rot, ref v)) yield break;
                yield return null;
            }
        }

        IEnumerator MoveTo(Transform goal, Vector3 worldOffset, float alongUp = 0f)
        {
            float v = 0f;
            while (true)
            {
                var p = goal.position + worldOffset + goal.up * alongUp;
                var rotGoal = alongUp != 0f ? Quaternion.FromToRotation(Vector3.up, goal.up) * _graspRot : _graspRot;
                if (Step(p, rotGoal, ref v)) yield break;
                yield return null;
            }
        }

        float _speedScale = 1f;
        float _settle;

        // One frame of the trapezoid profile: accelerate, cruise at baseSpeed * factor, brake
        // into the goal. The SSM factor scales the cruise speed and brakes immediately.
        bool Step(Vector3 p, Quaternion rotGoal, ref float v)
        {
            float dist = Vector3.Distance(ikTarget.position, p);
            float vMax = baseSpeed * F * _speedScale;
            float vBrake = Mathf.Sqrt(2f * acceleration * dist);
            // up with the normal ramp; down (slow zone, stop) with a short braking ramp
            float vSsm = v <= vMax ? Mathf.Min(v + acceleration * Time.deltaTime, vMax) : Mathf.Max(vMax, v - ssmBrake * Time.deltaTime);
            v = Mathf.Min(vSsm, vBrake);
            ikTarget.position = Vector3.MoveTowards(ikTarget.position, p, v * Time.deltaTime);
            ikTarget.rotation = Quaternion.RotateTowards(ikTarget.rotation, rotGoal, 120f * F * Time.deltaTime);
            if ((ikTarget.position - p).sqrMagnitude > arriveTol * arriveTol) { _settle = 0f; return false; }
            // target is there; now wait for the arm itself (time only runs while the arm may move)
            _settle += Time.deltaTime * F;
            bool settled = tcp == null || (tcp.position - ikTarget.position).sqrMagnitude <= settleTol * settleTol;
            if (settled || _settle > settleTimeout) { _settle = 0f; return true; }
            return false;
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
