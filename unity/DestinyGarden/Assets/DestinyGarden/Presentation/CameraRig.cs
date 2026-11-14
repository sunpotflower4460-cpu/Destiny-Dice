using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DestinyGarden.Presentation
{
    /// <summary>
    /// Orbit camera around the island: drag to turn it, wheel or pinch to come closer, a button to go
    /// back. A drag that starts on the interface is ignored. Scripted moves (the opening, the rainbow)
    /// lock the rig and hand the camera back where it ended.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public Vector3 Target = new Vector3(7.5f, -0.65f, 7.5f);

        private float _yaw, _pitch, _dist;           // current
        private float _tYaw, _tPitch, _tDist;        // wanted
        private float _homeYaw, _homePitch, _homeDist;
        private Vector3 _aim, _wantedAim;
        private bool _houseFocus;
        private float _spin;                         // degrees per second left over from a flick
        private int _lock;
        private bool _mouseDrag, _touchDrag;
        private Vector2 _last;
        private float _lastPinch;

        public float HomeYaw => _homeYaw;
        public float HomePitch => _homePitch;
        public float HomeDist => _homeDist;
        internal bool IsHouseFocused => _houseFocus;
        internal bool CanResetView => _lock == 0 && (_houseFocus ||
            Mathf.Abs(Mathf.DeltaAngle(_homeYaw, _tYaw)) > 0.8f ||
            Mathf.Abs(_homePitch - _tPitch) > 0.6f || Mathf.Abs(_homeDist - _tDist) > 0.25f);

        public const float MinPitch = 16f, MaxPitch = 74f, MinDist = 12f, MaxDist = 46f;
        private float Closest => _houseFocus ? 3.1f : MinDist;
        private float Farthest => _houseFocus ? 10f : MaxDist;

        public void SyncFromCamera()
        {
            if (Cam == null) return;
            Vector3 v = Cam.transform.position - _aim;
            float d = Mathf.Max(0.01f, v.magnitude);
            _dist = _tDist = d;
            _yaw = _tYaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            _pitch = _tPitch = Mathf.Asin(Mathf.Clamp(v.y / d, -1f, 1f)) * Mathf.Rad2Deg;
        }

        public void CaptureHome()
        {
            _aim = _wantedAim = Target;
            SyncFromCamera();
            _homeYaw = _yaw; _homePitch = _pitch; _homeDist = _dist;
        }

        public void Lock() { _lock++; }

        public void Unlock()
        {
            _lock = Mathf.Max(0, _lock - 1);
            if (_lock == 0) SyncFromCamera();
        }

        public void ResetView()
        {
            _houseFocus = false;
            Aim(Target, _homeYaw, _homePitch, _homeDist);
        }

        internal void FocusOnHouse(Vector3 target, Vector3 direction, bool snap)
        {
            if (_lock > 0) return;
            _houseFocus = true;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            Aim(target, yaw, 23f, 6.1f * Mathf.Max(1f, 1.3f / Cam.aspect));
            if (snap)
            {
                _aim = _wantedAim;
                _yaw = _tYaw;
                _pitch = _tPitch;
                _dist = _tDist;
                Apply();
            }
        }

        /// <summary>Briefly frame the saved event and return to the player's view.</summary>
        internal IEnumerator FocusOnRoll(Vector3 cell, bool reducedMotion)
        {
            if (reducedMotion || _lock > 0 || Cam == null) yield break;
            Vector3 start = Cam.transform.position;
            Quaternion startRotation = Cam.transform.rotation;
            Vector3 target = cell + Vector3.up * 0.45f;
            Vector3 viewDirection = -Cam.transform.forward;
            float distance = Mathf.Clamp(_dist * 0.58f, 9.5f, 17f);
            Vector3 near = target + viewDirection * distance;
            Quaternion nearRotation = Quaternion.LookRotation(target - near, Vector3.up);
            _mouseDrag = _touchDrag = false;
            _lastPinch = _spin = 0f;
            Lock();
            try
            {
                yield return Move(start, startRotation, near, nearRotation, 0.42f);
                yield return new WaitForSecondsRealtime(1.3f);
                yield return Move(near, nearRotation, start, startRotation, 0.44f);
            }
            finally
            {
                if (Cam != null) Cam.transform.SetPositionAndRotation(start, startRotation);
                // Restore the orbit's existing state. The camera itself was only moved for the reveal.
                _lock = Mathf.Max(0, _lock - 1);
            }
        }

        private IEnumerator Move(Vector3 from, Quaternion fromRotation, Vector3 to, Quaternion toRotation, float duration)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float fraction = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                Cam.transform.SetPositionAndRotation(Vector3.Lerp(from, to, fraction), Quaternion.Slerp(fromRotation, toRotation, fraction));
                yield return null;
            }
            Cam.transform.SetPositionAndRotation(to, toRotation);
        }

        private void Aim(Vector3 target, float yaw, float pitch, float distance)
        {
            _wantedAim = target;
            _tYaw = _yaw + Mathf.DeltaAngle(_yaw, yaw);
            _tPitch = pitch;
            _tDist = distance;
            _spin = 0f;
        }

        /// <summary>For QA captures: set the view directly (degrees, units).</summary>
        public void SetView(float yaw, float pitch, float dist)
        {
            _houseFocus = false;
            _aim = _wantedAim = Target;
            _yaw = _tYaw = yaw;
            _pitch = _tPitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
            _dist = _tDist = Mathf.Clamp(dist, MinDist, MaxDist);
            Apply();
        }

        private static bool OverUi(int touchId)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return touchId < 0 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(touchId);
        }

        private void Orbit(Vector2 delta)
        {
            _tYaw += delta.x * 0.25f;
            _tPitch = Mathf.Clamp(_tPitch - delta.y * 0.18f, MinPitch, MaxPitch);
            _spin = delta.x * 0.25f / Mathf.Max(0.008f, Time.unscaledDeltaTime) * 0.35f;
        }

        private void Update()
        {
            if (_lock > 0 || Cam == null) return;
            var touch = Touchscreen.current;
            var mouse = Mouse.current;
            bool handled = false;

            if (touch != null && touch.touches.Count > 0)
            {
                int active = 0; Vector2 p0 = default, p1 = default; int id0 = -1;
                foreach (var t in touch.touches)
                {
                    if (!t.press.isPressed) continue;
                    if (active == 0) { p0 = t.position.ReadValue(); id0 = t.touchId.ReadValue(); }
                    else if (active == 1) p1 = t.position.ReadValue();
                    active++;
                }
                if (active >= 2)
                {
                    float d = Vector2.Distance(p0, p1);
                    if (_lastPinch > 0f && d > 1f) _tDist = Mathf.Clamp(_tDist * _lastPinch / d, Closest, Farthest);
                    _lastPinch = d; _touchDrag = false; handled = true;
                }
                else if (active == 1)
                {
                    _lastPinch = 0f;
                    if (touch.primaryTouch.press.wasPressedThisFrame) { _touchDrag = !OverUi(id0); _last = p0; _spin = 0f; }
                    else if (_touchDrag) { Orbit(p0 - _last); _last = p0; }
                    handled = true;
                }
                else { _lastPinch = 0f; _touchDrag = false; } // no finger: leave the mouse alone (a touchscreen device can exist on a desktop)
            }

            if (!handled && mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame) { _mouseDrag = !OverUi(-1); _last = pos; _spin = 0f; }
                else if (mouse.leftButton.isPressed && _mouseDrag) { Orbit(pos - _last); _last = pos; }
                else if (!mouse.leftButton.isPressed) _mouseDrag = false;
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !OverUi(-1))
                    _tDist = Mathf.Clamp(_tDist * (1f - Mathf.Sign(scroll) * 0.07f), Closest, Farthest);
            }

            if (!_mouseDrag && !_touchDrag && Mathf.Abs(_spin) > 0.05f)
            {
                _tYaw += _spin * Time.unscaledDeltaTime;
                _spin = Mathf.MoveTowards(_spin, 0f, Mathf.Abs(_spin) * 2.5f * Time.unscaledDeltaTime + 0.5f * Time.unscaledDeltaTime);
            }
        }

        private void Apply()
        {
            float cp = Mathf.Cos(_pitch * Mathf.Deg2Rad), sp = Mathf.Sin(_pitch * Mathf.Deg2Rad);
            float cy = Mathf.Cos(_yaw * Mathf.Deg2Rad), sy = Mathf.Sin(_yaw * Mathf.Deg2Rad);
            Vector3 pos = _aim + _dist * new Vector3(sy * cp, sp, cy * cp);
            // Give the mouth of the bottle room at the end of the pullback.
            float lift = Mathf.SmoothStep(0f, 6.2f, Mathf.InverseLerp(30f, MaxDist, _dist));
            Vector3 aim = _aim + Vector3.up * lift;
            Cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(aim - pos, Vector3.up));
        }

        private void LateUpdate()
        {
            if (_lock > 0 || Cam == null) return;
            float k = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);
            _aim = Vector3.Lerp(_aim, _wantedAim, k);
            _yaw = Mathf.Lerp(_yaw, _tYaw, k);
            _pitch = Mathf.Lerp(_pitch, _tPitch, k);
            _dist = Mathf.Lerp(_dist, _tDist, k);
            Apply();
        }
    }
}
