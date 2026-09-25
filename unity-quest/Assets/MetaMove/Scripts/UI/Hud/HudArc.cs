using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MetaMove.UI.Hud
{
    // Bends a flat world-space HUD canvas onto an arc around the viewer: every direct
    // child whose name starts with columnPrefix is moved onto a cylinder of radius
    // radiusM (the viewing distance) and turned to face its centre, so the outer
    // readouts come towards you instead of fanning out as a wide flat wall. The single
    // stretched backplate is replaced by one backplate per column.
    //
    // Canvas convention (world-space uGUI): +Z points away from the viewer.
    // Runs before SafetyHud.Start, which sizes its speed bar from the canvas.
    [DefaultExecutionOrder(-10)]
    public class HudArc : MonoBehaviour
    {
        public string columnPrefix = "Col_";
        [Tooltip("Arc radius = distance from the eyes to the HUD, metres.")]
        [Range(0.3f, 2f)] public float radiusM = 0.65f;
        [Tooltip("Hide this full-width backplate (child name) and give each column its own.")]
        public string flatBackplate = "BG";
        public Color columnBackplate = new Color(0.04f, 0.07f, 0.12f, 0.82f);

        [Header("Strip shape (canvas units, 1 = 1 mm)")]
        [Tooltip("Reshape into a wide, flat strip with label and value side by side. Off = keep the authored layout.")]
        public bool strip = true;
        public float stripWidth = 720f;
        public float stripHeight = 80f;
        [Tooltip("Height share of the small label row on top.")]
        [Range(0.2f, 0.6f)] public float labelShare = 0.32f;

        void Start() => Apply();

        public void Apply()
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null) return;
            var root = (RectTransform)canvas.transform;
            if (strip) root.sizeDelta = new Vector2(stripWidth, stripHeight);
            float scale = Mathf.Max(1e-6f, root.lossyScale.x);
            float r = radiusM / scale;                          // radius in canvas units

            var flat = root.Find(flatBackplate);
            if (flat != null) flat.gameObject.SetActive(false);

            foreach (RectTransform col in root)
            {
                if (!col.name.StartsWith(columnPrefix)) continue;
                // Column centre along x in canvas units, taken from its anchors.
                float x = (Mathf.Lerp(col.anchorMin.x, col.anchorMax.x, 0.5f) - 0.5f) * root.rect.width;
                float theta = x / r;                            // arc length -> angle
                col.anchorMin = col.anchorMax = new Vector2(0.5f, col.anchorMin.y);
                col.anchorMin = new Vector2(0.5f, 0f);
                col.anchorMax = new Vector2(0.5f, 1f);
                float w = root.rect.width / 3f - 32f;
                col.sizeDelta = new Vector2(w, strip ? -16f : -32f);
                if (strip) SideBySide(col);
                col.anchoredPosition3D = new Vector3(r * Mathf.Sin(theta), 0f, -r * (1f - Mathf.Cos(theta)));
                col.localRotation = Quaternion.Euler(0f, theta * Mathf.Rad2Deg, 0f);

                if (col.Find("ArcBG") == null)
                {
                    var bg = new GameObject("ArcBG", typeof(RectTransform), typeof(Image));
                    var rt = (RectTransform)bg.transform;
                    rt.SetParent(col, false);
                    rt.SetAsFirstSibling();                     // behind label and value
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = new Vector2(-10f, -10f);
                    rt.offsetMax = new Vector2(10f, 10f);
                    bg.GetComponent<Image>().color = columnBackplate;
                }
            }
        }
    
        // Small label on top, value below - two rows in the flat strip. Side by side did not
        // fit long values ("CONNECTION" + "NOT CONNECTED" overlapped, seen in HudRender).
        void SideBySide(RectTransform col)
        {
            var label = col.Find("Label") as RectTransform;
            var value = col.Find("Value") as RectTransform;
            if (label != null)
            {
                label.anchorMin = new Vector2(0f, 1f - labelShare);
                label.anchorMax = new Vector2(1f, 1f);
                label.offsetMin = label.offsetMax = Vector2.zero;
                var t = label.GetComponent<TMP_Text>();
                if (t != null) { t.fontSize = 13f; t.alignment = TextAlignmentOptions.Center; }
            }
            if (value != null)
            {
                value.anchorMin = new Vector2(0f, 0f);
                value.anchorMax = new Vector2(1f, 1f - labelShare);
                value.offsetMin = value.offsetMax = Vector2.zero;
                var t = value.GetComponent<TMP_Text>();
                if (t != null)
                {
                    t.enableAutoSizing = true;
                    t.fontSizeMin = 12f;
                    t.fontSizeMax = 30f;
                    t.alignment = TextAlignmentOptions.Center;
                }
            }
        }
    }

}
