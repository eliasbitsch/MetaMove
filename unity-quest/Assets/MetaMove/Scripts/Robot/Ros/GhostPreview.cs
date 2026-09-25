using System.Linq;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

namespace MetaMove.Robot.Ros
{
    // The ghost robot of preview & confirm: a translucent copy of this rig that shows the
    // pose the real robot WOULD take (joints from preview_confirm on /metamove/preview_joints).
    // Visible only while a preview is pending or executing.
    //
    // Built in Awake - before any ROS message has posed the rig - so the copy starts in the
    // rest pose its JointAnglesSubscriber needs. The copy is made under an inactive holder,
    // so none of its components wake up before they are stripped: it keeps only meshes and
    // a JointAnglesSubscriber pointed at the preview topic.
    public class GhostPreview : MonoBehaviour
    {
        public string jointsTopic = "/metamove/preview_joints";
        public string stateTopic = "/metamove/preview_state";
        public Material ghostMaterial;

        GameObject _ghost;
        Renderer[] _renderers;
        bool _visible;
        volatile bool _wantVisible;

        void Awake()
        {
            var holder = new GameObject("GhostHolder");
            holder.SetActive(false);
            _ghost = Instantiate(gameObject, holder.transform);
            _ghost.name = name + " (ghost)";

            // Strip everything but the joint driver; drop grab handles and colliders.
            foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is JointAnglesSubscriber) continue;
                if (mb.GetType().Namespace != null && mb.GetType().Namespace.StartsWith("Oculus.Interaction"))
                    mb.gameObject.SetActive(false);   // grab handle: hide its whole object
                DestroyImmediate(mb);
            }
            foreach (var c in _ghost.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            var sub = _ghost.GetComponent<JointAnglesSubscriber>();
            if (sub != null) sub.topic = jointsTopic;

            _renderers = _ghost.GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers)
            {
                if (ghostMaterial != null) r.sharedMaterials = Enumerable.Repeat(ghostMaterial, r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.enabled = false;
            }

            _ghost.transform.SetParent(transform.parent, false);
            _ghost.transform.localPosition = transform.localPosition;
            _ghost.transform.localRotation = transform.localRotation;
            _ghost.transform.localScale = transform.localScale;
            if (Application.isPlaying) Destroy(holder); else DestroyImmediate(holder);
        }

        void OnEnable()
        {
            ROSConnection.GetOrCreateInstance().Subscribe<StringMsg>(stateTopic, m =>
            {
                var s = m.data ?? "";
                _wantVisible = s.Contains("\"preview\"") || s.Contains("\"executing\"") || s.Contains("\"unreachable\"");
            });
        }

        void Update()
        {
            // Follow the real robot's anchor (twin alignment moves it).
            if (_ghost.transform.parent != transform.parent) _ghost.transform.SetParent(transform.parent, false);
            _ghost.transform.localPosition = transform.localPosition;
            _ghost.transform.localRotation = transform.localRotation;
            if (_wantVisible == _visible) return;
            _visible = _wantVisible;
            foreach (var r in _renderers) if (r != null) r.enabled = _visible;
        }

        void OnDestroy()
        {
            if (_ghost != null) Destroy(_ghost);
        }
    }
}
