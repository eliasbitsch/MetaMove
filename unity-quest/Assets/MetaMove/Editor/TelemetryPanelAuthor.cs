#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using MetaMove.Robot;
using MetaMove.UI.Overlays;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.Surfaces;
using JointLimits = MetaMove.Robot.JointLimits;

namespace MetaMove.EditorTools
{
    // One-click: builds a floating Meta UI-Set panel hosting the ShowPose TCP readout.
    // The backplate is Meta's own `EmptyUIBackplateWithCanvas` (PointableCanvas already wired),
    // the grab affordance uses DistanceHandGrabInteractable + OneGrabFreeTransformer per the
    // BB table in HANDOFF.md (panel is draggable with near or ray pinch).
    //
    // Menu: MetaMove > Create Telemetry Panel (Meta UI)
    public static class TelemetryPanelAuthor
    {
        const string BackplatePath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/UISet/Prefabs/Backplate/EmptyUIBackplateWithCanvas.prefab";
        const string TcpPanelPath = "Assets/MetaMove/Prefabs/TelemetryPanel.prefab";
        const string JointPanelPath = "Assets/MetaMove/Prefabs/JointStatusPanel.prefab";
        const string SafetyPanelPath = "Assets/MetaMove/Prefabs/SafetyStatusPanel.prefab";

        [MenuItem("MetaMove/Create TCP Pose Panel (Meta UI)")]
        public static void CreateTcpPanel() => CreatePanel("TelemetryPanel", TcpPanelPath, PopulateTcp);

        [MenuItem("MetaMove/Create Joint Status Panel (Meta UI)")]
        public static void CreateJointPanel() => CreatePanel("JointStatusPanel", JointPanelPath, PopulateJointStatus);

        [MenuItem("MetaMove/Create Safety Status Panel (Meta UI)")]
        public static void CreateSafetyPanel() => CreatePanel("SafetyStatusPanel", SafetyPanelPath, PopulateSafety);

        static void CreatePanel(string goName, string prefabPath, System.Action<Transform> populate)
        {
            var backplate = AssetDatabase.LoadAssetAtPath<GameObject>(BackplatePath);
            if (backplate == null)
            {
                EditorUtility.DisplayDialog("Meta UI Panel",
                    $"Could not find Meta UI-Set backplate at:\n{BackplatePath}\n\n" +
                    "Install the `com.meta.xr.sdk.interaction` package and import the UISet sample.",
                    "OK");
                return;
            }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(backplate);
            root.name = goName;
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.transform.localPosition = new Vector3(0.5f, 1.4f, 0.8f);
            root.transform.localRotation = Quaternion.Euler(0, -20f, 0);
            root.transform.localScale = Vector3.one;

            // Ensure PointableCanvas is present so Meta's ray interactors hit this panel.
            var canvas = root.GetComponentInChildren<Canvas>();
            if (canvas == null) canvas = EnsureCanvas(root);
            if (canvas.GetComponent<PointableCanvas>() == null)
                canvas.gameObject.AddComponent<PointableCanvas>();

            populate(canvas.transform);
            AddGrabAffordance(root);

            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath)!);
            var savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkAllScenesDirty();

            Selection.activeObject = savedPrefab;
            EditorUtility.DisplayDialog("Meta UI Panel",
                $"Created {prefabPath}\n\n" +
                "Wire RobotTelemetry / SpeedScaler refs in the inspector if not auto-wired.\n" +
                "Panel is grabbable via ray or near pinch (Meta BBs).",
                "OK");
        }

        static void PopulateTcp(Transform canvas)
        {
            AddTitle(canvas, "TCP Pose");
            var body = AddBody(canvas);
            var tcp = body.gameObject.AddComponent<ShowPose>();
            tcp.label = body;
            var tel = Object.FindObjectOfType<RobotTelemetry>();
            if (tel != null) tcp.telemetry = tel;
        }

        static void PopulateJointStatus(Transform canvas)
        {
            AddTitle(canvas, "Joints");
            var body = AddBody(canvas);
            var mirror = body.gameObject.AddComponent<ShowJointStatusText>();
            mirror.label = body;
            var tel = Object.FindObjectOfType<RobotTelemetry>();
            if (tel != null) mirror.telemetry = tel;
            mirror.limits = AssetDatabase.LoadAssetAtPath<JointLimits>("Assets/MetaMove/Robot/JointLimits_GoFa5_95.asset");
        }

        static void PopulateSafety(Transform canvas)
        {
            AddTitle(canvas, "Safety");
            var body = AddBody(canvas);
            var mirror = body.gameObject.AddComponent<ShowSafetyStatusText>();
            mirror.label = body;
            var scaler = Object.FindObjectOfType<MetaMove.Safety.SpeedScaler>();
            if (scaler != null) mirror.scaler = scaler;
        }

        static Canvas EnsureCanvas(GameObject root)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(root.transform, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(400, 520);
            rt.localScale = Vector3.one * 0.001f;
            return c;
        }

        static void AddTitle(Transform parent, string text)
        {
            var go = new GameObject("Title", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0, 64);
            rt.anchoredPosition = new Vector2(0, -16);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
        }

        static TMP_Text AddBody(Transform parent)
        {
            var go = new GameObject("Body", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(32, 32); rt.offsetMax = new Vector2(-32, -96);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.fontSize = 28;
            tmp.color = new Color(0.9f, 0.95f, 1f);
            tmp.lineSpacing = 6f;
            return tmp;
        }

        static void AddGrabAffordance(GameObject root)
        {
            var colGo = new GameObject("GrabSurface", typeof(BoxCollider));
            colGo.transform.SetParent(root.transform, false);
            colGo.transform.localPosition = Vector3.zero;
            var col = colGo.GetComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.45f, 0.55f, 0.02f);

            var rb = colGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var transformer = colGo.AddComponent<OneGrabFreeTransformer>();

            var grabbable = colGo.AddComponent<Grabbable>();
            grabbable.InjectOptionalTargetTransform(root.transform);
            grabbable.InjectOptionalRigidbody(rb);
            grabbable.InjectOptionalOneGrabTransformer(transformer);
            grabbable.InjectOptionalKinematicWhileSelected(false);

            var handGrab = colGo.AddComponent<HandGrabInteractable>();
            handGrab.InjectAllHandGrabInteractable(
                GrabTypeFlags.All,
                rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);

            var distGrab = colGo.AddComponent<DistanceHandGrabInteractable>();
            distGrab.InjectAllDistanceHandGrabInteractable(
                GrabTypeFlags.Pinch,
                rb,
                GrabbingRule.DefaultPinchRule,
                GrabbingRule.DefaultPalmRule);
        }
    }
}
#endif
