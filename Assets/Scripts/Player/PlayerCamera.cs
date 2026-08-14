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

        [Header("Tilt Settings")]
        [SerializeField] private float maxTiltAngle = 4f;
        [SerializeField, Range(0.1f, 5f)] private float tiltResponse = 2.5f;
        [SerializeField, Range(0.1f, 20f)] private float tiltSmoothing = 12f;
        [SerializeField, Range(0.1f, 20f)] private float tiltRecovery = 3f;

        private Camera _cam;
        private PlayerController _player;

        private float _targetPitch;
        private float _targetYaw;
        private float _pitch;
        private float _yaw;
        private float _tilt;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _player = GetComponentInParent<PlayerController>();
        }

        private void Start()
        {
            if (_player != null)
                _targetYaw = _yaw = _player.transform.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (_player == null || Mouse.current == null) return;

            Vector2 raw = Mouse.current.delta.ReadValue();
            Vector2 mouse = raw;

            _targetPitch = Mathf.Clamp(_targetPitch - mouse.y * lookSpeed, -lookXLimit, lookXLimit);
            _targetYaw += mouse.x * lookSpeed;

            // Keep the accumulated yaw bounded so it never drifts to huge values.
            float yawWrap = Mathf.Floor((_targetYaw + 180f) / 360f) * 360f;
            if (yawWrap != 0f)
                _targetYaw -= yawWrap;

            // First-order exponential approach. No spring, so it can never overshoot,
            // oscillate, or produce NaN no matter how fast the mouse moves.
            float blend = rotationLag <= 0f
                ? 1f
                : 1f - Mathf.Exp(-(1f / rotationLag) * Time.deltaTime);

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
        }
    }
}