#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using MetaMove.Robot;
using MetaMove.Settings;
using MetaMove.UI.Hud;

namespace MetaMove.EditorTools
{
    // One-click: builds the lazy-follow curved Status HUD under the active camera rig,
    // wires RobotTelemetry + UiThemeConfig, and adds a PassthroughEnabler if missing.
    //
    // Menu: MetaMove > Create Status HUD (Bottom Curved)
    public static class StatusHudAuthor
    {
        const string ThemePath = "Assets/MetaMove/Settings/UiThemeConfig.asset";

        [MenuItem("MetaMove/Create Status HUD (Bottom Curved)")]
        public static void Create()
        {
            var head = FindHeadAnchor();
            if (head == null)
            {
                EditorUtility.DisplayDialog("Status HUD",
                    "No camera anchor found. Add OVRCameraRig (or any Camera tagged MainCamera) first.\n\n" +
                    "Tip: Meta > Tools > Building Blocks > Camera Rig.",
                    "OK");
                return;
            }

            var theme = AssetDatabase.LoadAssetAtPath<UiThemeConfig>(ThemePath);
            var telemetry = Object.FindFirstObjectByType<RobotTelemetry>();

            // HUD root is a *sibling* of the head anchor (under the rig), not a child —
            // we don't want it to inherit head pitch/roll; the StatusHud script handles follow.
            var rigRoot = head.parent != null ? head.parent : head;
            var root = new GameObject("StatusHud");
            root.transform.SetParent(rigRoot, false);

            var hud = root.AddComponent<StatusHud>();
            hud.headAnchor = head;
            hud.theme = theme;
            hud.telemetry = telemetry;

            // Build module visuals — backplate quad + label + value TMP per module.
            hud.modules.Clear();
            string[] ids   = { "battery",  "link",  "motors", "passthrough" };
            string[] names = { "BATTERY",  "LINK",  "ROBOT",  "PASSTHROUGH"  };
            for (int i = 0; i < ids.Length; i++)
            {
                hud.modules.Add(BuildModule(root.transform, ids[i], names[i], theme, hud.moduleWidthMm, hud.moduleHeightMm));
            }

            // Ensure passthrough is on.
            if (Object.FindFirstObjectByType<PassthroughEnabler>() == null)
            {
                var pt = new GameObject("PassthroughEnabler");
                pt.transform.SetParent(rigRoot, false);
                pt.AddComponent<PassthroughEnabler>();
            }

            Selection.activeObject = root;
            EditorSceneManager.MarkAllScenesDirty();
            EditorUtility.DisplayDialog("Status HUD",
                "Created StatusHud under " + rigRoot.name + ".\n\n" +
                (theme == null ? "⚠ UiThemeConfig not found at " + ThemePath + " — assign manually for theme colors.\n" : "") +
                (telemetry == null ? "⚠ No RobotTelemetry in scene — link/motors modules will read OFF.\n" : "") +
                "PassthroughEnabler ensures AR mode on Quest 3.",
                "OK");
        }

        static Transform FindHeadAnchor()
        {
            // Prefer Meta's CenterEyeAnchor if the OVRCameraRig is present, else fall back to Camera.main.
            var byName = GameObject.Find("CenterEyeAnchor");
            if (byName != null) return byName.transform;
            return Camera.main != null ? Camera.main.transform : null;
        }

        static StatusHud.HudModule BuildModule(
            Transform parent, string id, string label, UiThemeConfig theme, float wMm, float hMm)
        {
            var go = new GameObject($"Module_{id}");
            go.transform.SetParent(parent, false);

            // World-space canvas — RectTransform in mm with scale 0.001 → 1mm = 1 world-mm.
            var canvasGo = new GameObject("Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(go.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRt = (RectTransform)canvasGo.transform;
            canvasRt.sizeDelta = new Vector2(wMm, hMm);
            canvasRt.localScale = Vector3.one * 0.001f;

            // Backplate
            var bg = AddRect(canvasRt, "BG", new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = theme != null ? theme.bg : new Color(0.05f, 0.08f, 0.13f, 0.85f);

            // Status pill (small colored block on the left — tinted by status)
            var pill = AddRect(canvasRt, "Pill", new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(8, 8), new Vector2(16, -8));
            var pillImg = pill.gameObject.AddComponent<Image>();
            pillImg.color = theme != null ? theme.fgMuted : Color.gray;

            // Label (static, top)
            var labelRt = AddRect(canvasRt, "Label", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(24, -8), new Vector2(-8, -24));
            var labelTmp = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = theme != null ? theme.typeLabelSm * 2f : 12f;
            labelTmp.color = theme != null ? theme.fgMuted : new Color(0.6f, 0.65f, 0.72f);
            labelTmp.alignment = TextAlignmentOptions.TopLeft;

            // Value (large, bottom)
            var valueRt = AddRect(canvasRt, "Value", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(24, 4), new Vector2(-8, -28));
            var valueTmp = valueRt.gameObject.AddComponent<TextMeshProUGUI>();
            valueTmp.text = "--";
            valueTmp.fontSize = theme != null ? theme.typeHeading * 2f : 28f;
            valueTmp.color = Color.white;
            valueTmp.fontStyle = FontStyles.Bold;
            valueTmp.alignment = TextAlignmentOptions.BottomLeft;

            return new StatusHud.HudModule
            {
                id = id,
                label = label,
                root = go.transform,
                labelText = labelTmp,
                valueText = valueTmp,
                iconBg = pillImg,
            };
        }

        static RectTransform AddRect(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }
    }
}
#endif
