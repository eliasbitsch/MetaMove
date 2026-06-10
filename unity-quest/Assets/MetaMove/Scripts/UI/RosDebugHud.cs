using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using MetaMove.Robot;

namespace MetaMove.UI
{
    /// <summary>
    /// Editor-mode IMGUI overlay for the Scene_Robot dev bench.
    /// Shows ROS-TCP-Connector link state, /joint_states rate + values, optional
    /// /servo_node/commands rate, plus any JointStateRenderer wired in the scene.
    ///
    /// Not rendered in Quest builds (OnGUI is Editor-/Standalone-only). For the
    /// VR HUD see MetaMove.UI.Hud.StatusHud.
    /// </summary>
    public class RosDebugHud : MonoBehaviour
    {
        [Header("Topics to monitor")]
        public string jointStatesTopic = "/joint_states";
        public string servoCommandsTopic = "/servo_node/commands";

        [Header("UI")]
        public bool showOverlay = true;
        public KeyCode toggleKey = KeyCode.F2;
        public Vector2 origin = new Vector2(16, 16);
        public int width = 360;

        [Header("Optional refs")]
        [Tooltip("If wired, also displays its _currentDeg / _messagesReceived state.")]
        public JointStateRenderer renderer;

        ROSConnection _ros;
        bool _subscribed;

        // /joint_states sampling
        int _jsCount;
        float _jsLastTime;
        float _jsRateHz;
        double[] _jsLastPos = new double[6];

        // /servo_node/commands sampling
        int _cmdCount;
        float _cmdLastTime;
        float _cmdRateHz;

        float _lastRateTick;

        void OnEnable()
        {
            _ros = ROSConnection.GetOrCreateInstance();
            _ros.Subscribe<JointStateMsg>(jointStatesTopic, OnJointState);
            _ros.Subscribe<Float64MultiArrayMsg>(servoCommandsTopic, OnServoCmd);
            _subscribed = true;
        }

        void OnDisable() => _subscribed = false;

        void OnJointState(JointStateMsg m)
        {
            _jsCount++;
            _jsLastTime = Time.unscaledTime;
            if (m.position != null && m.position.Length >= 6)
                for (int i = 0; i < 6; i++) _jsLastPos[i] = m.position[i];
        }

        void OnServoCmd(Float64MultiArrayMsg _)
        {
            _cmdCount++;
            _cmdLastTime = Time.unscaledTime;
        }

        void Update()
        {
            if (ToggleKeyPressed()) showOverlay = !showOverlay;

            // Compute rolling 1s rate.
            float now = Time.unscaledTime;
            if (now - _lastRateTick >= 1f)
            {
                float dt = now - _lastRateTick;
                _jsRateHz = _jsCount / dt;
                _cmdRateHz = _cmdCount / dt;
                _jsCount = 0;
                _cmdCount = 0;
                _lastRateTick = now;
            }
        }

        void OnGUI()
        {
            if (!showOverlay) return;

            int lineH = 18;
            int lines = 12;
            var rect = new Rect(origin.x, origin.y, width, lineH * lines + 16);
            GUI.Box(rect, GUIContent.none);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft,
                richText = true,
                normal = { textColor = Color.white },
            };

            float y = rect.y + 8;
            float x = rect.x + 10;

            string linkColor = _ros != null && _subscribed ? "#7fff7f" : "#ff7f7f";
            string linkLabel = _ros != null && _subscribed ? "subscribed" : "not connected";
            GUI.Label(new Rect(x, y, width - 20, lineH),
                $"<b>ROS-TCP-Connector</b>   <color={linkColor}>{linkLabel}</color>", style);
            y += lineH;
            if (_ros != null)
            {
                GUI.Label(new Rect(x, y, width - 20, lineH),
                    $"  endpoint: {_ros.RosIPAddress}:{_ros.RosPort}", style);
                y += lineH;
            }
            y += 4;

            // /joint_states
            float jsAge = Time.unscaledTime - _jsLastTime;
            string jsColor = jsAge < 0.5f ? "#7fff7f" : "#ff7f7f";
            GUI.Label(new Rect(x, y, width - 20, lineH),
                $"<b>{jointStatesTopic}</b>   <color={jsColor}>{_jsRateHz:F1} Hz</color>   age {jsAge:F2}s", style);
            y += lineH;
            const float Rad2Deg = 180f / Mathf.PI;
            for (int i = 0; i < 6; i++)
            {
                GUI.Label(new Rect(x, y, width - 20, lineH),
                    $"  J{i + 1}  {(float)_jsLastPos[i] * Rad2Deg,7:F2}°", style);
                y += lineH;
            }

            // /servo_node/commands
            float cmdAge = Time.unscaledTime - _cmdLastTime;
            string cmdColor = cmdAge < 0.5f ? "#7fff7f" : "#888888";
            GUI.Label(new Rect(x, y, width - 20, lineH),
                $"<b>{servoCommandsTopic}</b>   <color={cmdColor}>{_cmdRateHz:F1} Hz</color>", style);
            y += lineH;

            // Optional renderer state
            if (renderer != null)
            {
                GUI.Label(new Rect(x, y, width - 20, lineH),
                    $"<b>JointStateRenderer</b>   rx={GetRendererCount()}   age {GetRendererAge():F2}s", style);
            }

            GUI.Label(new Rect(rect.x + 10, rect.yMax - 22, width - 20, 16),
                $"[{toggleKey}] toggle overlay", style);
        }

        bool ToggleKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            return toggleKey switch
            {
                KeyCode.F1 => kb.f1Key.wasPressedThisFrame,
                KeyCode.F2 => kb.f2Key.wasPressedThisFrame,
                KeyCode.F3 => kb.f3Key.wasPressedThisFrame,
                KeyCode.F4 => kb.f4Key.wasPressedThisFrame,
                KeyCode.H  => kb.hKey.wasPressedThisFrame,
                _ => false,
            };
#else
            return Input.GetKeyDown(toggleKey);
#endif
        }

        int GetRendererCount()
        {
            // Reflection avoided — JointStateRenderer exposes count via SerializeField but
            // not as public. Read via component property mirroring.
            if (renderer == null) return 0;
            var fld = typeof(JointStateRenderer).GetField("_messagesReceived",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return fld != null ? (int)fld.GetValue(renderer) : 0;
        }

        float GetRendererAge()
        {
            if (renderer == null) return 0;
            var fld = typeof(JointStateRenderer).GetField("_lastMsgAgeSec",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return fld != null ? (float)fld.GetValue(renderer) : 0;
        }
    }
}
