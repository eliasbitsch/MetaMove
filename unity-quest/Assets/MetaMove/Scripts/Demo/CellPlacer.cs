using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using MetaMove.Demo.Kitting;
using MetaMove.Safety;

namespace MetaMove.Demo
{
    // Puts the kitting cell into the room without a QR marker.
    //
    //   1. A ghost footprint (robot table, flow rack, the two speed zones) lies on the
    //      floor where either hand's pinch ray - or, without hands, the gaze - meets it,
    //      and always turns its front towards the user.
    //   2. Pinch and hold (~1.2 s, a ring fills up) confirms. The robot is spawned with its
    //      foot on the virtual table top and the whole cell hangs off one spatial anchor.
    //   3. On the headset the anchor is saved; next start it is loaded and the cell comes
    //      back where it was, no placing needed. "Place" in the hand menu (tap twice)
    //      forgets it and starts placing again - a gesture would fire while carrying a box.
    //
    // A QR marker, if one is seen, still works as before (QrAnchorCalibrator).
    public class CellPlacer : MonoBehaviour
    {
        public QrAnchorCalibrator calibrator;
        public KittingCell cell;
        public OVRHand rightHand, leftHand;
        public Transform head;

        [Tooltip("Robot foot height above the floor = virtual table top.")]
        public float tableHeight = 0.80f;
        public float defaultDistance = 1.8f;
        public float minDistance = 0.9f, maxDistance = 4.0f;
        public float holdToConfirm = 1.2f;
        [Tooltip("Place by hand in the Editor too (else the calibrator's editor auto-spawn is used).")]
        public bool placeInEditor = false;
        public Material ghostMaterial;
        [Tooltip("Labels on the placement ghost (YOU, ROBOT, RACK, WALL).")]
        public TMPro.TMP_FontAsset labelFont;

        const string PrefKey = "MetaMove.Bari.CellAnchor";
        const string ReplaceMarker = "place_again";
        enum Phase { Idle, Loading, Placing, Spawning, Placed }
        Phase _phase = Phase.Idle;
        float _hold;
        GameObject _ghost;
        Transform _ring;
        LineRenderer _ringLine;
        Vector3 _point;
        Quaternion _rot;

        public bool IsPlacing => _phase == Phase.Placing;

        IEnumerator Start()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            yield return new WaitForSeconds(0.8f);   // let tracking settle
            if (Application.isEditor && !placeInEditor) { _phase = Phase.Placed; yield break; }

#if UNITY_ANDROID && !UNITY_EDITOR
            // "Place" from the hand menu leaves a marker file before the app restarts: a file is
            // on disk at once, PlayerPrefs may not be when the process ends right after.
            var marker = System.IO.Path.Combine(Application.persistentDataPath, ReplaceMarker);
            if (System.IO.File.Exists(marker))
            {
                System.IO.File.Delete(marker);
                PlayerPrefs.DeleteKey(PrefKey); PlayerPrefs.Save();
                Debug.Log("[CellPlacer] place again (requested before the restart)");
            }
            var saved = PlayerPrefs.GetString(PrefKey, "");
            if (Guid.TryParse(saved, out var uuid))
            {
                _phase = Phase.Loading;
                bool ok = false, done = false;
                var load = StartCoroutine(LoadAnchor(uuid, r => { ok = r; done = true; }));
                // a new room never finds the old anchor - do not wait for it forever
                float t0 = Time.time;
                while (!done && Time.time - t0 < 8f) yield return null;
                if (ok) yield break;
                if (!done) { StopCoroutine(load); Debug.LogWarning("[CellPlacer] saved anchor not localised in 8 s - placing again"); }
                else Debug.LogWarning("[CellPlacer] saved anchor not found - placing again");
            }
#endif
            BeginPlacing();
        }

        public void BeginPlacing()
        {
            _phase = Phase.Placing;
            _hold = 0f;
            if (_ghost == null) BuildGhost();
            _ghost.SetActive(true);
            Debug.Log("[CellPlacer] placing: aim at the floor, pinch and hold to confirm");
        }

