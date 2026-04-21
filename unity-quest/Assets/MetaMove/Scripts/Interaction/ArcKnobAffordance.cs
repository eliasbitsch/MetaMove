using UnityEngine;
using Oculus.Interaction;

namespace MetaMove.Interaction
{
    /// <summary>
    /// Small always-visible knob at the arc's midpoint that morphs from a sphere
    /// into a pill (stretched along the arc tangent) while the handle is grabbed.
    /// </summary>
    public class ArcKnobAffordance : MonoBehaviour
    {
        [Tooltip("Primary interactable to read state from (selected = grabbed).")]
        public MonoBehaviour interactableSource;

        [Tooltip("Additional interactables — any in Select state triggers the grabbed morph.")]
        public MonoBehaviour[] additionalSources;

        [Tooltip("If true, pick up idle size from the current transform scale at Awake. If false, use idleScale below.")]
        public bool captureIdleFromTransform = true;

        public Vector3 idleScale = Vector3.one;

        [Tooltip("Multiplier applied to idleScale on each axis when grabbed. (1,2.8,1) = pill stretched along Y.")]
        public Vector3 grabbedMultiplier = new Vector3(1f, 2.8f, 1f);

        public float lerpSpeed = 12f;

        IInteractableView[] _views;
        Vector3 _idle;
        Vector3 _current;

        void Awake()
        {
            var list = new System.Collections.Generic.List<IInteractableView>(4);
            if (interactableSource is IInteractableView v0) list.Add(v0);
            if (additionalSources != null)
                foreach (var s in additionalSources)
                    if (s is IInteractableView v) list.Add(v);
            _views = list.ToArray();

            _idle = captureIdleFromTransform ? transform.localScale : idleScale;
            _current = _idle;
        }

        void Update()
        {
            bool grabbed = false;
            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i] != null && _views[i].State == InteractableState.Select)
                { grabbed = true; break; }
            }
            var target = grabbed
                ? new Vector3(_idle.x * grabbedMultiplier.x, _idle.y * grabbedMultiplier.y, _idle.z * grabbedMultiplier.z)
                : _idle;
            _current = Vector3.Lerp(_current, target, Time.deltaTime * lerpSpeed);
            transform.localScale = _current;
        }
    }
}
