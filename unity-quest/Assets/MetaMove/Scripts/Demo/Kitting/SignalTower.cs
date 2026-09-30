using UnityEngine;
using MetaMove.Safety;

namespace MetaMove.Demo.Kitting
{
    // Andon light on the table corner, the physical twin of the floor rings:
    //   green          line running at full speed (visitor outside dFar)
    //   amber          speed scaled (visitor between dFar and dNear)
    //   amber, blink   line needs attention (no parts, tray to show)
    //   red, blink     standstill (someone inside dNear)
    // Lamps are URP Lit materials with emission enabled; the colour is driven per
    // renderer through a MaterialPropertyBlock so the shared materials stay untouched.
    public class SignalTower : MonoBehaviour
    {
        public ProximitySpeedController speed;
        public KittingCell cell;
        public Renderer green, amber, red;
        public float intensity = 4f;
        public float blinkHz = 2f;

        static readonly Color G = new Color(0.15f, 1f, 0.3f);
        static readonly Color A = new Color(1f, 0.55f, 0.05f);
        static readonly Color R = new Color(1f, 0.08f, 0.05f);
        MaterialPropertyBlock _mpb;

        void Update()
        {
            float f = speed != null ? speed.Factor : 1f;
            bool blink = Mathf.Repeat(Time.time * blinkHz, 1f) < 0.5f;
            bool attention = cell != null &&
                (cell.State == KittingCell.Phase.Waiting || cell.State == KittingCell.Phase.Guide);

            bool r = f <= 0.001f && blink;
            bool a = !r && f > 0.001f && (f < 0.99f || (attention && blink));
            bool g = f >= 0.99f && !attention;

            Set(green, G, g);
            Set(amber, A, a);
            Set(red, R, r);
        }

        void Set(Renderer rend, Color c, bool on)
        {
            if (rend == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", on ? c * intensity : Color.black);
            _mpb.SetColor("_BaseColor", on ? c : c * 0.25f);
            rend.SetPropertyBlock(_mpb);
        }
    }
}
