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
        [SerializeField, Range(0.01f, 0.5f)] private float rotationLag = 0.12f;

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
        private float _pitchVelocity;
        private float _yawVelocity;
        private float _bodyYawVelocity;
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

            Vector2 mouse = Mouse.current.delta.ReadValue();
            float inputYaw = mouse.x * lookSpeed;

            _targetPitch = Mathf.Clamp(_targetPitch - mouse.y * lookSpeed, -lookXLimit, lookXLimit);
            _targetYaw += inputYaw;

            _pitch = Mathf.SmoothDampAngle(_pitch, _targetPitch, ref _pitchVelocity, rotationLag);
            _yaw = Mathf.SmoothDampAngle(_yaw, _targetYaw, ref _yawVelocity, rotationLag);

            float targetTilt = Mathf.Clamp(inputYaw * tiltResponse, -1f, 1f) * maxTiltAngle;

            float riseBlend = 1f - Mathf.Exp(-tiltSmoothing * Time.deltaTime);
            float decayBlend = 1f - Mathf.Exp(-tiltRecovery * Time.deltaTime);
            _tilt = Mathf.Abs(targetTilt) > Mathf.Abs(_tilt)
                ? Mathf.Lerp(_tilt, targetTilt, riseBlend)
                : Mathf.Lerp(_tilt, targetTilt, decayBlend);

            _cam.transform.localRotation = Quaternion.Euler(_pitch, 0f, _tilt);

            float bodyYaw = Mathf.SmoothDampAngle(_player.transform.eulerAngles.y, _targetYaw, ref _bodyYawVelocity, rotationLag);
            _player.transform.localRotation = Quaternion.Euler(0f, bodyYaw, 0f);
        }
    }
}