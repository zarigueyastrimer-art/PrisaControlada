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
        [SerializeField] private AudioClip[] footstepSounds;

        [Header("Footstep Speed Thresholds")]
        [SerializeField, Min(0f)] private float idleMaxSpeed = 100f;
        [SerializeField, Min(0f)] private float crouchMaxSpeed = 250f;
        [SerializeField, Min(0f)] private float walkMaxSpeed = 500f;

        [Header("Footstep Volumes & Intervals")]
        [SerializeField, Range(0f, 1f)] private float crouchFootstepVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] private float walkFootstepVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float runFootstepVolume = 0.8f;
        [SerializeField] private float crouchStepInterval = 0.6f;
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

        [Header("Stamina")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float staminaDrainRate = 25f;   // por segundo corriendo
        [SerializeField] private float staminaRegenRate = 15f;   // por segundo recuperando
        [SerializeField] private float staminaRegenDelay = 1.5f; // segundos sin correr antes de regenerar

        public event System.Action<float> OnNoiseMade;
        public event System.Action<float, float> OnStaminaChanged; // current, max
        public event System.Action<bool> OnStaminaExhausted; // true cuando se agota, false cuando recupera

        private float _currentStamina;
        private float _lastSprintTime = -1f; // -1 indica que no se ha corrido nunca
        private bool _isStaminaExhausted;

        [Header("References")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private WeaponController weapon;
        [SerializeField, Range(0.1f, 1f)] private float weaponBusySpeedFactor = 0.5f;

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
        public float WalkSpeed => walkSpeed;
        public Vector3 MoveVector => _moveDirection;
        public InputActionAsset InputActions => inputActions;
        public bool ControlEnabled => _controlEnabled;

        public float CurrentStamina => _currentStamina;
        public bool IsStaminaExhausted => _isStaminaExhausted;
        public float MaxStamina => maxStamina;

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

            if (weapon == null)
                weapon = GetComponentInChildren<WeaponController>();

            if (footstepAudioSource == null)
                footstepAudioSource = GetComponent<AudioSource>();
            if (footstepAudioSource == null)
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
            footstepAudioSource.playOnAwake = false;
            footstepAudioSource.loop = false;

            // Inicializar stamina
            _currentStamina = maxStamina;
            _isStaminaExhausted = false;
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
            // Mientras apunta o recarga no se puede correr y se camina mas lento (~mitad).
            bool weaponBusy = weapon != null && (weapon.IsAiming || weapon.IsReloading);
            
            // Determinar si se puede correr (tener stamina no agotada)
            bool canSprint = !_isCrouching && !_isStaminaExhausted;
            _isSprinting = canSprint && !weaponBusy && _sprintAction != null && _sprintAction.IsPressed();
            
            float baseSpeed = _isCrouching ? crouchSpeed : (_isSprinting ? runSpeed : walkSpeed);
            if (weaponBusy) baseSpeed *= weaponBusySpeedFactor;
            float speed = _isCrouching ? baseSpeed : baseSpeed * GetHealthSpeedFactor();

            Vector3 forward = transform.TransformDirection(Vector3.forward);
            Vector3 right = transform.TransformDirection(Vector3.right);
            float curSpeedX = speed * input.y;
            float curSpeedY = speed * input.x;
            _moveDirection = (forward * curSpeedX) + (right * curSpeedY);

            if (_jumpAction != null && _jumpAction.IsPressed() && !_isCrouching && _controller.isGrounded)
                _moveDirection.y = jumpPower;
            else
            {
                // Aplicar gravedad siempre (incluso en suelo) para evitar que el jugador
                // se quede "flotando" encima de objetos pequeños.
                // Cuando está en el suelo, usamos una gravedad más fuerte para pegarlo al suelo.
                float currentGravity = _controller.isGrounded ? gravity * 3f : gravity;
                _moveDirection.y -= currentGravity * Time.deltaTime;
            }

            // Manejar stamina (solo si hay movimiento horizontal)
            HandleStamina();

            _controller.Move(_moveDirection * Time.deltaTime);
        }

        private void HandleStamina()
        {
            bool isMovingHorizontally = _moveDirection.magnitude > 0.1f;
            bool isSprintingNow = _isSprinting && isMovingHorizontally && !_isCrouching;
            
            // Actualizar el tiempo del último sprint
            if (isSprintingNow)
            {
                _lastSprintTime = Time.time;
            }

            if (isSprintingNow)
            {
                // Agotar stamina mientras corremos
                _currentStamina -= staminaDrainRate * Time.deltaTime;
                
                if (_currentStamina <= 0f)
                {
                    _currentStamina = 0f;
                    _isStaminaExhausted = true;
                }
            }
            else
            {
                // Regenerar stamina si no está corriendo y no está agotada
                float timeSinceLastSprint = Time.time - _lastSprintTime;
                
                if (timeSinceLastSprint >= staminaRegenDelay)
                {
                    _currentStamina += staminaRegenRate * Time.deltaTime;
                    
                    if (_currentStamina >= maxStamina)
                    {
                        _currentStamina = maxStamina;
                        _isStaminaExhausted = false;
                    }
                }
            }

            // Notificar cambio de stamina
            OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
            
            // Notificar estado de stamina agotada solo cuando cambie
            if (_currentStamina <= 0f && !_isStaminaExhausted)
            {
                _isStaminaExhausted = true;
                OnStaminaExhausted?.Invoke(true);
            }
            else if (_currentStamina >= maxStamina && _isStaminaExhausted)
            {
                _isStaminaExhausted = false;
                OnStaminaExhausted?.Invoke(false);
            }
        }

        private float GetHealthSpeedFactor()
        {
            if (health == null) return 1f;

            float hp01 = Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);
            float range = Mathf.Max(0.01f, 1f - Mathf.Clamp01((float)healthThreshold / health.MaxHealth));
            float t = Mathf.Clamp01((1f - hp01) / range);
            return Mathf.Lerp(1f, minSpeedFactor, Mathf.Pow(t, healthSpeedExponent));
        }

        private enum FootstepState { Idle, Crouch, Walk, Run }

        private FootstepState GetFootstepState(float speed)
        {
            if (speed < idleMaxSpeed) return FootstepState.Idle;
            if (speed < crouchMaxSpeed) return FootstepState.Crouch;
            if (speed < walkMaxSpeed) return FootstepState.Walk;
            return FootstepState.Run;
        }

        private void HandleFootsteps()
        {
            if (!_controller.isGrounded) return;

            Vector3 horizontalVel = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z);
            float speed = horizontalVel.magnitude;

            FootstepState state = GetFootstepState(speed);
            if (state == FootstepState.Idle) return;

            float interval = state switch
            {
                FootstepState.Crouch => crouchStepInterval,
                FootstepState.Walk => walkStepInterval,
                FootstepState.Run => runStepInterval,
                _ => walkStepInterval
            };

            if (Time.time >= _nextStepTime)
            {
                PlayFootstepSound(state);
                OnNoiseMade?.Invoke(state == FootstepState.Crouch ? crouchNoise : (state == FootstepState.Run ? runNoise : walkNoise));
                _nextStepTime = Time.time + interval;
            }
        }

        private void PlayFootstepSound(FootstepState state)
        {
            if (footstepAudioSource == null) return;

            if (footstepSounds == null || footstepSounds.Length == 0) return;

            var clip = footstepSounds[Random.Range(0, footstepSounds.Length)];
            float volume = state switch
            {
                FootstepState.Crouch => crouchFootstepVolume,
                FootstepState.Walk => walkFootstepVolume,
                FootstepState.Run => runFootstepVolume,
                _ => walkFootstepVolume
            };
            footstepAudioSource.PlayOneShot(clip, volume);
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
