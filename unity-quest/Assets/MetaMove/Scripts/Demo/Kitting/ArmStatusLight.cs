using System.Collections.Generic;
using UnityEngine;
using MetaMove.Safety;
using MetaMove.UI.Kit;

namespace MetaMove.Demo.Kitting
{
    // The GoFa's own LED rings (the model's green "0,255,0" material, where the real arm has its
    // status lights) show what the speed monitoring does right now, so the visitor reads it off
    // the arm itself:
    //   green  full speed      amber  slowed (brighter the slower)
    //   red    stopped, pulsing          cyan  hand guiding, monitoring paused
    // Those material slots get an unlit material so they glow; colour per slot by property block,
    // the rest of each link keeps its own look.
    public class ArmStatusLight : MonoBehaviour
    {
        [Tooltip("Unlit material the LED slots switch to.")]
        public Material ledMaterial;
        [Tooltip("Material name of the LED rings in the robot model.")]
        public string ledMaterialName = "0,255,0";

        ProximitySpeedController _speed;
        KittingCell _cell;
        readonly List<KeyValuePair<Renderer, int>> _slots = new List<KeyValuePair<Renderer, int>>();
        MaterialPropertyBlock _mpb;
        Color _shown = UIKit.Ok;

        void Start()
        {
            // the robot is a prefab: the scene's controllers are found, not wired
            _speed = FindAnyObjectByType<ProximitySpeedController>();
            _cell = FindAnyObjectByType<KittingCell>();
            _mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool hit = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name.StartsWith(ledMaterialName))
                    {
                        if (ledMaterial != null) mats[i] = ledMaterial;
                        _slots.Add(new KeyValuePair<Renderer, int>(r, i));
                        hit = true;
                    }
                if (hit && ledMaterial != null) r.sharedMaterials = mats;
            }
            Debug.Log("[ArmStatusLight] LED slots: " + _slots.Count);
        }

        void Update()
        {
            if (_slots.Count == 0) return;
            float f = _speed != null ? Mathf.Clamp01(_speed.Factor) : 1f;
            bool guiding = _cell != null && (_cell.State == KittingCell.Phase.Guide || _cell.State == KittingCell.Phase.Align);
            Color target;
            if (guiding) target = UIKit.Accent;
            else if (f <= 0.001f) target = UIKit.Stop * Mathf.Lerp(0.45f, 1.25f, 0.5f + 0.5f * Mathf.Sin(Time.time * 7f));
            else if (f < 0.99f) target = UIKit.Warn * Mathf.Lerp(1.15f, 0.85f, f);
            else target = UIKit.Ok;
            target.a = 1f;
            _shown = Color.Lerp(_shown, target, 1f - Mathf.Exp(-Time.deltaTime / 0.06f));
            foreach (var s in _slots)
            {
                if (s.Key == null) continue;
                s.Key.GetPropertyBlock(_mpb, s.Value);
                _mpb.SetColor("_BaseColor", _shown);
                s.Key.SetPropertyBlock(_mpb, s.Value);
            }
        }
    }
}
