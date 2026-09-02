// Headless entry points for agent-driven Unity workflows.
// Invoked via: Unity.exe -batchmode -nographics -quit -projectPath <p> -executeMethod AgentCI.Ci.<Method>
//
// Every method prints one machine-parsable marker line and sets an explicit exit code,
// so a calling agent can decide what happened without reading the whole Editor log.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AgentCI
{
    public static class Ci
    {
        // ---------------------------------------------------------------- compile

        // Reaching this method at all proves every assembly compiled: Unity aborts
        // -executeMethod before invocation when a script fails to compile.
        public static void Compile()
        {
            Say("CI_COMPILE_OK unity=" + Application.unityVersion + " target=" + EditorUserBuildSettings.activeBuildTarget);
            Quit(0);
        }

        // ------------------------------------------------------------------ build

        // Args: -ciTarget Android|Win64  -ciOut <path>  -ciBundle <id>  -ciDevelopment
        public static void Build()
        {
            try
            {
                var args = Environment.GetCommandLineArgs().ToList();
                string targetName = Arg(args, "-ciTarget", "Android");
                bool development = args.Contains("-ciDevelopment");

                BuildTarget target;
                BuildTargetGroup group;
                if (targetName.Equals("Win64", StringComparison.OrdinalIgnoreCase))
                {
                    target = BuildTarget.StandaloneWindows64;
                    group = BuildTargetGroup.Standalone;
                }
                else
                {
                    target = BuildTarget.Android;
                    group = BuildTargetGroup.Android;
                }

                string defaultName = Sanitize(PlayerSettings.productName);
                string defaultOut = target == BuildTarget.Android
                    ? "Builds/" + defaultName + ".apk"
                    : "Builds/" + defaultName + "/" + defaultName + ".exe";
                string outPath = Arg(args, "-ciOut", defaultOut);

                string[] scenes = Scenes();
                if (scenes.Length == 0)
                {
                    Fail("CI_BUILD_FAILED reason=no_scenes (nothing enabled in Build Settings, no .unity under Assets/Scenes)");
                    return;
                }

                if (EditorUserBuildSettings.activeBuildTarget != target)
                {
                    Say("CI_INFO switching build target to " + target + " (the first switch can take several minutes)");
                    EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
                }

                if (target == BuildTarget.Android)
                {
                    string bundle = Arg(args, "-ciBundle", null);
                    if (!string.IsNullOrEmpty(bundle))
                        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, bundle);

                    // Quest is ARM64-only and requires IL2CPP.
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

                    // The Hub-bundled SDK ships a limited set of platforms; pinning to an
                    // installed one avoids Gradle trying to download and license another.
                    PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
                    PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
                }

                string dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var opts = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outPath,
                    target = target,
                    targetGroup = group,
                    options = development ? (BuildOptions.Development | BuildOptions.AllowDebugging) : BuildOptions.None,
                };

                Say("CI_INFO building scenes=" + scenes.Length + " out=" + outPath + " dev=" + development);
                BuildReport report = BuildPipeline.BuildPlayer(opts);
                BuildSummary s = report.summary;

                if (s.result == BuildResult.Succeeded)
                {
                    Say("CI_BUILD_OK path=" + s.outputPath + " bytes=" + s.totalSize + " seconds=" + (int)s.totalTime.TotalSeconds);
                    Quit(0);
                }
                else
                {
                    foreach (var step in report.steps)
                        foreach (var msg in step.messages)
                            if (msg.type == LogType.Error || msg.type == LogType.Exception)
                                Say("CI_BUILD_ERR " + msg.content.Replace("\n", " "));
                    Fail("CI_BUILD_FAILED result=" + s.result + " errors=" + s.totalErrors);
                }
            }
            catch (Exception e)
            {
                Fail("CI_BUILD_FAILED exception=" + e.Message);
            }
        }

        // ------------------------------------------------------------------- info

        // Dumps what an agent would otherwise have to guess: scenes, bundle id, target.
        public static void Info()
        {
            try
            {
                Say("CI_INFO_UNITY " + Application.unityVersion);
                Say("CI_INFO_PRODUCT " + PlayerSettings.productName);
                Say("CI_INFO_BUNDLE " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
                Say("CI_INFO_TARGET " + EditorUserBuildSettings.activeBuildTarget);
                foreach (var s in Scenes()) Say("CI_INFO_SCENE " + s);
                Quit(0);
            }
            catch (Exception e)
            {
                Fail("CI_INFO_FAILED " + e.Message);
            }
        }

        // ---------------------------------------------------------------- helpers

        static string[] Scenes()
        {
            var fromSettings = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();
            if (fromSettings.Length > 0) return fromSettings;

            // Fall back to every scene in the conventional folder, sorted, so a freshly
            // scaffolded project builds before anyone has touched Build Settings.
            if (!Directory.Exists("Assets/Scenes")) return new string[0];
            return Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p)
                .ToArray();
        }

        static string Arg(List<string> args, string key, string fallback)
        {
            int i = args.IndexOf(key);
            if (i >= 0 && i + 1 < args.Count) return args[i + 1];
            return fallback;
        }

        static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        static void Say(string msg) { Debug.Log(msg); Console.WriteLine(msg); }
        static void Fail(string msg) { Debug.LogError(msg); Console.WriteLine(msg); Quit(1); }
        static void Quit(int code) { Console.Out.Flush(); EditorApplication.Exit(code); }
    }
}
