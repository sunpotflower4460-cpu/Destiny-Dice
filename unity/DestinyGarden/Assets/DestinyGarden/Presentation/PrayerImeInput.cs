#if !ENABLE_LEGACY_INPUT_MANAGER && (!UNITY_WEBGL || UNITY_EDITOR)
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DestinyGarden.Presentation
{
    // uGUI's InputField uses BaseInput for these IME and touch properties.
    internal sealed class PrayerImeInput : BaseInput
    {
        private Keyboard _keyboard;
        private string _composition = "";

        public override string compositionString => _composition;
        public override bool touchSupported => Touchscreen.current != null;

        public override IMECompositionMode imeCompositionMode
        {
            set
            {
                ReleaseKeyboard();
                if (value != IMECompositionMode.On || Keyboard.current == null) return;
                _keyboard = Keyboard.current;
                _keyboard.onIMECompositionChange += OnComposition;
                _keyboard.SetIMEEnabled(true);
            }
        }

        public override Vector2 compositionCursorPos
        {
            set => _keyboard?.SetIMECursorPosition(value);
        }

        private void OnComposition(IMECompositionString composition) => _composition = composition.ToString();

        protected override void OnDisable()
        {
            ReleaseKeyboard();
            base.OnDisable();
        }

        private void ReleaseKeyboard()
        {
            if (_keyboard == null) return;
            _keyboard.onIMECompositionChange -= OnComposition;
            if (_keyboard.added) _keyboard.SetIMEEnabled(false);
            _keyboard = null;
            _composition = "";
        }
    }
}
#endif
