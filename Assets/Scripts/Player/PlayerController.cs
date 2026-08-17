using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.Player
{
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(100)]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 2f;
        [SerializeField] private float runSpeed = 4f;
        [SerializeField] private float jumpPower = 7f;
        [SerializeField] private float gravity = 10f;

        [Header("Crouch")]
        [SerializeField] private float crouchHeight = 1f;
        [SerializeField] private float crouchSpeed = 1.5f;
        [SerializeField] private float standUpCheckInterval = 0.1f;
        [SerializeField] private LayerMask standUpObstacleMask = -1;

        [Header("Crouch Smoothing")]
        [SerializeField] private float crouchSmoothTime = 0.12f;
        [SerializeField] private float crouchSwayForward = 0.1f;
        [SerializeField] private float crouchSwayPitch = 4f;

        [Header("Footstep Settings")]
        [SerializeField] private AudioSource footstepAudioSource;
        [SerializeField] private AudioClip[] walkFootstepSounds;
        [SerializeField] private AudioClip[] runFootstepSounds;
        [SerializeField] private float walkStepInterval = 0.5f;
        [SerializeField] private float runStepInterval = 0.3f;

        [Header("Noise")]
        [SerializeField] private float walkNoise = 0.35f;
        [SerializeField] private float runNoise = 1f;
        [SerializeField] private float crouchNoise = 0.12f;

        [Header("Health Movement")]
        [SerializeField] private PlayerHealth health;
        [SerializeField, Range(0f, 1f)] private float minSpeedFactor = 0.45f;
        [SerializeField, Range(1f, 4f)] private float healthSpeedExponent = 2f;
        [SerializeField, Min(0f)] private int healthThreshold = 25;

        public event System.Action<float> OnNoiseMade;

        [Header("References")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private InputActionAsset inputActions;

        private CharacterController _controller;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _crouchAction;

        private Vector3 _moveDirection = Vector3.zero;
        private bool _isCrouching;
        private bool _isSprinting;
        private float _standingHeight;
        private float _standingEyeHeight;
        private float _nextStandUpCheck;
        private float _nextStepTime;
        private float _crouchBlend;
        private float _crouchBlendVelocity;
        private float _crouchSwayPitch;
        private bool _controlEnabled = true;

        public float HorizontalSpeed => _moveDirection.magnitude;
        public bool IsSprinting => _isSprinting;
        public bool IsCrouching => _isCrouching;
        public bool IsGrounded => _controller.isGrounded;
        public float RunSpeed => runSpeed;
        public Vector3 MoveVector => _moveDirection;
        public InputActionAsset InputActions => inputActions;
        public bool ControlEnabled => _controlEnabled;

        public void SetControlEnabled(bool enabled)
        {
            _controlEnabled = enabled;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            // The CharacterController is the player's only collider. Any extra collider
            // on the same object (e.g. a CapsuleCollider overlapping the controller)
            // makes isGrounded always return true, which disables gravity (floating/flying).
            Collider[] ownColliders = GetComponents<Collider>();
            foreach (Collider col in ownColliders)
            {
                if (col != _controller)
                {
                    col.enabled = false;
                    Debug.LogWarning("[PlayerController] Collider extra en el jugador deshabilitado: " + col.GetType().Name + ". Solo debe haber un CharacterController.", this);
                }
            }

            if (health == null)
                health = GetComponent<PlayerHealth>();

            if (cameraPivot == null)
            {
                Transform found = transform.Find("CameraPivot");
                if (found != null)
                    cameraPivot = found;
            }

            if (inputActions != null)
            {
                _moveAction = inputActions.FindAction("Move");
                _jumpAction = inputActions.FindAction("Jump");
                _sprintAction = inputActions.FindAction("Sprint");
                _crouchAction = inputActions.FindAction("Crouch");
            }
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _jumpAction?.Enable();
            _sprintAction?.Enable();
            _crouchAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _jumpAction?.Disable();
            _sprintAction?.Disable();
            _crouchAction?.Disable();
        }

        private void Start()
        {
            _standingHeight = _controller.height;
            _standingEyeHeight = cameraPivot != null ? cameraPivot.localPosition.y : 1.6f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            HandleDebugKeys();
            HandleCrouch();
            HandleMovement();
            HandleFootsteps();
        }

        private void HandleDebugKeys()
        {
            if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            {
                PlayerHealth health = GetComponent<PlayerHealth>();
                if (health != null) health.TakeDamage(25);
            }

            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                PlayerHealth health = GetComponent<PlayerHealth>();
                if (health != null) health.Heal(25);
            }
        }

        private void HandleMovement()
        {
            if (!_controlEnabled)
            {
                _moveDirection = Vector3.zero;
                return;
            }

            Vector2 input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            _isSprinting = !_isCrouching && _sprintAction != null && _sprintAction.IsPressed();
            float baseSpeed = _isCrouching ? crouchSpeed : (_isSprinting ? runSpeed : walkSpeed);
            float speed = _isCrouching ? baseSpeed : baseSpeed * GetHealthSpeedFactor();

            Vector3 forward = transform.TransformDirection(Vector3.forward);
            Vector3 right = transform.TransformDirection(Vector3.right);
            float curSpeedX = speed * input.y;
            float curSpeedY = speed * input.x;
            _moveDirection = (forward * curSpeedX) + (right * curSpeedY);

            if (_jumpAction != null && _jumpAction.IsPressed() && !_isCrouching && _controller.isGrounded)
                _moveDirection.y = jumpPower;
            else if (!_controller.isGrounded)
                _moveDirection.y -= gravity * Time.deltaTime;

            _controller.Move(_moveDirection * Time.deltaTime);
        }

        private float GetHealthSpeedFactor()
        {
            if (health == null) return 1f;

            float hp01 = Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);
            float range = Mathf.Max(0.01f, 1f - Mathf.Clamp01((float)healthThreshold / health.MaxHealth));
            float t = Mathf.Clamp01((1f - hp01) / range);
            return Mathf.Lerp(1f, minSpeedFactor, Mathf.Pow(t, healthSpeedExponent));
        }

        private void HandleFootsteps()
        {
            if (!_controller.isGrounded || _moveDirection.magnitude <= 0f) return;

            float stepInterval = _isSprinting ? runStepInterval : walkStepInterval;

            if (Time.time >= _nextStepTime)
            {
                PlayFootstepSound(_isSprinting);
                OnNoiseMade?.Invoke(_isCrouching ? crouchNoise : (_isSprinting ? runNoise : walkNoise));
                _nextStepTime = Time.time + stepInterval;
            }
        }

        private void PlayFootstepSound(bool isRunning)
        {
            if (footstepAudioSource == null) return;

            AudioClip[] selectedFootstepSounds = isRunning ? runFootstepSounds : walkFootstepSounds;
            if (selectedFootstepSounds == null || selectedFootstepSounds.Length == 0) return;

            footstepAudioSource.clip = selectedFootstepSounds[Random.Range(0, selectedFootstepSounds.Length)];
            footstepAudioSource.Play();
        }

        private void HandleCrouch()
        {
            if (!_controlEnabled) return;

            bool wantCrouch = _crouchAction != null && _crouchAction.IsPressed();

            if (wantCrouch)
                _isCrouching = true;
            else if (_isCrouching && Time.time >= _nextStandUpCheck)
            {
                _nextStandUpCheck = Time.time + standUpCheckInterval;
                if (CanStandUp())
                    _isCrouching = false;
            }

            float target = _isCrouching ? 1f : 0f;
            _crouchBlend = Mathf.SmoothDamp(_crouchBlend, target, ref _crouchBlendVelocity, crouchSmoothTime);
            _crouchBlend = Mathf.Clamp01(_crouchBlend);

            float height = Mathf.Lerp(_standingHeight, crouchHeight, _crouchBlend);
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);

            if (cameraPivot != null)
            {
                float p = _crouchBlend;

                float eye = Mathf.Lerp(_standingEyeHeight, crouchHeight * 0.8f, p);

                // "S" sway while descending: swings forward near the top and back near the bottom.
                float envelope = Mathf.Sin(p * Mathf.PI * 2f) * 0.5f;
                float forwardOffset = crouchSwayForward * envelope;
                _crouchSwayPitch = -crouchSwayPitch * envelope;

                cameraPivot.localPosition = new Vector3(cameraPivot.localPosition.x, eye, forwardOffset);
            }
        }

        private void LateUpdate()
        {
            if (cameraPivot == null || Mathf.Abs(_crouchSwayPitch) < 0.01f) return;
            cameraPivot.localRotation *= Quaternion.Euler(_crouchSwayPitch, 0f, 0f);
            _crouchSwayPitch = 0f;
        }

        private bool CanStandUp()
        {
            Vector3 bottom = transform.position + Vector3.up * (_controller.radius + _controller.skinWidth);
            Vector3 top = transform.position + Vector3.up * (_standingHeight - _controller.radius - _controller.skinWidth);

            Collider[] hits = Physics.OverlapCapsule(bottom, top, _controller.radius, standUpObstacleMask, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                if (hit != _controller && !hit.transform.IsChildOf(transform))
                    return false;
            }

            return true;
        }
    }
}
