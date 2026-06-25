using UnityEngine;
using UnityEngine.InputSystem;
using MetaMove.Robot;

namespace MetaMove.Safety
{
    // Switches the fully-digital demo between two modes:
    //   * MANUAL : the user grabs the IK target (distance-grab) and the robot
    //              follows. The PickPlaceLoop is OFF.
    //   * AUTO   : the PickPlaceLoop drives the target through pick & place with
    //              distance-based speed scaling. The grab interaction is OFF so
    //              it doesn't fight the loop over the target.
    //
    // Toggle via: keyboard (editor), a VR controller button (device), or
    // ToggleMode()/SetAuto() from a UI button.
    public class DemoModeSwitch : MonoBehaviour
    {
        [Header("Targets")]
        public PickPlaceLoop loop;
        [Tooltip("Grab / interaction components disabled in AUTO so they don't fight the loop.")]
        public Behaviour[] grabInteractors;
        [Tooltip("Visuals shown only in MANUAL (e.g. the grab handle ball) — hidden in AUTO.")]
        public Renderer[] manualOnlyRenderers;

        [Header("State")]
        public bool autoMode = true;

        [Header("Input")]
        public Key editorKey = Key.Space;
        public bool useVrButton = true;
        public OVRInput.Button vrButton = OVRInput.Button.One;   // A / X

        public bool AutoMode => autoMode;

        void Start() => Apply();

        void Update()
        {
            var kb = Keyboard.current;
            bool toggle = kb != null && kb[editorKey].wasPressedThisFrame;
            if (useVrButton && OVRInput.GetDown(vrButton)) toggle = true;
            if (toggle) ToggleMode();
        }

        public void ToggleMode() { autoMode = !autoMode; Apply(); }
        public void SetAuto(bool a) { autoMode = a; Apply(); }

        void Apply()
        {
            if (loop != null) loop.enabled = autoMode;
            if (grabInteractors != null)
                foreach (var b in grabInteractors)
                    if (b != null) b.enabled = !autoMode;
            if (manualOnlyRenderers != null)
                foreach (var r in manualOnlyRenderers)
                    if (r != null) r.enabled = !autoMode;
        }
    }
}
