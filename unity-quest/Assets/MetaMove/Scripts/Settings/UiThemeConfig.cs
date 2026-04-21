using UnityEngine;

namespace MetaMove.Settings
{
    // Single source of truth for design tokens — spacing, colors, typography sizes.
    // Every panel / fixture script reads from this so changing the look once updates
    // the whole system.
    [CreateAssetMenu(menuName = "MetaMove/Settings/UI Theme", fileName = "UiThemeConfig")]
    public class UiThemeConfig : ScriptableObject
    {
        [Header("Spacing (world-mm)")]
        public float spaceXs = 4f;
        public float spaceSm = 8f;
        public float spaceMd = 16f;
        public float spaceLg = 24f;
        public float spaceXl = 40f;

        [Header("Typography (world-mm, TMP world-units)")]
        public float typeLabelSm = 6f;
        public float typeBody = 10f;
        public float typeHeading = 14f;
        public float typeTitle = 20f;
        public float typeDisplay = 32f;

        [Header("Colors")]
        public Color bg = new Color32(0x0E, 0x15, 0x20, 0xD9);
        public Color bgRaised = new Color32(0x1A, 0x23, 0x32, 0xFF);
        public Color border = new Color32(0x2B, 0x36, 0x48, 0xFF);
        public Color fg = Color.white;
        public Color fgMuted = new Color32(0x9A, 0xA5, 0xB8, 0xFF);
        public Color accent = new Color32(0x3D, 0xA5, 0xFF, 0xFF);
        public Color success = new Color32(0x4A, 0xDE, 0x80, 0xFF);
        public Color warning = new Color32(0xF5, 0xB9, 0x41, 0xFF);
        public Color destructive = new Color32(0xE2, 0x4A, 0x4A, 0xFF);
        public Color ghost = new Color(0.62f, 0.84f, 1f, 0.60f);

        [Header("Panel Sizing (world-mm)")]
        public Vector2 dashboardSize = new Vector2(600f, 400f);
        public Vector2 subPanelSize = new Vector2(400f, 300f);
        public Vector2 miniPanelSize = new Vector2(240f, 160f);
        public float buttonHeight = 40f;
        public float sliderHeight = 32f;
        public float topBarHeight = 48f;
        public float tabHeight = 40f;

        [Header("Radius / Elevation (world-mm)")]
        public float panelCornerRadius = 12f;
        public float buttonCornerRadius = 8f;
        public float panelElevationZ = 3f;

        [Header("Radial (world-mm)")]
        public float radialInnerRadius = 30f;
        public float radialOuterRadius = 80f;
        public int radialWedgeCount = 8;

        public Color StatusColor(bool ok) => ok ? success : destructive;
        public Color IkColor(float score01) =>
            score01 > 0.66f ? success : (score01 > 0.33f ? warning : destructive);
    }
}
