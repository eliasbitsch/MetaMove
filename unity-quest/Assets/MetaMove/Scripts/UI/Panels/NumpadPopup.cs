using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MetaMove.UI.Panels
{
    // Pokable numeric entry. Wraps Meta's Keypad.prefab (or any 12-button equivalent)
    // for entering exact values — degrees, millimeters, IP bytes, etc. One popup
    // per scene; callers request an edit via Request(value, (newValue) => ...).
    //
    // Hook each Meta KeypadButton's onClick to the corresponding Append/Clear/Enter.
    public class NumpadPopup : MonoBehaviour
    {
        public static NumpadPopup Instance { get; private set; }

        [Header("UI")]
        public TMP_Text displayText;
        public TMP_Text promptText;
        public Button enterButton;
        public Button cancelButton;
        public Button backspaceButton;

        [Header("Behaviour")]
        public int maxDigits = 8;
        public bool allowDecimal = true;
        public bool allowSign = true;

        string _buffer;
        Action<float> _onSubmit;
        Action _onCancel;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (enterButton != null) enterButton.onClick.AddListener(Submit);
            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
            if (backspaceButton != null) backspaceButton.onClick.AddListener(Backspace);
            gameObject.SetActive(false);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Request(string prompt, float initial, Action<float> onSubmit, Action onCancel = null)
        {
            _onSubmit = onSubmit;
            _onCancel = onCancel;
            _buffer = initial.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
            if (promptText != null) promptText.text = prompt;
            Refresh();
            gameObject.SetActive(true);
        }

        // Wire each digit button's onClick to AppendDigit with the corresponding int.
        public void AppendDigit(int d)
        {
            if (_buffer.Replace("-", "").Replace(".", "").Length >= maxDigits) return;
            if (_buffer == "0") _buffer = "";
            _buffer += d.ToString();
            Refresh();
        }

        // Wire to the "." button if allowDecimal.
        public void AppendDecimal()
        {
            if (!allowDecimal || _buffer.Contains(".")) return;
            if (_buffer.Length == 0 || _buffer == "-") _buffer += "0";
            _buffer += ".";
            Refresh();
        }

        // Wire to the "+/-" button if allowSign.
        public void ToggleSign()
        {
            if (!allowSign) return;
            if (_buffer.StartsWith("-")) _buffer = _buffer.Substring(1);
            else _buffer = "-" + _buffer;
            Refresh();
        }

        public void Backspace()
        {
            if (_buffer.Length == 0) return;
            _buffer = _buffer.Substring(0, _buffer.Length - 1);
            Refresh();
        }

        public void Clear()
        {
            _buffer = "";
            Refresh();
        }

        public void Submit()
        {
            if (float.TryParse(_buffer, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                _onSubmit?.Invoke(v);
            }
            gameObject.SetActive(false);
        }

        public void Cancel()
        {
            _onCancel?.Invoke();
            gameObject.SetActive(false);
        }

        void Refresh()
        {
            if (displayText != null) displayText.text = string.IsNullOrEmpty(_buffer) ? "0" : _buffer;
        }
    }
}
