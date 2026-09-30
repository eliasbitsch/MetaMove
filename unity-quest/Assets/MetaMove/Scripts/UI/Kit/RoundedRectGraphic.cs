using UnityEngine;
using UnityEngine.UI;

namespace MetaMove.UI.Kit
{
    /// <summary>
    /// A uGUI panel with per-pixel rounded corners and an optional border.
    /// </summary>
    /// <remarks>
    /// The shader needs the element's size in pixels to place the corner arcs, and that is only
    /// known here — a material cannot read the RectTransform. The size is pushed on every layout
    /// change rather than every frame, so resizing stays correct without per-frame cost.
    /// </remarks>
    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    public class RoundedRectGraphic : MonoBehaviour
    {
        [SerializeField, Tooltip("Corner radius in pixels of the layout, not of the screen.")]
        private float _radius = 40f;

        [SerializeField, Tooltip("Border thickness in pixels. Zero draws no border.")]
        private float _borderWidth = 3f;

        [SerializeField] private Color _borderColor = new Color(0.85f, 0.80f, 1f, 0.55f);

        [SerializeField, Tooltip("Give the border a brushed-metal bezel look (lit gradient + fine streaks).")]
        private bool _metalRim;

        [SerializeField, Range(0f, 1f), Tooltip("How much of the border colour the metal bezel keeps. 0 = grey steel.")]
        private float _metalTint;

        [SerializeField, Tooltip("Shine strength of the metal rim: sheen, hot specular and the travelling glint.")]
        private float _metalGloss = 1.2f;

        [SerializeField, Tooltip("Soft halo spreading inward from the border, in pixels.")]
        private float _glowWidth;

        [SerializeField, Range(0f, 2f), Tooltip("Strength of the halo.")]
        private float _glowIntensity = 1f;

        [SerializeField, Range(0.3f, 3f),
         Tooltip("How gradual the halo hands off. Lower spreads it further inward.")]
        private float _glowFalloff = 1.5f;

        [Header("Goggle shape")]
        [SerializeField, Tooltip("Two blended lens circles instead of a rounded rectangle.")]
        private bool _goggleShape;

        [SerializeField, Range(0f, 1f), Tooltip("Distance between lens centres, as a fraction of half-width.")]
        private float _lensSeparation = 0.34f;

        [SerializeField, Range(0.1f, 1.2f), Tooltip("Lens radius, relative to the smaller half-extent.")]
        private float _lensRadius = 0.62f;

        [SerializeField, Range(0f, 1f), Tooltip("How softly the two lenses merge across the bridge.")]
        private float _lensBlend = 0.35f;

        private static readonly int RectSizeId = Shader.PropertyToID("_RectSize");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int BorderWidthId = Shader.PropertyToID("_BorderWidth");
        private static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
        private static readonly int MetalRimId = Shader.PropertyToID("_MetalRim");
        private static readonly int MetalGlossId = Shader.PropertyToID("_MetalGloss");
        private static readonly int MetalTintId = Shader.PropertyToID("_MetalTint");
        private static readonly int GlowWidthId = Shader.PropertyToID("_GlowWidth");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int GlowFalloffId = Shader.PropertyToID("_GlowFalloff");
        private static readonly int GoggleShapeId = Shader.PropertyToID("_GoggleShape");
        private static readonly int LensSeparationId = Shader.PropertyToID("_LensSeparation");
        private static readonly int LensRadiusId = Shader.PropertyToID("_LensRadius");
        private static readonly int LensBlendId = Shader.PropertyToID("_LensBlend");

        private Image _image;
        private RectTransform _rect;
        private Material _material;

        public float Radius
        {
            get => _radius;
            set { _radius = value; Apply(); }
        }

        /// <summary>Sets corner radius and border in one go, applying once.</summary>
        public void Configure(float radius, float borderWidth, Color borderColor)
        {
            _radius = radius;
            _borderWidth = borderWidth;
            _borderColor = borderColor;
            Apply();
        }

        /// <summary>Switches to the goggle outline and sets its proportions.</summary>
        public void SetGoggleShape(bool enabled, float separation, float radius, float blend)
        {
            _goggleShape = enabled;
            _lensSeparation = separation;
            _lensRadius = radius;
            _lensBlend = blend;
            Apply();
        }

        /// <summary>Turns the brushed-metal bezel look on or off for the border.</summary>
        public void SetMetalRim(bool on) { _metalRim = on; Apply(); }

        /// <summary>
        /// How much of the border colour the brushed bezel keeps: 0 is grey steel, 1 is coloured metal.
        /// </summary>
        /// <remarks>
        /// Zero by default, so every panel that had a metal rim before this existed looks exactly as it
        /// did. Turn it up where the bezel is carrying information — an action's colour, say — rather
        /// than just being a bezel.
        /// </remarks>
        public void SetMetalTint(float keep)
        {
            _metalTint = Mathf.Clamp01(keep);
            if (_material != null) _material.SetFloat(MetalTintId, _metalTint);
        }

        /// <summary>Sets the metal-rim shine strength (cheap: just the one float, no full re-apply).</summary>
        public void SetMetalGloss(float gloss)
        {
            _metalGloss = gloss;
            if (_material != null) _material.SetFloat(MetalGlossId, gloss);
        }

        /// <summary>Sets the halo spreading inward from the border.</summary>
        public void SetGlow(float width, float intensity, float falloff)
        {
            _glowWidth = width;
            _glowIntensity = intensity;
            _glowFalloff = falloff;
            Apply();
        }

        /// <summary>Size the shader is currently using, for diagnostics.</summary>
        public Vector2 CurrentRectSize => _rect != null ? _rect.rect.size : Vector2.zero;

        /// <summary>Whether the material resolved. False means the shader was missing.</summary>
        public bool HasMaterial => _material != null;

        /// <summary>
        /// Values as the shader actually sees them, read back from the material.
        /// </summary>
        /// <remarks>
        /// Reported from the material rather than from the serialized fields, because the question
        /// is whether the assignments arrive — and the fields would answer the wrong question by
        /// echoing what was set rather than what is in effect.
        /// </remarks>
        public string DescribeMaterialState()
        {
            if (_material == null) return "material=null";

            var renderMaterial = _image != null ? _image.materialForRendering : null;
            return $"border={_material.GetFloat(BorderWidthId):F1} " +
                   $"borderCol={_material.GetColor(BorderColorId)} " +
                   $"radius={_material.GetFloat(RadiusId):F0} " +
                   $"rectSize={_material.GetVector(RectSizeId)} " +
                   $"glow={_material.GetFloat(GlowWidthId):F0}/{_material.GetFloat(GlowIntensityId):F2} " +
                   $"goggle={_material.GetFloat(GoggleShapeId):F0} " +
                   $"lens={_material.GetFloat(LensSeparationId):F2}/{_material.GetFloat(LensRadiusId):F2}/{_material.GetFloat(LensBlendId):F2} " +
                   $"shader={_material.shader.name} " +
                   $"renderShader={(renderMaterial != null ? renderMaterial.shader.name : "none")}";
        }

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rect = GetComponent<RectTransform>();
            EnsureMaterial();
        }

