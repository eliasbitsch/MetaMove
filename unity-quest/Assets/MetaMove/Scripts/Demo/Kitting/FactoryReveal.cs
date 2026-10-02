using UnityEngine;

namespace MetaMove.Demo.Kitting
{
    // The moment the cell is placed: the slab of factory floor spreads out from under the
    // robot, the wall rises out of the ground, the hall beam and the roof fragment with its
    // sign lower in from above.
    //
    // Driven by a clock in Update rather than a coroutine, and the resting poses are taken
    // once in Awake: whatever re-enables or re-parents the cell, every part always ends
    // exactly where the builder put it.
    public class FactoryReveal : MonoBehaviour
    {
        public Transform floorIsland, wall, beam, portalMask, roof;
        public float floorTime = 0.9f, wallTime = 0.8f, beamTime = 0.7f;
        public float wallDrop = 2.9f, beamLift = 1.5f;

        Vector3 _wall, _beam, _mask, _roof;
        float _t = -1f;
        bool _captured;

        void Awake() => Capture();

        void Capture()
        {
            if (_captured) return;
            if (wall != null) _wall = wall.localPosition;
            if (beam != null) _beam = beam.localPosition;
            if (portalMask != null) _mask = portalMask.localPosition;
            if (roof != null) _roof = roof.localPosition;
            _captured = true;
        }

        void OnEnable() { Capture(); _t = 0f; Apply(0f); }

        void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;
            Apply(_t);
            if (_t > floorTime + wallTime + beamTime) { Apply(1e3f); _t = -1f; }
        }

        static float EaseOut(float x, float p) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), p);

        void Apply(float t)
        {
            float f = EaseOut(t / floorTime, 3f);
            float w = EaseOut((t - floorTime) / wallTime, 3f);
            float b = EaseOut((t - floorTime - wallTime) / beamTime, 2f);
            if (floorIsland != null) floorIsland.localScale = new Vector3(Mathf.Max(0.001f, f), 1f, Mathf.Max(0.001f, f));
            if (wall != null) wall.localPosition = _wall + Vector3.down * wallDrop * (1f - w);
            if (portalMask != null) portalMask.localPosition = _mask + Vector3.down * wallDrop * (1f - w);
            if (beam != null) beam.localPosition = _beam + Vector3.up * beamLift * (1f - b);
            if (roof != null) roof.localPosition = _roof + Vector3.up * beamLift * (1f - b);
        }
    }
}
