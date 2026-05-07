#if UNITY_EDITOR
using System.IO;
using MetaMove.Robot.EGM;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetaMove.EditorTools
{
    // Erzeugt eine minimale Test-Szene mit EgmClient + EgmKeyboardTester,
    // damit am Lab nix in Scene_Robot kaputtgeht.
    // Aufruf: MetaMove → EGM → Create Keyboard Test Scene
    public static class EgmTestSceneBuilder
    {
        const string ScenePath = "Assets/MetaMove/Scenes/Scene_EGM_KeyboardTest.unity";

        [MenuItem("MetaMove/EGM/Create Keyboard Test Scene")]
        public static void CreateScene()
        {
            var dir = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Kamera so positionieren, dass das OnGUI-Overlay unbehindert sichtbar ist.
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 1.5f, -3f);
                cam.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
            }

            // EGM-Tester-GameObject
            var go = new GameObject("EGM_Tester");
            var egm = go.AddComponent<EgmClient>();
            egm.listenPort = 6511;
            egm.autoDetectRemote = true;
            egm.remoteHost = "192.168.125.1";
            egm.remotePort = 6511;

            var tester = go.AddComponent<EgmKeyboardTester>();
            tester.streamRateHz = 250f;

            // Sichtbares Marker-Cube als Target-Visualisierung (nur Anzeige, kein Logik-Bezug)
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "TargetMarker";
            marker.transform.localScale = Vector3.one * 0.05f;
            marker.transform.position = new Vector3(0.4f, 0.6f, 0f);
            var mr = marker.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null) mr.sharedMaterial.color = new Color(1f, 0.6f, 0.1f);
            Object.DestroyImmediate(marker.GetComponent<BoxCollider>());

            // Info-Text-GameObject (rein dokumentarisch, keine Component)
            var info = new GameObject("README — Arrows=XY, Q/E=Z, Shift=fast, H=HOME, F1=set HOME");
            info.transform.SetParent(go.transform, false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "EGM Test Scene",
                $"Erstellt: {ScenePath}\n\nGameObject 'EGM_Tester' enthält EgmClient + EgmKeyboardTester.\n\nVor Play-Mode: GoFa-RAPID muss EGMRunPose streamen (siehe Recovery-Steps).",
                "OK");

            Selection.activeGameObject = go;
        }
    }
}
#endif
