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
    //      floor where the right hand's pinch ray - or, without hands, the gaze - meets it,
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

        const string PrefKey = "MetaMove.Bari.CellAnchor";
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
            var saved = PlayerPrefs.GetString(PrefKey, "");
            if (Guid.TryParse(saved, out var uuid))
            {
                _phase = Phase.Loading;
                bool ok = false;
                yield return LoadAnchor(uuid, r => ok = r);
                if (ok) yield break;
                Debug.LogWarning("[CellPlacer] saved anchor not found - placing again");
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
            bool rPinch = Pinching(rightHand) || OVRInput.Get(OVRInput.Button.One);

            if (_phase != Phase.Placing) return;
            if (!FloorTarget(out _point)) return;
            Vector3 toUser = head.position - _point; toUser.y = 0f;
            if (toUser.sqrMagnitude < 1e-4f) toUser = Vector3.back;
            _rot = Quaternion.LookRotation(toUser.normalized, Vector3.up);   // cell +Z faces the user
            _ghost.transform.SetPositionAndRotation(_point, _rot);

            var kb = Keyboard.current;
            if (rPinch) _hold += Time.deltaTime; else _hold = 0f;
            UpdateRing(Mathf.Clamp01(_hold / holdToConfirm));
            if (_hold >= holdToConfirm || (kb != null && kb.pKey.wasPressedThisFrame)) Confirm();
        }

        bool Pinching(OVRHand h) =>
            h != null && h.IsTracked && h.GetFingerIsPinching(OVRHand.HandFinger.Index);

        // Where the right hand's pointer ray (or the gaze) hits the floor.
        bool FloorTarget(out Vector3 p)
        {
            float floorY = 0f;   // FloorLevel tracking origin
            var tracking = head != null ? head.parent : null;
            if (tracking != null) floorY = tracking.position.y;
            Ray ray;
            if (rightHand != null && rightHand.IsTracked && rightHand.IsPointerPoseValid)
                ray = new Ray(rightHand.PointerPose.position, rightHand.PointerPose.forward);
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
            Debug.Log("[CellPlacer] cell placed at " + floorPoint.ToString("F2") + " yaw " + rot.eulerAngles.y.ToString("F0"));
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

        void ForgetAndReplace()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
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
            _ringLine = Circle(_ghost.transform, 0.18f, Color.white, 0.03f);
            _ring = _ringLine.transform;
            _ring.localPosition = new Vector3(0f, 0.01f, 1.6f);   // progress ring on the user's side
            UpdateRing(0f);
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
