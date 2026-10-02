using System.IO;
using UnityEngine;

namespace MetaMove.Demo
{
    // Recording mode without filming the room: if the file 'record_mode' exists in the app's
    // files folder, passthrough is switched off and the cameras clear to a dark grey.
    //   adb shell touch /sdcard/Android/data/com.MetaMove.Bari/files/record_mode   (on)
    //   adb shell rm    /sdcard/Android/data/com.MetaMove.Bari/files/record_mode   (off)
    // Read once at start; restart the app after switching.
    public class RecordingBackdrop : MonoBehaviour
    {
        public Color backdrop = new Color(0.16f, 0.17f, 0.19f, 1f);

        public static bool Active { get; private set; }

        void Start()
        {
            Active = File.Exists(Path.Combine(Application.persistentDataPath, "record_mode"));
            if (!Active) return;
            foreach (var layer in FindObjectsByType<OVRPassthroughLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                layer.enabled = false;
            if (OVRManager.instance != null) OVRManager.instance.isInsightPassthroughEnabled = false;
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = backdrop;
            }
            Debug.Log("[Record] passthrough off, dark backdrop");
        }
    }
}