        void Update()
        {
            // either hand: the one that pinches aims, otherwise the right, then the left
            bool lp = Pinching(leftHand), rp = Pinching(rightHand);
            if (lp && !rp) _aim = leftHand; else if (rp) _aim = rightHand;
            else if (!Usable(_aim)) _aim = Usable(rightHand) ? rightHand : leftHand;
            bool rPinch = lp || rp || OVRInput.Get(OVRInput.Button.One);

            if (_phase != Phase.Placing) return;
            if (!FloorTarget(out _point)) return;
            // The visitor's spot (white circle, YOU) lies under the head; the hand only says
            // in which direction the robot goes. The cell is turned so that its spot is exactly
            // where the visitor stands - no aiming sideways to hit the circle.
            var feet = new Vector3(head.position.x, _point.y, head.position.z);
            Vector3 dir = _point - feet; dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) { dir = head.forward; dir.y = 0f; }
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            var spot = new Vector3(VisitorSpot.x, 0f, VisitorSpot.y);
            float spotYaw = Mathf.Atan2(spot.x, spot.z) * Mathf.Rad2Deg;   // the spot seen from the robot foot
            // the robot lies along dir from the visitor: cell +Z points back at the visitor
            _rot = Quaternion.LookRotation(-dir.normalized, Vector3.up) * Quaternion.Euler(0f, -spotYaw, 0f);
            _point = feet - _rot * spot;
            _ghost.transform.SetPositionAndRotation(_point, _rot);

            var kb = Keyboard.current;
            if (rPinch) _hold += Time.deltaTime; else _hold = 0f;
            UpdateRing(Mathf.Clamp01(_hold / holdToConfirm));
            if (_hold >= holdToConfirm || (kb != null && kb.pKey.wasPressedThisFrame)) Confirm();
        }

        OVRHand _aim;
        static bool Usable(OVRHand h) => h != null && h.IsTracked && h.IsPointerPoseValid;

        bool Pinching(OVRHand h) =>
            h != null && h.IsTracked && h.GetFingerIsPinching(OVRHand.HandFinger.Index);

        // Where the aiming hand's pointer ray (or the gaze) hits the floor.
        bool FloorTarget(out Vector3 p)
        {
            float floorY = 0f;   // FloorLevel tracking origin
            var tracking = head != null ? head.parent : null;
            if (tracking != null) floorY = tracking.position.y;
            Ray ray;
            if (Usable(_aim))
                ray = new Ray(_aim.PointerPose.position, _aim.PointerPose.forward);
            else
                ray = new Ray(head.position, head.forward);
            var plane = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
            if (plane.Raycast(ray, out float t) && t > 0f) p = ray.GetPoint(t);
            else { var f = head.forward; f.y = 0f; p = head.position + f.normalized * defaultDistance; p.y = floorY; }
            // Keep the footprint at a sensible distance from the user.
            var d = p - head.position; d.y = 0f;
            float dist = Mathf.Clamp(d.magnitude, minDistance, maxDistance);
            p = new Vector3(head.position.x, floorY, head.position.z) + d.normalized * dist;
            return true;
        }

        public void Confirm()
        {
            if (_phase != Phase.Placing) return;
            StartCoroutine(PlaceAt(_point, _rot, true));
        }

        // Test hook for the Editor / Simulator.
        public void PlaceAtWorld(Vector3 floorPoint, float yawDeg)
        {
            if (_ghost == null) BuildGhost();
            _phase = Phase.Placing;
            _point = floorPoint; _rot = Quaternion.Euler(0f, yawDeg, 0f);
            Confirm();
        }

        IEnumerator PlaceAt(Vector3 floorPoint, Quaternion rot, bool save)
        {
            _phase = Phase.Spawning;
            if (_ghost != null) _ghost.SetActive(false);
            if (cell != null) cell.frontOverride = rot * Vector3.forward;
            var root = calibrator.SpawnWithBaseAt(floorPoint + Vector3.up * tableHeight, rot);
            while (!calibrator.Placed) yield return null;

            // One anchor at the floor point carries the whole cell.
            var anchorGo = new GameObject("CellAnchor");
            anchorGo.transform.SetPositionAndRotation(floorPoint, rot);
            root.transform.SetParent(anchorGo.transform, true);
#if UNITY_ANDROID && !UNITY_EDITOR
            var anchor = anchorGo.AddComponent<OVRSpatialAnchor>();
            yield return new WaitUntil(() => anchor.Created);
            if (save)
            {
                var task = anchor.SaveAnchorAsync();
                while (!task.IsCompleted) yield return null;
                var res = task.GetResult();
                if (res.Success) { PlayerPrefs.SetString(PrefKey, anchor.Uuid.ToString()); PlayerPrefs.Save(); }
                Debug.Log("[CellPlacer] anchor " + anchor.Uuid + " saved=" + res.Success);
            }
#endif
            _phase = Phase.Placed;
            Debug.Log("[CellPlacer] cell placed at " + floorPoint.ToString("F2") + " yaw " + rot.eulerAngles.y.ToString("F0") +
                      " head y " + (head != null ? head.position.y.ToString("F2") : "?") +
                      " tracking y " + (head != null && head.parent != null ? head.parent.position.y.ToString("F2") : "?"));
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        IEnumerator LoadAnchor(Guid uuid, Action<bool> done)
        {
            var list = new List<OVRSpatialAnchor.UnboundAnchor>();
            var task = OVRSpatialAnchor.LoadUnboundAnchorsAsync(new[] { uuid }, list);
            while (!task.IsCompleted) yield return null;
            var res = task.GetResult();
            if (!res.Success || res.Value.Count == 0) { done(false); yield break; }
            var ua = res.Value[0];
            var loc = ua.LocalizeAsync();
            while (!loc.IsCompleted) yield return null;
            if (!loc.GetResult()) { done(false); yield break; }
            var anchorGo = new GameObject("CellAnchor");
            var anchor = anchorGo.AddComponent<OVRSpatialAnchor>();
            ua.BindTo(anchor);
            yield return null;   // let the anchor drive the transform once
            var pose = anchorGo.transform;
            if (cell != null) cell.frontOverride = pose.forward;
            var root = calibrator.SpawnWithBaseAt(pose.position + Vector3.up * tableHeight, pose.rotation);
            while (!calibrator.Placed) yield return null;
            root.transform.SetParent(anchorGo.transform, true);
            _phase = Phase.Placed;
            Debug.Log("[CellPlacer] cell restored from anchor " + uuid);
            done(true);
        }
#endif

        // Hand menu "Place": forget the saved anchor and place the cell again.
        public void Replace() => ForgetAndReplace();

        // On the headset the whole app restarts: a reloaded scene came back with the tracking
        // origin shifted (the cell looked smaller and sunk into the floor), a fresh start is
        // exactly the first launch. The editor just reloads the scene.
        void ForgetAndReplace()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("[CellPlacer] anchor forgotten - restarting the app to place again");
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, ReplaceMarker), "1");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
                using (var intent = pm.Call<AndroidJavaObject>("getLaunchIntentForPackage", activity.Call<string>("getPackageName")))
                {
                    intent.Call<AndroidJavaObject>("addFlags", 0x10008000);   // NEW_TASK | CLEAR_TASK
                    activity.Call("startActivity", intent);
                }
                using (var sys = new AndroidJavaClass("java.lang.System")) sys.CallStatic("exit", 0);
                return;
            }
            catch (System.Exception e) { Debug.LogWarning("[CellPlacer] restart failed, reloading the scene: " + e.Message); }
