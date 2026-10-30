#if UNITY_EDITOR
using Oculus.Interaction;
using UnityEngine;

namespace MetaMove.UI
{
    // Unity Editor only (Meta XR Simulator). A mouse click in the simulator is always a
    // pinch, and the simulated hands follow the head, so a fingertip can hardly reach the
    // lazy-follow button panel - the poke buttons were not pressable. In Play mode every
    // poke button gets a ray on the same surface as well: aim the hand ray, pinch (click)
    // = the button's WhenSelect, exactly what a poke fires. Never in a device build.
    public static class EditorRayButtons
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            int n = 0;
            foreach (var w in Object.FindObjectsByType<PointableUnityEventWrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var poke = w.GetComponent<PokeInteractable>();
                if (poke == null || poke.SurfacePatch == null || w.GetComponent<RayInteractable>() != null) continue;
                var ray = w.gameObject.AddComponent<RayInteractable>();
                ray.InjectSurface(poke.SurfacePatch);
                w.gameObject.AddComponent<RaySelectForwarder>().Bind(ray, w);
                n++;
            }
            Debug.Log($"[EditorRayButtons] ray + pinch enabled on {n} poke buttons (Editor only)");

            // The button panel lazy-follows the head, and so do the simulated hands: aiming
            // turned the panel away. In the Editor it settles in front once, then stays put.
            foreach (var follow in Object.FindObjectsByType<LazyFollow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (follow.GetComponentInChildren<PointableUnityEventWrapper>(true) != null)
                    follow.gameObject.AddComponent<EditorPanelFreeze>().follow = follow;
        }
    }

    public class EditorPanelFreeze : MonoBehaviour
    {
        public LazyFollow follow;
        Vector3 _offset;          // authored offset from the head (panel is a child of the CenterEyeAnchor)
        Transform _head;

        void Awake()
        {
            // Read the authored layout before LazyFollow detaches the panel, then take over:
            // in the Simulator the head is not tracked yet at Start, so LazyFollow recorded a
            // wrong offset and the panel ended up on the floor.
            _head = transform.parent;
            _offset = transform.localPosition;
            if (follow != null) follow.enabled = false;
        }

        System.Collections.IEnumerator Start()
        {
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            float t0 = Time.time;
            while (_head != null && _head.position.y < 1f && Time.time - t0 < 20f) yield return null;
            yield return new WaitForSeconds(0.5f);
            if (_head == null) yield break;
            // Same as LazyFollow: offset in the head's yaw frame, panel level, +Z away from the viewer.
            var yaw = Quaternion.Euler(0f, _head.eulerAngles.y, 0f);
            transform.SetParent(null, true);
            transform.position = _head.position + yaw * _offset;
            var away = transform.position - _head.position;
            away.y = 0f;
            if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            Debug.Log($"[EditorRayButtons] {name} placed in front and stays (Editor only) - aim the hand ray, pinch to press");
        }
    }

    public class RaySelectForwarder : MonoBehaviour
    {
        RayInteractable _ray;
        PointableUnityEventWrapper _wrapper;

        public void Bind(RayInteractable ray, PointableUnityEventWrapper wrapper)
        {
            _ray = ray;
            _wrapper = wrapper;
            _ray.WhenPointerEventRaised += OnPointer;
        }

        void OnPointer(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select) _wrapper.WhenSelect.Invoke(evt);
        }

        void OnDestroy()
        {
            if (_ray != null) _ray.WhenPointerEventRaised -= OnPointer;
        }
    }
}
#endif
