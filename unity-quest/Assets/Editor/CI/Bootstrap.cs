// One-shot project bootstrap: applies the Quest settings that are otherwise
// rediscovered by debugging a grey void in the headset every single project.
//
//   Unity.exe -batchmode -nographics -quit -projectPath <p> \
//             -executeMethod AgentCI.Bootstrap.Run -ciBundle com.ait.foo [-ciFlat]
//
// Every step is independently guarded and prints a CI_BOOT_* marker, so a partial
// failure (SDK not installed yet, API moved between versions) still leaves the
// project usable and tells the caller exactly which step needs a human.
//
// Meta and XR Management types are reached by reflection on purpose: this file has
// to compile in a project where those packages are not present.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AgentCI
{
    public static class Bootstrap
    {
        public static void Run()
        {
            var args = Environment.GetCommandLineArgs().ToList();
            string bundle = Arg(args, "-ciBundle", "com.ait.unnamed");
            bool flat = args.Contains("-ciFlat");

            Step("scene", () => EnsureScene());
            Step("player", () => PlayerSettingsForQuest(bundle, flat));

            if (!flat)
            {
                Step("xr-loader-android", () => AssignOpenXrLoader(BuildTargetGroup.Android));
                Step("xr-loader-standalone", () => AssignOpenXrLoader(BuildTargetGroup.Standalone));
                Step("ovr-project-config", () => ConfigureOvrProjectConfig());
                Step("ovr-manifest", () => RegenerateAndroidManifest());
                Step("ovr-fixall", () => OvrFixAll());
            }

            AssetDatabase.SaveAssets();
            Say("CI_BOOT_DONE bundle=" + bundle + " flat=" + flat);
            Quit(0);
        }

        // ------------------------------------------------------------- steps

        static void EnsureScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            string main = "Assets/Scenes/Main.unity";

            if (!File.Exists(main))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, main);
            }

            var wanted = new List<EditorBuildSettingsScene>();
            wanted.Add(new EditorBuildSettingsScene(main, true));
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != main) wanted.Add(s);
            EditorBuildSettings.scenes = wanted.ToArray();
            Say("CI_BOOT_SCENE " + main + " (build scenes: " + wanted.Count + ")");
        }

        static void PlayerSettingsForQuest(string bundle, bool flat)
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, bundle);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;

            if (!flat)
            {
                // Vulkan only. The GLES fallback silently costs the Quest-specific
                // render optimisations and is the usual reason a build is slower
                // than an otherwise identical one.
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });

                // Single-Pass Instanced: one draw call for both eyes.
                PlayerSettings.stereoRenderingPath = StereoRenderingPath.Instancing;

                // Desktop must be D3D11 for Link play-in-editor; OVR's own fixer
                // otherwise forces this later and demands an Editor restart.
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            }
            Say("CI_BOOT_PLAYER il2cpp arm64 bundle=" + bundle);
        }

        // In Unity 6 the Oculus XR Plugin is deprecated and its loader throws inside
        // OVRManager.InitOVRManager (grey void + Editor crash). OpenXR + MetaXRFeature
        // is the supported path, so that is the loader we assign.
        static void AssignOpenXrLoader(BuildTargetGroup group)
        {
            var storeType = FindType("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore");
            var settingsType = FindType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget");
            var generalType = FindType("UnityEngine.XR.Management.XRGeneralSettings");
            if (storeType == null || settingsType == null || generalType == null)
                throw new Exception("XR Management not installed (com.unity.xr.management)");

            string key = (string)generalType.GetField("k_SettingsKey", BindingFlags.Public | BindingFlags.Static).GetValue(null);

            var tryGet = typeof(EditorBuildSettings)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "TryGetConfigObject" && m.IsGenericMethod)
                .MakeGenericMethod(settingsType);

            object[] p = new object[] { key, null };
            bool found = (bool)tryGet.Invoke(null, p);
            object perTarget = p[1];

            if (!found || perTarget == null)
            {
                // First run: XR Management has never created its settings asset.
                var create = settingsType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
                if (create == null) throw new Exception("XRGeneralSettingsPerBuildTarget has no settings asset yet - open the Editor once (Project Settings > XR Plug-in Management)");
                perTarget = create.Invoke(null, null);
                EditorBuildSettings.AddConfigObject(key, (UnityEngine.Object)perTarget, true);
            }

            var getOrCreate = settingsType.GetMethod("GetOrCreate", BindingFlags.Public | BindingFlags.Instance);
            object general = getOrCreate != null
                ? getOrCreate.Invoke(perTarget, new object[] { group })
                : settingsType.GetMethod("SettingsForBuildTarget").Invoke(perTarget, new object[] { group });
            if (general == null) throw new Exception("no XRGeneralSettings for " + group);

            object manager = generalType.GetProperty("Manager", BindingFlags.Public | BindingFlags.Instance).GetValue(general, null);
            if (manager == null) throw new Exception("XRGeneralSettings.Manager is null for " + group);

            var assign = storeType.GetMethod("AssignLoader", BindingFlags.Public | BindingFlags.Static);
            bool ok = (bool)assign.Invoke(null, new object[] { manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group });
            if (!ok) throw new Exception("AssignLoader(OpenXRLoader) returned false for " + group);

            // Both loaders active at once is a hard conflict the OVR setup tool flags.
            var remove = storeType.GetMethod("RemoveLoader", BindingFlags.Public | BindingFlags.Static);
            if (remove != null)
            {
                try { remove.Invoke(null, new object[] { manager, "Unity.XR.Oculus.OculusLoader", group }); } catch { }
            }

            EditorUtility.SetDirty((UnityEngine.Object)general);
            Say("CI_BOOT_XR " + group + " loader=OpenXRLoader");
        }

        // Every one of these defaults to off/None in a fresh project, which is why
        // hands, passthrough and body tracking "do not work" out of the box.
        static void ConfigureOvrProjectConfig()
        {
            var cfgType = FindType("OVRProjectConfig");
            if (cfgType == null) throw new Exception("Meta XR SDK not installed (com.meta.xr.sdk.all)");

            object cfg = null;
            var getCached = cfgType.GetMethod("GetProjectConfig", BindingFlags.Public | BindingFlags.Static);
            if (getCached != null) cfg = getCached.Invoke(null, null);
            if (cfg == null)
            {
                var prop = cfgType.GetProperty("CachedProjectConfig", BindingFlags.Public | BindingFlags.Static);
                if (prop != null) cfg = prop.GetValue(null, null);
            }
            if (cfg == null) throw new Exception("could not obtain OVRProjectConfig");

            SetEnum(cfg, "handTrackingSupport", "ControllersAndHands");
            SetEnum(cfg, "handTrackingFrequency", "HIGH");
            SetEnum(cfg, "insightPassthroughSupport", "Supported");
            SetEnum(cfg, "bodyTrackingSupport", "Supported");
            SetEnum(cfg, "anchorSupport", "Enabled");
            SetEnum(cfg, "sceneSupport", "Supported");

            var commit = cfgType.GetMethod("CommitProjectConfig", BindingFlags.Public | BindingFlags.Static);
            if (commit != null) commit.Invoke(null, new object[] { cfg });
            EditorUtility.SetDirty((UnityEngine.Object)cfg);
            Say("CI_BOOT_OVRCFG hands+passthrough+body+anchors+scene");
        }

        static void RegenerateAndroidManifest()
        {
            var t = FindType("OVRManifestPreprocessor");
            if (t == null) throw new Exception("OVRManifestPreprocessor not found");
            var m = t.GetMethod("GenerateOrUpdateAndroidManifest", BindingFlags.Public | BindingFlags.Static);
            if (m == null) throw new Exception("GenerateOrUpdateAndroidManifest not found");
            var ps = m.GetParameters();
            if (ps.Length == 0) m.Invoke(null, null);
            else m.Invoke(null, new object[] { true });
            Say("CI_BOOT_MANIFEST regenerated");
        }

        // Turns on MetaXRFeature and the foveation/subsampling/touch-profile features
        // in the OpenXR settings - only effective once OpenXR is the assigned loader.
        static void OvrFixAll()
        {
            var t = FindType("OVRProjectSetup");
            if (t == null) throw new Exception("OVRProjectSetup not found");
            var m = t.GetMethod("FixAll", BindingFlags.Public | BindingFlags.Static)
                 ?? t.GetMethod("FixAllAsync", BindingFlags.Public | BindingFlags.Static);
            if (m == null) throw new Exception("neither FixAll nor FixAllAsync found");

            var ps = m.GetParameters();
            object[] callArgs = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].ParameterType == typeof(BuildTargetGroup)) callArgs[i] = BuildTargetGroup.Android;
                else if (ps[i].HasDefaultValue) callArgs[i] = ps[i].DefaultValue;
                else callArgs[i] = ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
            }
            m.Invoke(null, callArgs);
            Say("CI_BOOT_FIXALL " + m.Name + " invoked for Android");
        }

        // ----------------------------------------------------------- helpers

        static void Step(string name, Action a)
        {
            try { a(); }
            catch (Exception e)
            {
                var msg = e is TargetInvocationException && e.InnerException != null ? e.InnerException.Message : e.Message;
                Say("CI_BOOT_SKIP " + name + " :: " + msg.Replace("\n", " "));
            }
        }

        static void SetEnum(object target, string member, string valueName)
        {
            var t = target.GetType();
            var prop = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
            var field = prop == null ? t.GetField(member, BindingFlags.Public | BindingFlags.Instance) : null;
            Type memberType = prop != null ? prop.PropertyType : (field != null ? field.FieldType : null);
            if (memberType == null) { Say("CI_BOOT_NOTE no member " + member); return; }

            Type enumType = Nullable.GetUnderlyingType(memberType);
            if (enumType == null) enumType = memberType;
            if (!enumType.IsEnum) { Say("CI_BOOT_NOTE " + member + " is not an enum"); return; }

            string match = Enum.GetNames(enumType).FirstOrDefault(n => string.Equals(n, valueName, StringComparison.OrdinalIgnoreCase));
            if (match == null) { Say("CI_BOOT_NOTE " + member + " has no value " + valueName + " (has: " + string.Join(",", Enum.GetNames(enumType)) + ")"); return; }

            object val = Enum.Parse(enumType, match);
            if (prop != null) prop.SetValue(target, val, null); else field.SetValue(target, val);
        }

        static Type FindType(string name)
        {
            var t = Type.GetType(name);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(name);
                if (t != null) return t;
                t = asm.GetTypes().FirstOrDefault(x => x.FullName == name || x.Name == name);
                if (t != null) return t;
            }
            return null;
        }

        static string Arg(List<string> args, string key, string fallback)
        {
            int i = args.IndexOf(key);
            if (i >= 0 && i + 1 < args.Count) return args[i + 1];
            return fallback;
        }

        static void Say(string msg) { Debug.Log(msg); Console.WriteLine(msg); }
        static void Quit(int code) { Console.Out.Flush(); EditorApplication.Exit(code); }
    }
}
