#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MetaMove.Robot;
using MetaMove.Safety;

namespace MetaMove.EditorTools
{
    // One-click setup for Scene_SafetyAnchorTest.
    // Menu: MetaMove > Setup Safety Anchor Test Scene (overwrite)
    //
    // Auto-wires everything that can be done without manual drag-drop:
    //   - Directional Light + XR Origin rig
    //   - MRUK prefab (Meta's scene/marker stack — enables QR tracking)
    //   - Spatial Anchor Core BB prefab
    //   - AnchoredRobotBase prefab: GoFa + AnchoredBaseBinder (auto-created if not present)
    //   - QrAnchorCalibrator wired to that prefab with payload "METAMOVE_ROBOT_BASE_01"
    //   - TcpMock + SafetyZone_TCP (Sphere, follows TcpMock)
    //   - SafetyZone_StaticBox (drift reference, world-space)
    //   - RobotTelemetry + SpeedScaler
    //   - AnchorDriftHud
    //
    // Things still needing user action (flagged in the final dialog):
    //   1. On MRUK, tick TrackerConfiguration.QRCodeTrackingEnabled in the inspector
    //   2. Print docs/markers/METAMOVE_ROBOT_BASE_01_print.pdf at 100% scale
    //   3. Attach marker to real-world object
    //   4. Add scene to Build Settings
    public static class SafetyAnchorTestSceneSetup
    {
        const string ScenePath = "Assets/MetaMove/Scenes/Playground/Scene_SafetyAnchorTest.unity";
        const string AnchorPrefabPath = "Assets/MetaMove/Prefabs/AnchoredRobotBase.prefab";

        const string XrOriginPrefabGuid = "f6336ac4ac8b4d34bc5072418cdc62a0";
        const string GoFaPrefabGuid = "56b9b012d95a18146a21065b7c36fa81";
        const string MrukPrefabGuid = "c2e00db6d2dfa42ac8d821db8d493a44";
        const string SpatialAnchorCoreGuid = "97dc87d8a31752848aa51059a8287dd2";

        const string QrPayload = "METAMOVE_ROBOT_BASE_01";

