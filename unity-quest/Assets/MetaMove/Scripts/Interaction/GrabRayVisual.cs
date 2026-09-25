using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

namespace MetaMove.Interaction
{
    // Keeps a ray from the hand to a distance-grabbed object WHILE it is held.
    // Meta's DistantInteractionLineVisual hides its line on Select by design (it expects the
    // object to fly into the hand); with MoveFromTargetProvider the object stays at a distance
    // and is steered from there, so the link between hand and object should stay visible.
    [RequireComponent(typeof(LineRenderer))]
    public class GrabRayVisual : MonoBehaviour
    {
        public DistanceHandGrabInteractable interactable;
        [Range(8, 48)] public int segments = 24;
        [Tooltip("How far the ray leaves the hand straight before it bends (share of the length).")]
        [Range(0f, 0.8f)] public float straightShare = 0.35f;
        public float width = 0.004f;

        LineRenderer _line;
        DistanceHandGrabInteractor[] _interactors = new DistanceHandGrabInteractor[0];
        float _nextRefresh;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = width;
            _line.positionCount = segments;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.enabled = false;
            if (interactable == null) interactable = GetComponentInParent<DistanceHandGrabInteractable>();
        }

        void LateUpdate()
        {
            if (Time.unscaledTime >= _nextRefresh)
            {
                _interactors = FindObjectsByType<DistanceHandGrabInteractor>(FindObjectsSortMode.None);
                _nextRefresh = Time.unscaledTime + 2f;
            }
            DistanceHandGrabInteractor holder = null;
            foreach (var i in _interactors)
                if (i != null && i.State == InteractorState.Select && i.SelectedInteractable == interactable) { holder = i; break; }

            if (holder == null) { _line.enabled = false; return; }

            Pose origin = holder.Origin;
            Vector3 end = interactable.transform.position;
            float len = Vector3.Distance(origin.position, end);
            Vector3 ctrl = origin.position + origin.forward * (len * straightShare);   // leave the hand straight, then bend
            for (int k = 0; k < segments; k++)
            {
                float t = k / (segments - 1f);
                Vector3 a = Vector3.Lerp(origin.position, ctrl, t);
                Vector3 b = Vector3.Lerp(ctrl, end, t);
                _line.SetPosition(k, Vector3.Lerp(a, b, t));
            }
            _line.enabled = true;
        }
    }
}
