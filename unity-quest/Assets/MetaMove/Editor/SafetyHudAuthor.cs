#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using MetaMove.Safety;
using MetaMove.UI.Visualization;

namespace MetaMove.EditorTools
{
    // One-click minimal Safety HUD: a head-followed world-space panel with three
    // readouts — CONNECTION / DISTANCE (m) / SPEED (%) — driven by SafetyHud.
    //
    // Menu builds the ROS variant (Scene_QRTest). BuildHudPanel() is reused by
    // VirtualDemoAuthor for the local (no-ROS) digital demo.
    public static class SafetyHudAuthor
    {
        [MenuItem("MetaMove/Create Safety HUD (Connected-Distance-Speed)")]
        public static void Create()
        {
            var head = GameObject.Find("CenterEyeAnchor")
                       ?? (Camera.main != null ? Camera.main.gameObject : null);
            if (head == null)
            {
                Debug.LogError("[SafetyHudAuthor] No CenterEyeAnchor / Main Camera found.");
                return;
            }

            // Gather human points + robot base from existing DistanceRulers.
            var rulers = Object.FindObjectsByType<DistanceRuler>(FindObjectsSortMode.None);
            var humanPoints = new List<Transform>();
            Transform robotBase = null;
            foreach (var r in rulers)
            {
                if (r.source != null && !humanPoints.Contains(r.source)) humanPoints.Add(r.source);
                if (r.target != null) robotBase = r.target;
            }
            if (humanPoints.Count == 0) humanPoints.Add(head.transform);

            var hud = BuildHudPanel(head.transform, robotBase, humanPoints.ToArray(), useRos: true);

            // Wire QR anchor -> SetRobotBase so it tracks even if anchor spawns later.
            var calib = Object.FindFirstObjectByType<QrAnchorCalibrator>();
            if (calib != null)
                UnityEditor.Events.UnityEventTools.AddPersistentListener(calib.onAnchorSpawned, hud.SetRobotBase);

            // Ensure a ROSConnection exists.
            if (Object.FindFirstObjectByType<Unity.Robotics.ROSTCPConnector.ROSConnection>() == null)
                new GameObject("ROSConnection").AddComponent<Unity.Robotics.ROSTCPConnector.ROSConnection>();

            Selection.activeObject = hud.gameObject;
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[SafetyHudAuthor] ROS HUD under {head.name}. humanPoints={humanPoints.Count}, " +
                      $"robotBase={(robotBase ? robotBase.name : "<runtime via QR>")}, " +
                      (calib != null ? "wired onAnchorSpawned->SetRobotBase." : "no calibrator."));
        }

        // Builds the 3-readout HUD panel under headAnchor and returns the SafetyHud.
        // useRos=false -> local digital-demo mode (caller sets localController/modeSwitch).
        public static SafetyHud BuildHudPanel(Transform headAnchor, Transform robotBase,
                                              Transform[] humanPoints, bool useRos)
        {
            var existing = GameObject.Find("SafetyHUD");
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject("SafetyHUD");
            root.transform.SetParent(headAnchor, false);
            root.transform.localPosition = new Vector3(0f, -0.18f, 0.6f);
            root.transform.localRotation = Quaternion.identity;

            var canvasGo = new GameObject("Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(root.transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var crt = (RectTransform)canvasGo.transform;
            crt.sizeDelta = new Vector2(540, 190);
            crt.localScale = Vector3.one * 0.001f;

            var bg = NewRect(crt, "BG", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bg.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.07f, 0.12f, 0.82f);

            var connectedVal = BuildReadout(crt, "Connected", "CONNECTION", 0f);
            var distanceVal  = BuildReadout(crt, "Distance",  "DISTANCE",   1f / 3f);
            var speedVal     = BuildReadout(crt, "Speed",     "SPEED",      2f / 3f);

            var hud = root.AddComponent<SafetyHud>();
            hud.connectedText = connectedVal;
            hud.distanceText = distanceVal;
            hud.speedText = speedVal;
            hud.humanPoints = humanPoints;
            hud.robotBase = robotBase;
            hud.useRos = useRos;
            return hud;
        }

        static TMP_Text BuildReadout(RectTransform parent, string id, string label, float xFrac)
        {
            var col = NewRect(parent, $"Col_{id}",
                new Vector2(xFrac, 0f), new Vector2(xFrac + 1f / 3f, 1f),
                new Vector2(16, 16), new Vector2(-16, -16));

            var lab = NewRect(col, "Label", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -42), new Vector2(0, -6));
            var labTmp = lab.gameObject.AddComponent<TextMeshProUGUI>();
            labTmp.text = label;
            labTmp.fontSize = 22f;
            labTmp.color = new Color(0.6f, 0.66f, 0.74f);
            labTmp.alignment = TextAlignmentOptions.Center;
            labTmp.textWrappingMode = TextWrappingModes.NoWrap;

            var val = NewRect(col, "Value", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, 8), new Vector2(0, -50));
            var valTmp = val.gameObject.AddComponent<TextMeshProUGUI>();
            valTmp.text = "--";
            valTmp.fontStyle = FontStyles.Bold;
            valTmp.color = Color.white;
            valTmp.alignment = TextAlignmentOptions.Center;
            valTmp.textWrappingMode = TextWrappingModes.NoWrap;
            valTmp.enableAutoSizing = true;
            valTmp.fontSizeMin = 14f;
            valTmp.fontSizeMax = 52f;
            return valTmp;
        }

        static RectTransform NewRect(RectTransform parent, string name,
            Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = oMin; rt.offsetMax = oMax;
            return rt;
        }
    }
}
#endif