        [MenuItem("MetaMove/Setup Safety Anchor Test Scene (overwrite)")]
        public static void Build()
        {
            if (File.Exists(ScenePath))
            {
                bool ok = EditorUtility.DisplayDialog(
                    "MetaMove — Setup Safety Anchor Test Scene",
                    $"This will OVERWRITE:\n  {ScenePath}\n\nContinue?",
                    "Overwrite", "Cancel");
                if (!ok) return;
            }

            EnsureFolder("Assets/MetaMove/Scenes/Playground");
            EnsureFolder("Assets/MetaMove/Prefabs");

            // 1. Build AnchoredRobotBase prefab if missing.
            var anchorPrefab = EnsureAnchoredRobotBasePrefab();

            // 2. Create the scene.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // XR Rig
            var xrOriginPrefab = LoadPrefab(XrOriginPrefabGuid, "XR Origin (XR Rig)");
            if (xrOriginPrefab != null)
            {
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(xrOriginPrefab);
                rig.name = "XR Origin (XR Rig)";
            }
            else
            {
                Debug.LogWarning("[SafetyAnchorTestSceneSetup] XR Origin prefab not found — add manually.");
                var cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                cam.AddComponent<Camera>();
                cam.AddComponent<AudioListener>();
                cam.transform.position = new Vector3(0f, 1.6f, 0f);
            }

            // MRUK (enables QR tracking)
            var mrukPrefab = LoadPrefab(MrukPrefabGuid, "MRUK");
            if (mrukPrefab != null)
            {
                var mruk = (GameObject)PrefabUtility.InstantiatePrefab(mrukPrefab);
                mruk.name = "MRUK";
            }
            else
            {
                Debug.LogWarning("[SafetyAnchorTestSceneSetup] MRUK prefab not found — install com.meta.xr.mrutilitykit or add manually via Building Blocks.");
            }

            // Spatial Anchor Core BB
            var saCorePrefab = LoadPrefab(SpatialAnchorCoreGuid, "SpatialAnchorCore");
            if (saCorePrefab != null)
            {
                var sa = (GameObject)PrefabUtility.InstantiatePrefab(saCorePrefab);
                sa.name = "[BuildingBlock] Spatial Anchor Core";
            }

            // TCP mock
            var tcp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tcp.name = "TcpMock";
            tcp.transform.localScale = Vector3.one * 0.03f;
            tcp.transform.position = new Vector3(0.3f, 1.1f, 0.6f);
            var col = tcp.GetComponent<Collider>(); if (col != null) Object.DestroyImmediate(col);

            // Dynamic sphere zone that follows TCP
            var zoneDyn = new GameObject("SafetyZone_TCP");
            var szDyn = zoneDyn.AddComponent<SafetyZone>();
            szDyn.shape = ZoneShape.Sphere;
            szDyn.radiusMeters = 0.30f;
            szDyn.mode = ZoneMode.ReducedSpeed;
            szDyn.reducedFraction = 0.3f;
            szDyn.followTarget = tcp.transform;

            // Static box (drift reference — world-space, NOT under anchor)
            var zoneStatic = new GameObject("SafetyZone_StaticBox");
            zoneStatic.transform.position = new Vector3(0f, 1.0f, 0.6f);
            var szStat = zoneStatic.AddComponent<SafetyZone>();
            szStat.shape = ZoneShape.Box;
            szStat.halfExtents = new Vector3(0.4f, 0.4f, 0.4f);
            szStat.mode = ZoneMode.MonitoredStandstill;

            // Telemetry + SpeedScaler
            var tel = new GameObject("RobotTelemetry");
            var telemetry = tel.AddComponent<RobotTelemetry>();
            var scaler = tel.AddComponent<SpeedScaler>();
            scaler.telemetry = telemetry;
            scaler.zones.Add(szDyn);
            scaler.zones.Add(szStat);

            // QR Anchor Calibrator
            var qrGo = new GameObject("QrCalibrator");
            var qr = qrGo.AddComponent<QrAnchorCalibrator>();
            qr.expectedPayload = QrPayload;
            qr.anchorPrefab = anchorPrefab;
            qr.printedSizeMeters = 0.1f;
            qr.minStableFrames = 10;

            // Drift HUD
            var hudGo = new GameObject("AnchorDriftHud");
            hudGo.AddComponent<AnchorDriftHud>();

            // Save scene
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[SafetyAnchorTestSceneSetup] wrote {ScenePath}");

            EditorUtility.DisplayDialog(
                "MetaMove — Safety Anchor Test Scene",
                $"Scene created:\n{ScenePath}\n\n" +
                "AUTO-WIRED: MRUK, Spatial Anchor Core, AnchoredRobotBase prefab, QrCalibrator,\n" +
                "safety zones, telemetry, HUD.\n\n" +
                "STILL TO DO IN EDITOR:\n" +
                "  1. Click MRUK → Settings → TrackerConfiguration → enable 'QR Code Tracking Enabled'\n" +
                "  2. File → Build Settings → Add Open Scenes\n\n" +
                "STILL TO DO OUTSIDE EDITOR:\n" +
                "  3. Print docs/markers/METAMOVE_ROBOT_BASE_01_print.pdf at 100% scale\n" +
                "  4. Attach the printed marker to your test object (flat, center = CAD origin)\n\n" +
                "THEN: Build & Run. See docs/safety-anchor-test.md for full walkthrough.",
                "OK");
        }

        static GameObject EnsureAnchoredRobotBasePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(AnchorPrefabPath);
            if (existing != null) return existing;

            var gofa = LoadPrefab(GoFaPrefabGuid, "GoFa_CRB15000_5_95");
            if (gofa == null)
            {
                Debug.LogError("[SafetyAnchorTestSceneSetup] Cannot create AnchoredRobotBase: GoFa prefab missing.");
                return null;
            }

            var root = new GameObject("AnchoredRobotBase");
            var binder = root.AddComponent<AnchoredBaseBinder>();
            var gofaInst = (GameObject)PrefabUtility.InstantiatePrefab(gofa, root.transform);
            gofaInst.transform.localPosition = Vector3.zero;
            gofaInst.transform.localRotation = Quaternion.identity;
            binder.mountedRoot = gofaInst.transform;

            var saved = PrefabUtility.SaveAsPrefabAsset(root, AnchorPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[SafetyAnchorTestSceneSetup] created {AnchorPrefabPath}");
            return saved;
        }

        static GameObject LoadPrefab(string guid, string label)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[SafetyAnchorTestSceneSetup] {label} prefab GUID {guid} not found");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
