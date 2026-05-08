using System.Collections;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MetaMove.Robot.EGM
{
    // Project hat das neue Input-System aktiv, daher dürfen wir UnityEngine.Input.*
    // nicht aufrufen. Diese Helpers spiegeln nur was der Tester braucht.
    static class Kbd
    {
#if ENABLE_INPUT_SYSTEM
        static UnityEngine.InputSystem.Key Map(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.UpArrow:    return UnityEngine.InputSystem.Key.UpArrow;
                case KeyCode.DownArrow:  return UnityEngine.InputSystem.Key.DownArrow;
                case KeyCode.LeftArrow:  return UnityEngine.InputSystem.Key.LeftArrow;
                case KeyCode.RightArrow: return UnityEngine.InputSystem.Key.RightArrow;
                case KeyCode.Q:          return UnityEngine.InputSystem.Key.Q;
                case KeyCode.E:          return UnityEngine.InputSystem.Key.E;
                case KeyCode.H:          return UnityEngine.InputSystem.Key.H;
                case KeyCode.M:          return UnityEngine.InputSystem.Key.M;
                case KeyCode.F1:         return UnityEngine.InputSystem.Key.F1;
                case KeyCode.LeftShift:  return UnityEngine.InputSystem.Key.LeftShift;
                case KeyCode.I:          return UnityEngine.InputSystem.Key.I;
                case KeyCode.J:          return UnityEngine.InputSystem.Key.J;
                case KeyCode.K:          return UnityEngine.InputSystem.Key.K;
                case KeyCode.L:          return UnityEngine.InputSystem.Key.L;
                case KeyCode.U:          return UnityEngine.InputSystem.Key.U;
                case KeyCode.O:          return UnityEngine.InputSystem.Key.O;
                case KeyCode.Alpha1:     return UnityEngine.InputSystem.Key.Digit1;
                case KeyCode.Alpha2:     return UnityEngine.InputSystem.Key.Digit2;
                case KeyCode.Alpha3:     return UnityEngine.InputSystem.Key.Digit3;
                case KeyCode.Alpha4:     return UnityEngine.InputSystem.Key.Digit4;
                case KeyCode.Alpha5:     return UnityEngine.InputSystem.Key.Digit5;
                case KeyCode.Alpha6:     return UnityEngine.InputSystem.Key.Digit6;
                case KeyCode.Plus:
                case KeyCode.KeypadPlus: return UnityEngine.InputSystem.Key.NumpadPlus;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus: return UnityEngine.InputSystem.Key.NumpadMinus;
                case KeyCode.PageUp:     return UnityEngine.InputSystem.Key.PageUp;
                case KeyCode.PageDown:   return UnityEngine.InputSystem.Key.PageDown;
                case KeyCode.Z:            return UnityEngine.InputSystem.Key.Z;
                case KeyCode.X:            return UnityEngine.InputSystem.Key.X;
                case KeyCode.C:            return UnityEngine.InputSystem.Key.C;
                case KeyCode.V:            return UnityEngine.InputSystem.Key.V;
                case KeyCode.R:            return UnityEngine.InputSystem.Key.R;
                case KeyCode.F:            return UnityEngine.InputSystem.Key.F;
                default:                 return UnityEngine.InputSystem.Key.None;
            }
        }
        public static bool GetKey(KeyCode k)
        {
            var kb = Keyboard.current; if (kb == null) return false;
            var key = Map(k); if (key == UnityEngine.InputSystem.Key.None) return false;
            return kb[key].isPressed;
        }
        public static bool GetKeyDown(KeyCode k)
        {
            var kb = Keyboard.current; if (kb == null) return false;
            var key = Map(k); if (key == UnityEngine.InputSystem.Key.None) return false;
            return kb[key].wasPressedThisFrame;
        }
#else
        public static bool GetKey(KeyCode k) => Input.GetKey(k);
        public static bool GetKeyDown(KeyCode k) => Input.GetKeyDown(k);
#endif
    }

    // Unified EGM keyboard tester — supports both Pose teleop (RAPID Mode 9)
    // and per-axis Joint teleop (RAPID Mode 10). Toggle with M key.
    //
    // Pose mode keys:
    //   Arrows = X/Y, Q/E = Z, IJKL = Rx/Ry, U/O = Rz
    // Joint mode keys:
    //   1..6 = select joint, +/- = jog selected joint
    // Common:
    //   Shift hold = fast (5x)
    //   H hold = ramp toward home (smooth)
    //   F1 = capture current target as home
    //   M = toggle Pose ↔ Joint mode (Unity-side; remember to set RAPID metaCmd)
    //   PageUp/Down = adjust linear speed
    //   Home/End = adjust angular speed
    public class EgmKeyboardTester : MonoBehaviour
    {
        public enum Mode { Pose, Joint }

        [Header("Mode")]
        public Mode mode = Mode.Pose;

        [Header("EGM clients (separate UDP ports — Pose on 6512, Joint on 6513)")]
        [Tooltip("EgmClient for Pose mode (typically port 6512). Required.")]
        public EgmClient egmPose;
        [Tooltip("EgmClient for Joint mode (typically port 6513). Required if Joint mode used.")]
        public EgmClient egmJoint;
        [Tooltip("Beim M-Toggle automatisch RAPID metaCmd via RWS setzen (9=Pose, 10=Joint).")]
        public bool autoSwitchRapidMode = true;

        [Header("RWS (für autoSwitchRapidMode)")]
        public string rwsHost = "192.168.125.1";
        public string rwsUser = "Default User";
        public string rwsPassword = "robotics";

        // Cached web-session cookie + mastership grab
        string _rwsCookieJar = null;
        bool _rwsBusy = false;

        [Header("Speeds")]
        [Tooltip("Translation pro Sekunde (Meter) bei gehaltener Pfeiltaste.")]
        public float linearSpeed = 0.05f;
        [Tooltip("Rotation pro Sekunde (Grad) bei gehaltener Rotation-Taste.")]
        public float angularSpeed = 30f;
        [Tooltip("Joint-Rotation pro Sekunde (Grad) bei gehaltener +/- Taste.")]
        public float jointSpeed = 20f;
        [Tooltip("Multiplikator wenn Hold-Key (Shift) gedrückt ist.")]
        public float fastMultiplier = 5f;
        [Tooltip("HOME-Recall nutzt linearSpeed × homeSpeedFactor (Default 1 = volle Slider-Geschwindigkeit; 0.3 = ein Drittel davon).")]
        [Range(0.1f, 1f)] public float homeSpeedFactor = 1f;

        [Header("Streaming")]
        [Range(50f, 500f)] public float streamRateHz = 250f;

        [Header("Pose home (mm + quaternion wxyz, ABB-Frame)")]
        public bool homeOverride = false;
        public Vector3 homePosMm = new Vector3(400f, 0f, 600f);
        public Vector4 homeRotWxyz = new Vector4(0f, 0f, 1f, 0f);

        [Header("Pose limits (sanity, mm)")]
        public Vector3 minPosMm = new Vector3(150f, -500f, 100f);
        public Vector3 maxPosMm = new Vector3(800f,  500f, 900f);

        [Header("Joint limits (deg, GoFa CRB 15000-5/0.95)")]
        public Vector2 j1Limits = new Vector2(-180f, 180f);
        public Vector2 j2Limits = new Vector2(-90f, 90f);
        public Vector2 j3Limits = new Vector2(-180f, 50f);
        public Vector2 j4Limits = new Vector2(-180f, 180f);
        public Vector2 j5Limits = new Vector2(-225f, 225f);
        public Vector2 j6Limits = new Vector2(-360f, 360f);

        [Header("Status (read-only)")]
        [SerializeField] Vector3 _targetPosMm;
        [SerializeField] Vector4 _targetRotWxyz;
        [SerializeField] double[] _jointTargetDeg = new double[6];
        [SerializeField] double[] _jointHomeDeg = new double[6];
        [SerializeField] int _activeJoint = 6;
        [SerializeField] bool _havePose, _haveJoints, _haveHome, _haveJointHome;
        [SerializeField] bool _connected;
        [SerializeField] float _hz;

        float _lastSend;
        float _modeSwitchSettleUntil;
        EgmClient ActiveClient => mode == Mode.Pose ? egmPose : egmJoint;

        void Awake()
        {
            // Backward-compat: if no explicit pose client wired, use this GameObject's first EgmClient
            if (egmPose == null) egmPose = GetComponent<EgmClient>();
            if (homeOverride) _haveHome = true;
        }

        void Start()
        {
            if (autoSwitchRapidMode) StartCoroutine(SetRapidMetaCmd(mode == Mode.Pose ? 9 : 10));
        }

        void Update()
        {
            var c = ActiveClient;
            _connected = c != null && c.Connected;
            _hz = c != null ? c.MeasuredHz : 0f;

            // Capture initial pose + joints from feedback (active client)
            if (c != null && c.TryGetLatest(out var fb))
            {
                if (!_havePose)
                {
                    _targetPosMm = new Vector3((float)fb.cartesian.pos.x, (float)fb.cartesian.pos.y, (float)fb.cartesian.pos.z);
                    _targetRotWxyz = new Vector4((float)fb.cartesian.orient.u0, (float)fb.cartesian.orient.u1, (float)fb.cartesian.orient.u2, (float)fb.cartesian.orient.u3);
                    _havePose = true;
                    if (!_haveHome && !homeOverride)
                    {
                        homePosMm = _targetPosMm;
                        homeRotWxyz = _targetRotWxyz;
                        _haveHome = true;
                    }
                }
                if (!_haveJoints && fb.joints != null && fb.joints.joints.Count >= 6)
                {
                    for (int i = 0; i < 6; i++) _jointTargetDeg[i] = fb.joints.joints[i];
                    _haveJoints = true;
                    if (!_haveJointHome)
                    {
                        for (int i = 0; i < 6; i++) _jointHomeDeg[i] = _jointTargetDeg[i];
                        _haveJointHome = true;
                    }
                }
            }

            // M = mode toggle is INTENTIONALLY DISABLED. ABB EGM does not support
            // reliable live mode switching — we tried separate UDPUC devices, separate
            // ports, EGMStop+long waits, settle pauses, cold restarts, and even with
            // all that the robot either errors with "could not open device" or jumps
            // at max speed between modes. Architecture is now: pick mode at Play start
            // (Inspector), restart program to switch.
            if (Kbd.GetKeyDown(KeyCode.M))
            {
                Debug.Log("[EGM] Mode toggle disabled — Stop Play, change Inspector 'mode' field, Play again.");
            }

            // Speed adjustment hotkeys
            float dt = Time.unscaledDeltaTime;
            bool fast = Kbd.GetKey(KeyCode.LeftShift);
            float fastMul = fast ? fastMultiplier : 1f;
            if (Kbd.GetKey(KeyCode.X)) linearSpeed = Mathf.Min(0.5f,   linearSpeed + 0.05f * dt);
            if (Kbd.GetKey(KeyCode.Z)) linearSpeed = Mathf.Max(0.005f, linearSpeed - 0.05f * dt);
            if (Kbd.GetKey(KeyCode.V)) angularSpeed = Mathf.Min(180f, angularSpeed + 30f * dt);
            if (Kbd.GetKey(KeyCode.C)) angularSpeed = Mathf.Max(1f,   angularSpeed - 30f * dt);

            if (mode == Mode.Pose) UpdatePose(dt, fastMul);
            else                   UpdateJoint(dt, fastMul);

            // Stream — pause briefly after mode-switch so robot can settle
            if (Time.unscaledTime < _modeSwitchSettleUntil) return;
            float minInterval = 1f / Mathf.Max(50f, streamRateHz);
            if (Time.unscaledTime - _lastSend < minInterval) return;
            _lastSend = Time.unscaledTime;

            if (mode == Mode.Pose && _havePose && egmPose != null)
            {
                Vector3 posM = _targetPosMm * 0.001f;
                Quaternion rot = new Quaternion(_targetRotWxyz.y, _targetRotWxyz.z, _targetRotWxyz.w, _targetRotWxyz.x);
                egmPose.SendPose(posM, rot);
            }
            else if (mode == Mode.Joint && _haveJoints && egmJoint != null)
            {
                egmJoint.SendJoints(_jointTargetDeg);
            }
        }

        void UpdatePose(float dt, float fastMul)
        {
            if (!_havePose) return;

            // HOME-Recall: ramp position AND rotation independently with their slider speeds
            if (Kbd.GetKey(KeyCode.H) && _haveHome)
            {
                // Position ramp
                Vector3 delta = homePosMm - _targetPosMm;
                float posStep = linearSpeed * 1000f * homeSpeedFactor * dt;
                if (delta.magnitude <= posStep) _targetPosMm = homePosMm;
                else _targetPosMm += delta.normalized * posStep;

                // Rotation ramp via Quaternion.RotateTowards (angular limit per frame)
                Quaternion curRot = new Quaternion(_targetRotWxyz.y, _targetRotWxyz.z, _targetRotWxyz.w, _targetRotWxyz.x);
                Quaternion homeRot = new Quaternion(homeRotWxyz.y, homeRotWxyz.z, homeRotWxyz.w, homeRotWxyz.x);
                float rotStep = angularSpeed * homeSpeedFactor * dt;
                Quaternion next = Quaternion.RotateTowards(curRot, homeRot, rotStep);
                _targetRotWxyz = new Vector4(next.w, next.x, next.y, next.z);
            }
            if (Kbd.GetKeyDown(KeyCode.F1))
            {
                homePosMm = _targetPosMm; homeRotWxyz = _targetRotWxyz; _haveHome = true;
                Debug.Log($"[EGM] Pose home set: {homePosMm}");
            }

            float linStep = linearSpeed * 1000f * fastMul * dt;
            float dx = 0f, dy = 0f, dz = 0f;
            if (Kbd.GetKey(KeyCode.UpArrow))    dx += linStep;
            if (Kbd.GetKey(KeyCode.DownArrow))  dx -= linStep;
            if (Kbd.GetKey(KeyCode.LeftArrow))  dy += linStep;
            if (Kbd.GetKey(KeyCode.RightArrow)) dy -= linStep;
            if (Kbd.GetKey(KeyCode.E))          dz += linStep;
            if (Kbd.GetKey(KeyCode.Q))          dz -= linStep;
            _targetPosMm.x = Mathf.Clamp(_targetPosMm.x + dx, minPosMm.x, maxPosMm.x);
            _targetPosMm.y = Mathf.Clamp(_targetPosMm.y + dy, minPosMm.y, maxPosMm.y);
            _targetPosMm.z = Mathf.Clamp(_targetPosMm.z + dz, minPosMm.z, maxPosMm.z);

            float angStep = angularSpeed * fastMul * dt;
            float rx = 0f, ry = 0f, rz = 0f;
            if (Kbd.GetKey(KeyCode.I)) rx += angStep;
            if (Kbd.GetKey(KeyCode.K)) rx -= angStep;
            if (Kbd.GetKey(KeyCode.J)) ry += angStep;
            if (Kbd.GetKey(KeyCode.L)) ry -= angStep;
            if (Kbd.GetKey(KeyCode.U)) rz += angStep;
            if (Kbd.GetKey(KeyCode.O)) rz -= angStep;
            if (rx != 0f || ry != 0f || rz != 0f)
            {
                Quaternion delta = Quaternion.Euler(rx, ry, rz);
                Quaternion target = new Quaternion(_targetRotWxyz.y, _targetRotWxyz.z, _targetRotWxyz.w, _targetRotWxyz.x);
                target = delta * target;
                _targetRotWxyz = new Vector4(target.w, target.x, target.y, target.z);
            }
        }

        void UpdateJoint(float dt, float fastMul)
        {
            if (!_haveJoints) return;

            if (Kbd.GetKeyDown(KeyCode.Alpha1)) _activeJoint = 1;
            if (Kbd.GetKeyDown(KeyCode.Alpha2)) _activeJoint = 2;
            if (Kbd.GetKeyDown(KeyCode.Alpha3)) _activeJoint = 3;
            if (Kbd.GetKeyDown(KeyCode.Alpha4)) _activeJoint = 4;
            if (Kbd.GetKeyDown(KeyCode.Alpha5)) _activeJoint = 5;
            if (Kbd.GetKeyDown(KeyCode.Alpha6)) _activeJoint = 6;

            float jStep = jointSpeed * fastMul * dt;
            float jdelta = 0f;
            if (Kbd.GetKey(KeyCode.R)) jdelta += jStep;
            if (Kbd.GetKey(KeyCode.F)) jdelta -= jStep;
            if (jdelta != 0f)
            {
                int idx = _activeJoint - 1;
                Vector2 lim = idx switch
                {
                    0 => j1Limits, 1 => j2Limits, 2 => j3Limits,
                    3 => j4Limits, 4 => j5Limits, _ => j6Limits
                };
                _jointTargetDeg[idx] = System.Math.Clamp(_jointTargetDeg[idx] + jdelta, lim.x, lim.y);
            }

            if (Kbd.GetKey(KeyCode.H) && _haveJointHome)
            {
                float step = jointSpeed * homeSpeedFactor * dt;
                for (int i = 0; i < 6; i++)
                {
                    double diff = _jointHomeDeg[i] - _jointTargetDeg[i];
                    if (System.Math.Abs(diff) <= step) _jointTargetDeg[i] = _jointHomeDeg[i];
                    else _jointTargetDeg[i] += System.Math.Sign(diff) * step;
                }
            }
            if (Kbd.GetKeyDown(KeyCode.F1))
            {
                for (int i = 0; i < 6; i++) _jointHomeDeg[i] = _jointTargetDeg[i];
                _haveJointHome = true;
                Debug.Log($"[EGM] Joint home set: {string.Join(", ", _jointHomeDeg)}");
            }
        }

        // Sequenz: mastership/edit/request → set metaCmd → mastership/edit/release.
        // Bypassed self-signed TLS via custom CertificateHandler. Cookie jar in
        // memory, not persisted — fresh login every coroutine call (safe & cheap).
        IEnumerator SetRapidMetaCmd(int value)
        {
            if (_rwsBusy) yield break;
            _rwsBusy = true;
            string baseUrl = $"https://{rwsHost}";
            string auth = "Basic " + System.Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{rwsUser}:{rwsPassword}"));

            // Login (just to seed any session cookie / verify reachability)
            var login = UnityWebRequest.Get($"{baseUrl}/rw/system");
            login.SetRequestHeader("Authorization", auth);
            login.SetRequestHeader("Accept", "application/xhtml+xml;v=2.0");
            login.certificateHandler = new BypassTls();
            yield return login.SendWebRequest();
            if (login.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[EGM] RWS login failed: {login.error}");
                _rwsBusy = false; yield break;
            }
            string cookies = login.GetResponseHeader("Set-Cookie") ?? "";

            // Grab mastership
            yield return RwsPost($"{baseUrl}/rw/mastership/edit/request", "", auth, cookies);

            // Set PERS metaCmd
            string body = $"value={value}";
            string symUrl = $"{baseUrl}/rw/rapid/symbol/RAPID%2FT_ROB1%2FMetaMoveCore%2FmetaCmd/data?action=set";
            yield return RwsPost(symUrl, body, auth, cookies);

            // Release mastership
            yield return RwsPost($"{baseUrl}/rw/mastership/edit/release", "", auth, cookies);

            Debug.Log($"[EGM] RAPID metaCmd → {value}");
            _rwsBusy = false;
        }

        IEnumerator RwsPost(string url, string body, string auth, string cookies)
        {
            byte[] payload = string.IsNullOrEmpty(body) ? new byte[0] : System.Text.Encoding.UTF8.GetBytes(body);
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(payload);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", auth);
            req.SetRequestHeader("Accept", "application/xhtml+xml;v=2.0");
            req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded;v=2.0");
            if (!string.IsNullOrEmpty(cookies)) req.SetRequestHeader("Cookie", cookies);
            req.certificateHandler = new BypassTls();
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success && req.responseCode != 204)
                Debug.LogWarning($"[EGM] RWS POST {url} → {req.responseCode} {req.error}");
        }

        // Self-signed cert on the controller — bypass TLS validation for lab use.
        class BypassTls : CertificateHandler { protected override bool ValidateCertificate(byte[] certificateData) => true; }

        void OnGUI()
        {
            const int w = 380, h = 290;
            GUI.Box(new Rect(10, 10, w, h), $"EGM Tester — Mode: {mode}");
            int y = 32;
            GUI.Label(new Rect(20, y, w - 20, 20), $"Connected: {_connected}    Rx: {_hz:F0} Hz"); y += 18;

            if (mode == Mode.Pose)
            {
                GUI.Label(new Rect(20, y, w - 20, 20), $"TCP (mm): {_targetPosMm.x:F1}, {_targetPosMm.y:F1}, {_targetPosMm.z:F1}"); y += 18;
                GUI.Label(new Rect(20, y, w - 20, 20), $"Pose home set: {_haveHome}"); y += 18;
            }
            else
            {
                GUI.Label(new Rect(20, y, w - 20, 20), $"J: {_jointTargetDeg[0]:F1}, {_jointTargetDeg[1]:F1}, {_jointTargetDeg[2]:F1}"); y += 18;
                GUI.Label(new Rect(20, y, w - 20, 20), $"   {_jointTargetDeg[3]:F1}, {_jointTargetDeg[4]:F1}, {_jointTargetDeg[5]:F1}     active: J{_activeJoint}"); y += 18;
            }
            y += 4;

            // Live sliders
            GUI.Label(new Rect(20, y, 130, 20), $"Lin: {linearSpeed * 1000f:F0} mm/s");
            linearSpeed = GUI.HorizontalSlider(new Rect(160, y + 5, w - 180, 12), linearSpeed, 0.005f, 0.5f); y += 22;
            GUI.Label(new Rect(20, y, 130, 20), $"Rot: {angularSpeed:F0} deg/s");
            angularSpeed = GUI.HorizontalSlider(new Rect(160, y + 5, w - 180, 12), angularSpeed, 1f, 180f); y += 22;
            GUI.Label(new Rect(20, y, 130, 20), $"Joint: {jointSpeed:F0} deg/s");
            jointSpeed = GUI.HorizontalSlider(new Rect(160, y + 5, w - 180, 12), jointSpeed, 1f, 180f); y += 24;

            if (mode == Mode.Pose)
            {
                GUI.Label(new Rect(20, y, w - 20, 20), "Arrows=XY  Q/E=Z  IJKL=Rx/Ry  U/O=Rz"); y += 18;
            }
            else
            {
                GUI.Label(new Rect(20, y, w - 20, 20), "1..6 select joint   R/F jog ±"); y += 18;
            }
            GUI.Label(new Rect(20, y, w - 20, 20), "Shift=fast  H hold=HOME  F1=set HOME  M=mode"); y += 18;
            GUI.Label(new Rect(20, y, w - 20, 20), $"Z/X=lin±  C/V=rot±   RWS auto: {autoSwitchRapidMode}"); y += 18;
        }
    }
}
