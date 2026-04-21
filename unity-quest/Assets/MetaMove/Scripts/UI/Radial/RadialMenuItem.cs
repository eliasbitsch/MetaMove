using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MetaMove.Settings;

namespace MetaMove.UI.Radial
{
    // One wedge. Wrap a Meta PokeInteractable prefab with this and wire:
    //   WhenSelect  → RadialMenuItem.Activate
    //   WhenHover   → RadialMenuItem.OnHoverEnter
    //   WhenUnhover → RadialMenuItem.OnHoverExit
    //
    // The parent HandRadialMenu injects the per-wedge callbacks through Configure(),
    // so the prefab itself doesn't need any direct reference to the menu.
    public class RadialMenuItem : MonoBehaviour
    {
        public HandRadialMenu.Wedge wedge;
        public Image backdrop;
        public Image iconImage;
        public TextMeshProUGUI labelText;

        UiThemeConfig _theme;
        Color _baseColor, _hoverColor;
        bool _hover;

        Action _onActivate;
        Action _onHoverEnter;
        Action _onHoverExit;

        public void Configure(HandRadialMenu.Wedge w, UiThemeConfig theme,
                              Action onActivate, Action onHoverEnter, Action onHoverExit)
        {
            wedge = w;
            _theme = theme;
            _onActivate = onActivate;
            _onHoverEnter = onHoverEnter;
            _onHoverExit = onHoverExit;

            if (iconImage != null) iconImage.sprite = w.icon;
            if (labelText != null) labelText.text = w.label;

            _baseColor = theme != null ? theme.bgRaised : new Color(0.1f, 0.14f, 0.2f, 0.9f);
            _hoverColor = theme != null ? theme.accent : new Color(0.24f, 0.65f, 1f, 1f);
            if (backdrop != null) backdrop.color = _baseColor;
            if (labelText != null && theme != null) labelText.color = theme.fg;
        }

        public void OnHoverEnter()
        {
            _hover = true;
            Apply();
            _onHoverEnter?.Invoke();
        }

        public void OnHoverExit()
        {
            _hover = false;
            Apply();
            _onHoverExit?.Invoke();
        }

        public void Activate()
        {
            _onActivate?.Invoke();
        }

        void Apply()
        {
            if (backdrop != null) backdrop.color = _hover ? _hoverColor : _baseColor;
        }
    }
}
