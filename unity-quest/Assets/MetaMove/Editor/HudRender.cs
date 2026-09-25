#if UNITY_EDITOR
using System.IO;
using System.Linq;
using MetaMove.Safety;
using MetaMove.UI.Hud;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Renders the headset HUD offscreen with the LONGEST texts it can show, so overlaps are
    // visible without a headset. Run WITHOUT -nographics (it needs a GPU):
    //   Unity.exe -batchmode -projectPath unity-quest -executeMethod MetaMove.EditorTools.HudRender.Run -hudOut <png>
    public static class HudRender
    {
        public static void Run()
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, "-hudOut");
            string outPath = i >= 0 ? a[i + 1] : "hud.png";

            EditorSceneManager.OpenScene("Assets/MetaMove/Scenes/Scene_Robot.unity", OpenSceneMode.Single);
            var hud = Object.FindFirstObjectByType<SafetyHud>(FindObjectsInactive.Include);
            var arc = hud.GetComponent<HudArc>();
            if (arc != null) arc.Apply();
            typeof(SafetyHud).GetMethod("BuildSpeedBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(hud, null);

            // Worst case texts.
            hud.connectedText.text = "NOT CONNECTED";
            hud.distanceText.text = "10.00 m";
            hud.speedText.text = "MANUELL 100 %";
            foreach (var t in hud.GetComponentsInChildren<TMP_Text>(true))
                if (t.transform.parent != null && t.transform.parent.name == "Track" || t.name == "Label" && t.transform.parent.parent.name == "SpeedBar")
                    t.text = "ROBOT SPEED 100 %  |  MAX 100 %";
            foreach (var t in hud.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();

            // Camera where the eyes are: the HUD group's parent is the CenterEyeAnchor.
            var group = hud.transform.parent;
            var eye = group.parent;
            var camGo = new GameObject("HudRenderCam");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(eye.position, Quaternion.LookRotation(group.position - eye.position, Vector3.up));
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.58f, 0.6f);   // passthrough-ish grey
            cam.nearClipPlane = 0.05f;
            var rt = new RenderTexture(1800, 700, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log($"[HudRender] HUD_RENDERED {outPath}");
            EditorApplication.Exit(0);
        }
    }
}
#endif
