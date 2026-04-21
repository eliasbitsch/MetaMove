#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MetaMove.EditorTools
{
    // Auto-creates Assets/MetaMove/Resources/SampleThumbnails.asset with references
    // to Meta's Sample thumbnails (by GUID). Forces Unity to include them in the
    // build so the tile menu actually shows images. Runs automatically before each
    // Android build.
    public class SampleThumbnailsAuthor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        const string AssetPath = "Assets/MetaMove/Resources/SampleThumbnails.asset";

        static readonly (string sceneName, string guid)[] Map =
        {
            ("ComprehensiveRigExample",           "56caf8c6e89440e46935713b552cd3cd"),
            ("ConcurrentHandsControllersExamples","fa047035784b7564aa31af557d724fb4"),
            ("UISetExamples",                     "4e236ccd95d5ce541ad024c504327587"),
            ("PokeExamples",                      "b14c65405a4651e45a7d4d6718558ce0"),
            ("RayExamples",                       "25f8c026e84ba6e4186ef5c2868f05be"),
            ("DistanceGrabExamples",              "e3264794d9076b544acc21c001eb349d"),
            ("HandGrabExamples",                  "f18a401b75c8c4c46ba88c481b9f19f9"),
            ("TouchGrabExamples",                 "11f27b21546fe9244a5bbc7fc9f48837"),
            ("HandGrabUseExamples",               "698aa40b47e1876469e0ecb2cfa4e7b4"),
            ("SnapExamples",                      "5a93026f5a66d914a93d97691bc26161"),
            ("TransformerExamples",               "7c63d563d806e7d4c8d67d4e01643be0"),
            ("PanelWithManipulators",             "9cb4d3e251adc8143bdf2a9427f8bc5b"),
            ("GestureExamples",                   "b412fb1201633044088b76928d997acb"),
            ("PoseExamples",                      "4a728abfb8951a44fbe9b376213a4f26"),
            ("LocomotionExamples",                "da11b7adabd2ca84ba3cfe51fee4d7a1"),
            ("BodyPoseDetectionExamples",         "7b243791ea5e1394cb054c3cc372b3aa"),
            ("HandOnHandInteraction",             "22bd2d8cb611d194199ba808d815b4b8"),
        };

        public void OnPreprocessBuild(BuildReport report) => Create();

        static bool IsWebp(byte[] data)
        {
            if (data == null || data.Length < 12) return false;
            // "RIFF" + 4 bytes size + "WEBP"
            return data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
                && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50;
        }

        [MenuItem("MetaMove/Author Sample Thumbnails Asset")]
        public static void Create()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath)!);
            var so = AssetDatabase.LoadAssetAtPath<MetaMove.Sandbox.SampleThumbnails>(AssetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<MetaMove.Sandbox.SampleThumbnails>();
                AssetDatabase.CreateAsset(so, AssetPath);
            }

            // Copy all thumbnail PNGs from the package cache into our own Resources
            // folder so Unity indexes them reliably (package-cache sprites sometimes
            // fail to load as Sprite sub-asset in Unity 6).
            const string CopyDir = "Assets/MetaMove/Resources/Thumbs";
            Directory.CreateDirectory(CopyDir);

            var entries = new MetaMove.Sandbox.SampleThumbnails.Entry[Map.Length];
            for (int i = 0; i < Map.Length; i++)
            {
                var srcPath = AssetDatabase.GUIDToAssetPath(Map[i].guid);
                string copyPath = null;
                if (!string.IsNullOrEmpty(srcPath) && File.Exists(srcPath))
                {
                    copyPath = $"{CopyDir}/{Map[i].sceneName}.png";
                    // Meta ships some images as WebP with a .png extension. Detect by
                    // magic bytes and re-encode to a real PNG; Unity can't import WebP.
                    var srcBytes = File.ReadAllBytes(srcPath);
                    if (IsWebp(srcBytes))
                    {
                        // Meta ships WebP with .png extension. Unity can't decode WebP,
                        // so use the bundled dwebp.exe to convert to real PNG.
                        var dwebp = Path.GetFullPath("Tools/dwebp.exe");
                        if (!File.Exists(dwebp))
                        {
                            copyPath = null;
                            Debug.LogError($"[SampleThumbnailsAuthor] Missing {dwebp}");
                        }
                        else
                        {
                            var srcAbs = Path.GetFullPath(srcPath);
                            var dstAbs = Path.GetFullPath(copyPath);
                            var psi = new System.Diagnostics.ProcessStartInfo(dwebp, $"\"{srcAbs}\" -o \"{dstAbs}\"")
                            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                            using (var p = System.Diagnostics.Process.Start(psi))
                            {
                                string err = p.StandardError.ReadToEnd();
                                p.WaitForExit(15000);
                                if (p.ExitCode != 0 || !File.Exists(dstAbs))
                                {
                                    copyPath = null;
                                    Debug.LogError($"[SampleThumbnailsAuthor] dwebp failed for '{srcPath}': {err}");
                                }
                            }
                        }
                    }
                    else
                    {
                        File.Copy(srcPath, copyPath, true);
                    }

                    if (copyPath != null)
                    {
                        AssetDatabase.ImportAsset(copyPath, ImportAssetOptions.ForceSynchronousImport);
                        var imp = AssetImporter.GetAtPath(copyPath) as TextureImporter;
                        if (imp != null && imp.textureType != TextureImporterType.Sprite)
                        {
                            imp.textureType = TextureImporterType.Sprite;
                            imp.spriteImportMode = SpriteImportMode.Single;
                            imp.SaveAndReimport();
                        }
                    }
                }

                Sprite sprite = null;
                if (!string.IsNullOrEmpty(copyPath))
                {
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(copyPath);
                    if (sprite == null)
                    {
                        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(copyPath))
                            if (a is Sprite s) { sprite = s; break; }
                    }
                }

                entries[i] = new MetaMove.Sandbox.SampleThumbnails.Entry
                {
                    sceneName = Map[i].sceneName,
                    sprite = sprite
                };
                Debug.Log($"[SampleThumbnailsAuthor] {Map[i].sceneName} -> copy='{copyPath}' sprite={(sprite != null ? sprite.name : "NULL")}");
            }
            so.entries = entries;
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SampleThumbnailsAuthor] Authored {entries.Length} thumbnails into {AssetPath}");
        }
    }
}
#endif