#endif
            Debug.Log("[CellPlacer] anchor forgotten - reloading to place again");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ------------------------------------------------------------ ghost footprint

        void BuildGhost()
        {
            _ghost = new GameObject("PlacementGhost");
            // cell frame: origin under the robot foot, +Z towards the user, y = floor here
            Rect(_ghost.transform, new Vector3(-0.15f, 0f, 0f), new Vector2(1.00f, 0.60f), new Color(0.4f, 0.9f, 1f));   // robot table
            Rect(_ghost.transform, new Vector3(0f, 0f, 0.74f), new Vector2(1.24f, 0.86f), new Color(0.4f, 0.9f, 1f));    // flow rack
            Circle(_ghost.transform, 0.6f, new Color(0.95f, 0.25f, 0.22f), 0.02f);
            Circle(_ghost.transform, 2.0f, new Color(1f, 0.62f, 0.1f), 0.02f);
            // where the factory wall will stand: line it up with a real wall, or keep it clear of one
            WallGhost(_ghost.transform, -1.29f, 1.70f, 2.90f, new Color(0.4f, 0.9f, 1f, 0.9f));
            // where the visitor is meant to stand: step into the white circle, then confirm
            var spot = Circle(_ghost.transform, 0.35f, new Color(1f, 1f, 1f, 0.95f), 0.035f);
            spot.transform.localPosition = new Vector3(VisitorSpot.x, 0.01f, VisitorSpot.y);
            FloorLabel("YOU", new Vector3(VisitorSpot.x, 0.012f, VisitorSpot.y), 0.11f, Color.white);
            FloorLabel("ROBOT", new Vector3(-0.15f, 0.012f, 0f), 0.10f, new Color(0.4f, 0.9f, 1f));
            FloorLabel("RACK", new Vector3(0f, 0.012f, 0.74f), 0.10f, new Color(0.4f, 0.9f, 1f));
            UprightLabel("WALL", new Vector3(0f, 1.45f, -1.27f), 0.22f, new Color(0.4f, 0.9f, 1f));
            _ringLine = Circle(_ghost.transform, 0.18f, Color.white, 0.03f);
            _ring = _ringLine.transform;
            _ring.localPosition = new Vector3(VisitorSpot.x, 0.01f, VisitorSpot.y - 0.75f);   // progress ring just in front of YOU
            UpdateRing(0f);
        }

        // The visitor's place in the cell frame (x, z): in front of the rack, beside the trolley.
        static readonly Vector2 VisitorSpot = new Vector2(0.30f, 2.45f);

        // Upright outline of the wall at cell z, half-width hx, height h, with a few uprights so
        // it reads as a surface.
        void WallGhost(Transform parent, float z, float hx, float h, Color col)
        {
            var go = new GameObject("WallGhost");
            go.transform.SetParent(parent, false);
            var pts = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(-hx, 0f, z), new Vector3(hx, 0f, z), new Vector3(hx, h, z), new Vector3(-hx, h, z), new Vector3(-hx, 0f, z)
            };
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = false; lr.widthMultiplier = 0.025f;
            lr.sharedMaterial = ghostMaterial; lr.startColor = lr.endColor = col;
            lr.positionCount = pts.Count; lr.SetPositions(pts.ToArray());
            for (int i = 1; i < 4; i++)
            {
                float x = -hx + i * hx * 2f / 4f;
                var u = new GameObject("Upright").AddComponent<LineRenderer>();
                u.transform.SetParent(go.transform, false);
                u.useWorldSpace = false; u.widthMultiplier = 0.012f; u.sharedMaterial = ghostMaterial;
                var c = col; c.a *= 0.5f; u.startColor = u.endColor = c;
                u.positionCount = 2; u.SetPositions(new[] { new Vector3(x, 0f, z), new Vector3(x, h, z) });
            }
        }

        // Word lying on the floor, readable from the visitor's side (+Z).
        void FloorLabel(string text, Vector3 at, float h, Color c)
        {
            var t = Label(text, h, c);
            if (t == null) return;
            t.transform.localPosition = at;
            t.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
        }

        // Word standing up, facing the visitor (+Z).
        void UprightLabel(string text, Vector3 at, float h, Color c)
        {
            var t = Label(text, h, c);
            if (t == null) return;
            t.transform.localPosition = at;
            t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        TMPro.TextMeshPro Label(string text, float h, Color c)
        {
            if (labelFont == null) return null;
            var go = new GameObject("Label_" + text, typeof(TMPro.TextMeshPro));
            go.transform.SetParent(_ghost.transform, false);
            var t = go.GetComponent<TMPro.TextMeshPro>();
            t.font = labelFont; t.text = text; t.color = c;
            t.fontStyle = TMPro.FontStyles.Bold; t.characterSpacing = 10f;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.fontSize = h * 10f;   // TextMeshPro: font size 10 = 1 m line height
            ((RectTransform)go.transform).sizeDelta = new Vector2(3f, h * 1.5f);
            return t;
        }

        LineRenderer Line(Transform parent, string name, Color c, float w)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = true; lr.widthMultiplier = w;
            lr.sharedMaterial = ghostMaterial;
            lr.startColor = lr.endColor = c;
            lr.alignment = LineAlignment.TransformZ;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return lr;
        }

        void Rect(Transform parent, Vector3 c, Vector2 size, Color col)
        {
            var lr = Line(parent, "Rect", col, 0.025f);
            lr.positionCount = 4;
            float x = size.x / 2f, z = size.y / 2f;
            // line lies in local XY because of the 90 deg tilt about X: local Y -> ghost +Z
            lr.SetPositions(new[] {
                new Vector3(c.x - x, c.z - z, 0f), new Vector3(c.x + x, c.z - z, 0f),
                new Vector3(c.x + x, c.z + z, 0f), new Vector3(c.x - x, c.z + z, 0f) });
        }

        LineRenderer Circle(Transform parent, float r, Color col, float w)
        {
            var lr = Line(parent, "Circle", col, w);
            int n = 72; lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            return lr;
        }

        void UpdateRing(float t)
        {
            if (_ringLine == null) return;
            var c = Color.Lerp(new Color(1f, 1f, 1f, 0.5f), new Color(0.3f, 1f, 0.5f, 1f), t);
            _ringLine.startColor = _ringLine.endColor = c;
            _ring.localScale = Vector3.one * (1f + 0.4f * t);
        }
    }
}
