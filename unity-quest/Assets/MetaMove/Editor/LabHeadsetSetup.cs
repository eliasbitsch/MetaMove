#if UNITY_EDITOR
using System;
using System.Linq;
using MetaMove.Safety;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Scene_Robot / headset settings for the lab:
    //  - MRUK no longer loads the room scene on start. The app only needs QR trackables;
    //    loading the scene is what makes Horizon OS ask "update your space?" on every launch.
    //  - Immersive Debugger on (menu button), with TwinAlignment on the QR calibrator so the
    //    twin can be slid onto the real robot in the headset.
    // Idempotent. -executeMethod MetaMove.EditorTools.LabHeadsetSetup.Apply
    public static class LabHeadsetSetup
    {
        const string Scene = "Assets/MetaMove/Scenes/Scene_Robot.unity";
        const string DebuggerSettings = "Assets/Resources/ImmersiveDebuggerSettings.asset";

        [MenuItem("MetaMove/Lab headset setup (no room prompt, twin alignment debugger)")]
        public static void Apply()
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);

            // 1. MRUK: keep QR tracking, skip the room scene.
            var mruk = UnityEngine.Object.FindFirstObjectByType<Meta.XR.MRUtilityKit.MRUK>(FindObjectsInactive.Include);
            var so = new SerializedObject(mruk);
            var load = so.FindProperty("SceneSettings.LoadSceneOnStartup");
            var qr = so.FindProperty("SceneSettings.<TrackerConfiguration>k__BackingField.<QRCodeTrackingEnabled>k__BackingField");
            Debug.Log($"[LabHeadsetSetup] MRUK LoadSceneOnStartup {load?.boolValue} -> false, QR tracking {qr?.boolValue}");
            if (load != null) load.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 2. Twin alignment on the calibrator.
            var cal = UnityEngine.Object.FindFirstObjectByType<QrAnchorCalibrator>(FindObjectsInactive.Include);
            var align = cal.GetComponent<TwinAlignment>();
            if (align == null) align = cal.gameObject.AddComponent<TwinAlignment>();
            align.calibrator = cal;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            // 3. Immersive Debugger on, and its list of inspectable types re-baked.
            var settings = AssetDatabase.LoadMainAssetAtPath(DebuggerSettings);
            var sso = new SerializedObject(settings);
            sso.FindProperty("immersiveDebuggerEnabled").boolValue = true;
            sso.FindProperty("immersiveDebuggerDisplayAtStartup").boolValue = false;
            sso.FindProperty("showInspectors").boolValue = true;
            sso.ApplyModifiedPropertiesWithoutUndo();
            // The SDK scans assemblies in a background task that a batch run does not wait for;
            // collect the [DebugMember] types synchronously and hand them to the same internal setter.
            var types = TypeCache.GetFieldsWithAttribute<Meta.XR.ImmersiveDebugger.DebugMember>().Select(f => f.DeclaringType)
                .Concat(TypeCache.GetMethodsWithAttribute<Meta.XR.ImmersiveDebugger.DebugMember>().Select(m => m.DeclaringType))
                .Where(t => t != null && typeof(UnityEngine.Object).IsAssignableFrom(t))
                .Distinct().ToList();
            var rs = AppDomain.CurrentDomain.GetAssemblies()
                .Select(asm => asm.GetType("Meta.XR.ImmersiveDebugger.RuntimeSettings"))
                .First(t => t != null);
            rs.GetMethod("UpdateAllDebugTypes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
              .Invoke(null, new object[] { types, settings });
            Debug.Log($"[LabHeadsetSetup] debug types: {string.Join(", ", types.Select(t => t.Name))}");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            string yaml = System.IO.File.ReadAllText(DebuggerSettings);
            bool registered = yaml.Contains("TwinAlignment");
            Debug.Log($"[LabHeadsetSetup] LABHEADSET_DONE debugger enabled, TwinAlignment registered={registered}");
            if (Application.isBatchMode) EditorApplication.Exit(registered ? 0 : 2);
        }
    }
}
#endif