        private void OnEnable() => Apply();

        private void OnRectTransformDimensionsChange() => Apply();

#if UNITY_EDITOR
        private void OnValidate() => Apply();
#endif

        private void EnsureMaterial()
        {
            if (_material != null) return;

            var shader = Shader.Find("MetaMove/UI Rounded Rect");
            if (shader == null)
            {
                Debug.LogError("[MetaMove] Shader 'MetaMove/UI Rounded Rect' not found (Resources/MetaMoveUIKit.mat keeps it in the build).");
                return;
            }

            // An instance rather than the shared material: every panel has its own size, and a
            // shared material would leave them all drawing the last one's corners.
            _material = new Material(shader) { name = "UIRoundedRect (instance)" };
            if (_image != null) _image.material = _material;
        }

        private void Apply()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_rect == null) _rect = GetComponent<RectTransform>();
            EnsureMaterial();
            if (_material == null) return;

            var size = _rect.rect.size;
            _material.SetVector(RectSizeId, new Vector4(size.x, size.y, 0f, 0f));
            _material.SetFloat(RadiusId, _radius);
            _material.SetFloat(BorderWidthId, _borderWidth);
            _material.SetColor(BorderColorId, _borderColor);
            _material.SetFloat(MetalRimId, _metalRim ? 1f : 0f);
            _material.SetFloat(MetalGlossId, _metalGloss);
            _material.SetFloat(MetalTintId, _metalTint);
            _material.SetFloat(GlowWidthId, _glowWidth);
            _material.SetFloat(GlowIntensityId, _glowIntensity);
            _material.SetFloat(GlowFalloffId, _glowFalloff);
            _material.SetFloat(GoggleShapeId, _goggleShape ? 1f : 0f);
            _material.SetFloat(LensSeparationId, _lensSeparation);
            _material.SetFloat(LensRadiusId, _lensRadius);
            _material.SetFloat(LensBlendId, _lensBlend);
        }
    }
}
