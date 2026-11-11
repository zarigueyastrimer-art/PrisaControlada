using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.Player
{
    [RequireComponent(typeof(Camera))]
    public class PlayerCamera : MonoBehaviour
    {
        [Header("Look Settings")]
        [SerializeField] private float lookSpeed = 2f;
        [SerializeField, Range(0f, 90f)] private float lookXLimit = 90f;
        [SerializeField] private float lookSmoothing = 0.06f;

        [Header("Lag Settings")]
        [SerializeField, Range(0f, 0.5f)] private float rotationLag = 0.12f;
        [SerializeField, Range(0f, 0.5f)] private float maxRotationLag = 0.35f;
        [SerializeField, Range(1f, 4f)] private float lagExponent = 2f;
        [SerializeField, Min(0f)] private int lagHealthThreshold = 25;

        [Header("Tilt Settings")]
        [SerializeField] private float maxTiltAngle = 4f;
        [SerializeField, Range(0.1f, 5f)] private float tiltResponse = 2.5f;
        [SerializeField, Range(0.1f, 20f)] private float tiltSmoothing = 12f;
        [SerializeField, Range(0.1f, 20f)] private float tiltRecovery = 3f;

        [Header("Zoom sin arma (click derecho, mantener)")]
        [SerializeField] private bool zoomWhenUnarmed = true;
        [SerializeField] private float zoomFov = 45f;
        [SerializeField, Min(0f)] private float zoomSmoothTime = 0.1f;
        [SerializeField, Min(0.1f)] private float weaponScanInterval = 0.5f;

        private Camera _cam;
        private PlayerController _player;
        private PlayerHealth _health;

        private float _targetPitch;
        private float _targetYaw;
        private float _pitch;
        private float _yaw;
        private float _tilt;
        private bool _lookEnabled = true;

        private float _baseFov;
        private float _zoomVelocity;
        private float _nextWeaponScan;
        private WeaponController[] _weapons;
        private ShotgunToggle[] _shotguns;
        private GlockToggle[] _glocks;

        public bool LookEnabled => _lookEnabled;

        public void SetLookEnabled(bool enabled)
        {
            _lookEnabled = enabled;
        }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _player = GetComponentInParent<PlayerController>();
            _health = GetComponentInParent<PlayerHealth>();

            if (_cam != null)
                _baseFov = _cam.fieldOfView;
        }

        private void Start()
        {
            if (_player != null)
                _targetYaw = _yaw = _player.transform.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (_player == null || _cam == null) return;

            Vector2 mouse = Vector2.zero;
            if (_lookEnabled && Mouse.current != null)
            {
                mouse = Mouse.current.delta.ReadValue();

                _targetPitch = Mathf.Clamp(_targetPitch - mouse.y * lookSpeed, -lookXLimit, lookXLimit);
                _targetYaw += mouse.x * lookSpeed;

                // Keep the accumulated yaw bounded so it never drifts to huge values.
                float yawWrap = Mathf.Floor((_targetYaw + 180f) / 360f) * 360f;
                if (yawWrap != 0f)
                    _targetYaw -= yawWrap;
            }

            // First-order exponential approach. No spring, so it can never overshoot,
            // oscillate, or produce NaN no matter how fast the mouse moves.
            float effectiveLag = rotationLag;
            if (_health != null)
            {
                float hp01 = Mathf.Clamp01((float)_health.CurrentHealth / _health.MaxHealth);
                float range = Mathf.Max(0.01f, 1f - Mathf.Clamp01((float)lagHealthThreshold / _health.MaxHealth));
                float t = Mathf.Clamp01((1f - hp01) / range);
                effectiveLag = Mathf.Lerp(rotationLag, maxRotationLag, Mathf.Pow(t, lagExponent));
            }

            float blend = effectiveLag <= 0f
                ? 1f
                : 1f - Mathf.Exp(-(1f / effectiveLag) * Time.deltaTime);

            _pitch = Mathf.Lerp(_pitch, _targetPitch, blend);
            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, blend);

            // Safety: NaN from a bad frame would permanently freeze the camera.
            if (float.IsNaN(_pitch) || float.IsNaN(_yaw))
            {
                _pitch = _targetPitch;
                _yaw = _targetYaw;
            }

            float targetTilt = Mathf.Clamp(mouse.x * lookSpeed * tiltResponse, -1f, 1f) * maxTiltAngle;

            float riseBlend = 1f - Mathf.Exp(-tiltSmoothing * Time.deltaTime);
            float decayBlend = 1f - Mathf.Exp(-tiltRecovery * Time.deltaTime);
            _tilt = Mathf.Abs(targetTilt) > Mathf.Abs(_tilt)
                ? Mathf.Lerp(_tilt, targetTilt, riseBlend)
                : Mathf.Lerp(_tilt, targetTilt, decayBlend);

            _cam.transform.localRotation = Quaternion.Euler(_pitch, 0f, _tilt);
            _player.transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);

            UpdateUnarmedZoom();
        }

        // Click derecho SIN arma equipada: mantener = zoom, soltar = vuelve al FOV normal.
        // Con un arma equipada el FOV lo maneja el arma, asi que aca no se toca.
        private void UpdateUnarmedZoom()
        {
            if (!zoomWhenUnarmed || _cam == null) return;

            if (AnyWeaponEquipped())
            {
                _zoomVelocity = 0f;
                return;
            }

            bool held = _lookEnabled
                && Mouse.current != null
                && Mouse.current.rightButton.isPressed;

            float target = held ? zoomFov : _baseFov;

            if (Mathf.Abs(_cam.fieldOfView - target) <= 0.01f)
            {
                _cam.fieldOfView = target;
                _zoomVelocity = 0f;
                return;
            }

            _cam.fieldOfView = Mathf.SmoothDamp(
                _cam.fieldOfView, target, ref _zoomVelocity, Mathf.Max(0.0001f, zoomSmoothTime));
        }

        private bool AnyWeaponEquipped()
        {
            if (Time.unscaledTime >= _nextWeaponScan)
            {
                _nextWeaponScan = Time.unscaledTime + Mathf.Max(0.1f, weaponScanInterval);

                _weapons = FindObjectsByType<WeaponController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                _shotguns = FindObjectsByType<ShotgunToggle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                _glocks = FindObjectsByType<GlockToggle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }

            if (_weapons != null)
                foreach (WeaponController w in _weapons)
                    if (w != null && w.IsEquipped) return true;

            if (_shotguns != null)
                foreach (ShotgunToggle s in _shotguns)
                    if (s != null && s.IsEquipped) return true;

            if (_glocks != null)
                foreach (GlockToggle g in _glocks)
                    if (g != null && g.IsEquipped) return true;

            return false;
        }

        public void AddRecoil(float pitchDegrees, float yawDegrees)
        {
            _targetPitch = Mathf.Clamp(_targetPitch - pitchDegrees, -lookXLimit, lookXLimit);
            _targetYaw += yawDegrees;
            float yawWrap = Mathf.Floor((_targetYaw + 180f) / 360f) * 360f;
            if (yawWrap != 0f) _targetYaw -= yawWrap;
        }
    }
}