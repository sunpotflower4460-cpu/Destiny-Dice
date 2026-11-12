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
        public Vector3 Target = new Vector3(7.5f, 0.2f, 8.4f);

        private float _yaw, _pitch, _dist;           // current
        private float _tYaw, _tPitch, _tDist;        // wanted
        private float _homeYaw, _homePitch, _homeDist;
        private float _spin;                         // degrees per second left over from a flick
        private int _lock;
        private bool _mouseDrag, _touchDrag;
        private Vector2 _last;
        private float _lastPinch;

        public float HomeYaw => _homeYaw;
        public float HomePitch => _homePitch;
        public float HomeDist => _homeDist;

        public const float MinPitch = 16f, MaxPitch = 74f, MinDist = 12f, MaxDist = 30f;

        public void SyncFromCamera()
        {
            if (Cam == null) return;
            Vector3 v = Cam.transform.position - Target;
            float d = Mathf.Max(0.01f, v.magnitude);
            _dist = _tDist = d;
            _yaw = _tYaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            _pitch = _tPitch = Mathf.Asin(Mathf.Clamp(v.y / d, -1f, 1f)) * Mathf.Rad2Deg;
        }

        public void CaptureHome()
        {
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
            _tYaw = _yaw + Mathf.DeltaAngle(_yaw, _homeYaw);
            _tPitch = _homePitch; _tDist = _homeDist; _spin = 0f;
        }

        /// <summary>For QA captures: set the view directly (degrees, units).</summary>
        public void SetView(float yaw, float pitch, float dist)
        {
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
                    if (_lastPinch > 0f && d > 1f) _tDist = Mathf.Clamp(_tDist * _lastPinch / d, MinDist, MaxDist);
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
                    _tDist = Mathf.Clamp(_tDist * (1f - Mathf.Sign(scroll) * 0.07f), MinDist, MaxDist);
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
            Vector3 pos = Target + _dist * new Vector3(sy * cp, sp, cy * cp);
            Cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Target - pos, Vector3.up));
        }

        private void LateUpdate()
        {
            if (_lock > 0 || Cam == null) return;
            float k = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);
            _yaw = Mathf.Lerp(_yaw, _tYaw, k);
            _pitch = Mathf.Lerp(_pitch, _tPitch, k);
            _dist = Mathf.Lerp(_dist, _tDist, k);
            Apply();
        }
    }
}
