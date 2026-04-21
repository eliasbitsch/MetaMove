using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

namespace MetaMove.Interaction
{
    /// <summary>
    /// Fades a Renderer's _BaseColor alpha based on the distance to the nearest
    /// tracked hand, so the arc handle appears when the user reaches toward it
    /// (mirrors Meta's proximity-based affordance reveal).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class ArcProximityFade : MonoBehaviour
    {
        [Tooltip("Fully visible when nearest probe is closer than this (meters).")]
        public float nearDistance = 0.35f;

        [Tooltip("Fully hidden when nearest probe is farther than this (meters).")]
        public float farDistance = 1.2f;

        [Tooltip("Include the main camera (head) as a proximity probe — reliable fallback if hands aren't tracked.")]
        public bool includeCamera = true;

        [Tooltip("Log probe discovery and current distance each second (debug).")]
        public bool debugLog = false;

        [Tooltip("Higher = snappier fade.")]
        public float fadeSpeed = 6f;

        [Tooltip("Idle alpha when no hand is near (0 = hidden, >0 = always faintly visible).")]
        [Range(0f, 1f)]
        public float idleAlpha = 0f;

        Renderer _rend;
        MaterialPropertyBlock _mpb;
        Transform[] _probes;
        IInteractableView[] _views;
        float _alpha;
        float _probeRefreshTimer;

        [Tooltip("Fade curve sharpness at arc ends (passed to MetaMove/ArcFadeWhite shader). Higher = crisper cut-off.")]
        public float fadeSharpness = 2.0f;

        [Tooltip("Interactables to watch — when ANY is Hovered or Selected (e.g. distance ray aimed at it), force full opacity.")]
        public MonoBehaviour[] interactableSources;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int FadeSharpnessId = Shader.PropertyToID("_FadeSharpness");

        void Awake()
        {
            _rend = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            _alpha = idleAlpha;

            // Persist _FadeSharpness on the material itself so the shader always
            // has a valid value (MPB alone can race with SRP batcher on URP).
            if (_rend.sharedMaterial != null &&
                _rend.sharedMaterial.HasProperty(FadeSharpnessId))
            {
                _rend.sharedMaterial.SetFloat(FadeSharpnessId, fadeSharpness);
            }

            var list = new System.Collections.Generic.List<IInteractableView>(4);
            if (interactableSources != null)
                foreach (var s in interactableSources)
                    if (s is IInteractableView v) list.Add(v);
            _views = list.ToArray();
        }

        void Update()
        {
            _probeRefreshTimer -= Time.deltaTime;
            if (_probes == null || _probes.Length == 0 || _probeRefreshTimer <= 0f)
            {
                RefreshProbes();
                _probeRefreshTimer = 1f;
            }

            float nearest = float.PositiveInfinity;
            Vector3 me = transform.position;
            if (_probes != null)
            {
                for (int i = 0; i < _probes.Length; i++)
                {
                    var t = _probes[i];
                    if (t == null) continue;
                    // Skip probes that sit exactly at origin (= not tracked yet).
                    if (t.position.sqrMagnitude < 0.0001f) continue;
                    float d = Vector3.Distance(t.position, me);
                    if (d < nearest) nearest = d;
                }
            }

            if (debugLog && _probeRefreshTimer > 0.9f)
            {
                Debug.Log($"[ArcProximityFade] probes={(_probes?.Length ?? 0)} nearest={nearest:F3}m alpha={_alpha:F2}");
            }

            float target = idleAlpha;
            if (!float.IsPositiveInfinity(nearest))
            {
                float proximity = Mathf.InverseLerp(farDistance, nearDistance, nearest);
                target = Mathf.Max(idleAlpha, proximity);
            }

            // If any watched interactable is being hovered or grabbed (e.g. distance
            // ray aimed at this arc), force full opacity regardless of physical proximity.
            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i] == null) continue;
                var st = _views[i].State;
                if (st == InteractableState.Hover || st == InteractableState.Select)
                { target = 1f; break; }
            }

            _alpha = Mathf.MoveTowards(_alpha, target, Time.deltaTime * fadeSpeed);

            _rend.GetPropertyBlock(_mpb);
            var c = new Color(1f, 1f, 1f, _alpha);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId, c);
            // Must also set _FadeSharpness here: touching _BaseColor via MaterialPropertyBlock
            // breaks the SRP Batcher binding for this CBUFFER, so all other per-material
            // uniforms (including _FadeSharpness) would read as 0 → no UV fade at ends.
            _mpb.SetFloat(FadeSharpnessId, fadeSharpness);
            _rend.SetPropertyBlock(_mpb);
        }

        void RefreshProbes()
        {
            var list = new System.Collections.Generic.List<Transform>(4);
            var hands = Object.FindObjectsByType<Hand>(FindObjectsSortMode.None);
            if (hands != null)
            {
                foreach (var h in hands)
                {
                    if (h != null) list.Add(h.transform);
                }
            }
            if (includeCamera && Camera.main != null) list.Add(Camera.main.transform);
            _probes = list.ToArray();
            if (debugLog) Debug.Log($"[ArcProximityFade] RefreshProbes → {_probes.Length} probe(s) (hands + camera)");
        }
    }
}
