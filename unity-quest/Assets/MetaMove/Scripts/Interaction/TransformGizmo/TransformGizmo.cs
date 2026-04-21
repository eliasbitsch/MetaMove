using UnityEngine;

namespace MetaMove.Interaction.Gizmo
{
    // Orchestrates a 6-DOF MoveIt-style gizmo:
    //   - Three arrows (X/Y/Z) for axis-constrained translation
    //   - Three rings (X/Y/Z) for axis-constrained rotation
    //   - Optional free-move sphere (we already have this as the "ball" distance-grab handle)
    //
    // Toggle visibility + modes centrally. Each arrow/ring GameObject has its own
    // TransformGizmoAxis configured with its axis + mode. The six are listed here for
    // show/hide and mode-switching.
    public class TransformGizmo : MonoBehaviour
    {
        public enum DisplayMode { Off, TranslateOnly, RotateOnly, TranslateAndRotate, Ball, BallAndAxes }

        public DisplayMode mode = DisplayMode.BallAndAxes;

        [Header("Translate Arrows")]
        public GameObject arrowX;
        public GameObject arrowY;
        public GameObject arrowZ;

        [Header("Rotate Rings")]
        public GameObject ringX;
        public GameObject ringY;
        public GameObject ringZ;

        [Header("Free-Move Ball (distance-grab sphere)")]
        public GameObject ball;

        void OnEnable() { Apply(); }
        void OnValidate() { Apply(); }

        public void SetMode(DisplayMode m) { mode = m; Apply(); }

        void Apply()
        {
            bool axes = mode == DisplayMode.TranslateOnly
                     || mode == DisplayMode.RotateOnly
                     || mode == DisplayMode.TranslateAndRotate
                     || mode == DisplayMode.BallAndAxes;

            bool trans = axes && mode != DisplayMode.RotateOnly;
            bool rot   = axes && mode != DisplayMode.TranslateOnly;
            bool ballOn = mode == DisplayMode.Ball || mode == DisplayMode.BallAndAxes;

            if (arrowX != null) arrowX.SetActive(trans);
            if (arrowY != null) arrowY.SetActive(trans);
            if (arrowZ != null) arrowZ.SetActive(trans);
            if (ringX != null) ringX.SetActive(rot);
            if (ringY != null) ringY.SetActive(rot);
            if (ringZ != null) ringZ.SetActive(rot);
            if (ball != null) ball.SetActive(ballOn);
        }

        // Hooks for mini-panel / radial toggles
        public void ShowBallOnly() => SetMode(DisplayMode.Ball);
        public void ShowAxesOnly() => SetMode(DisplayMode.TranslateAndRotate);
        public void ShowBoth() => SetMode(DisplayMode.BallAndAxes);
        public void Hide() => SetMode(DisplayMode.Off);
    }
}
