using PrisaControlada.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.Player
{
    public class WeaponController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator weaponAnimator;
        [SerializeField] private Animator weaponArmAnimator;
        [SerializeField] private Transform weaponRoot;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private InputActionAsset inputActions;

        [Header("Weapon Stats")]
        [SerializeField] private float damage = 35f;
        [SerializeField] private float fireRate = 1.5f;
        [SerializeField] private float range = 100f;
        [SerializeField] private int maxAmmo = 6;
        [SerializeField] private float reloadTime = 1.8f;
        [SerializeField] private LayerMask shootMask = ~0;

        [Header("Equip / Unequip")]
        [SerializeField] private bool startEquipped = false;

        [Header("Aim")]
        [SerializeField] private float normalFov = 70f;
        [SerializeField] private float aimFov = 45f;
        [SerializeField] private float fovSmoothTime = 0.15f;

        private static readonly int AimParam = Animator.StringToHash("Aim");
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int ShootParam = Animator.StringToHash("Shoot");
        private static readonly int ReloadingParam = Animator.StringToHash("Reloading");

        private InputAction _attackAction;
        private InputAction _aimAction;
        private InputAction _reloadAction;
        private InputAction _weaponToggleAction;

        private bool _equipped;
        private bool _isAiming;
        private bool _isReloading;
        private int _ammo;
        private float _nextFireTime;
        private float _reloadEndTime;
        private float _fovVelocity;

        public bool IsEquipped => _equipped;
        public bool IsAiming => _isAiming;
        public int Ammo => _ammo;
        public int MaxAmmo => maxAmmo;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();

            if (inputActions != null)
            {
                _attackAction = inputActions.FindAction("Attack");
                _aimAction = inputActions.FindAction("Aim");
                _reloadAction = inputActions.FindAction("Reload");
                _weaponToggleAction = inputActions.FindAction("Previous");
            }
            Debug.Log($"[Weapon] Awake: Attack={_attackAction != null} Aim={_aimAction != null} Reload={_reloadAction != null} Toggle={_weaponToggleAction != null} startEquipped={startEquipped}");
        }

        private void OnEnable()
        {
            _attackAction?.Enable();
            _aimAction?.Enable();
            _reloadAction?.Enable();
            _weaponToggleAction?.Enable();
        }

        private void OnDisable()
        {
            _attackAction?.Disable();
            _aimAction?.Disable();
            _reloadAction?.Disable();
            _weaponToggleAction?.Disable();
        }

        private void Start()
        {
            _ammo = maxAmmo;
            if (startEquipped)
                SetEquipped(true);
            else
                SetEquipped(false);
        }

        private void Update()
        {
            bool canControl = player == null || player.ControlEnabled;

            if (canControl && _weaponToggleAction != null && _weaponToggleAction.WasPressedThisFrame())
                ToggleEquip();

            if (!_equipped) return;

            ResetTriggerBoth(ShootParam);

            UpdateAim(canControl);
            UpdateShoot(canControl);
            UpdateReload(canControl);
            UpdateAnimatorSpeed();
            UpdateFov();
        }

        private void ToggleEquip()
        {
            if (_isReloading) return;
            SetEquipped(!_equipped);
            Debug.Log($"[Weapon] ToggleEquip -> equipped={_equipped} gunAnimatorNull={weaponAnimator == null} armAnimatorNull={weaponArmAnimator == null}");
        }

        private void SetEquipped(bool value)
        {
            _equipped = value;

            if (weaponRoot != null) weaponRoot.gameObject.SetActive(value);
            _isAiming = false;
            SetBoolBoth(AimParam, false);

            if (value)
            {
                PlayClip(weaponAnimator, "Idle");
                PlayClip(weaponArmAnimator, "Idle");
            }
            else
            {
                PlayClip(weaponAnimator, "Empty");
                PlayClip(weaponArmAnimator, "Empty");
            }
        }

        private void SetBoolBoth(int param, bool value)
        {
            if (weaponAnimator != null) weaponAnimator.SetBool(param, value);
            if (weaponArmAnimator != null) weaponArmAnimator.SetBool(param, value);
        }

        private void SetFloatBoth(int param, float value, float dampTime, float deltaTime)
        {
            if (weaponAnimator != null) weaponAnimator.SetFloat(param, value, dampTime, deltaTime);
            if (weaponArmAnimator != null) weaponArmAnimator.SetFloat(param, value, dampTime, deltaTime);
        }

        private void SetTriggerBoth(int param)
        {
            if (weaponAnimator != null) weaponAnimator.SetTrigger(param);
            if (weaponArmAnimator != null) weaponArmAnimator.SetTrigger(param);
        }

        private void ResetTriggerBoth(int param)
        {
            if (weaponAnimator != null) weaponAnimator.ResetTrigger(param);
            if (weaponArmAnimator != null) weaponArmAnimator.ResetTrigger(param);
        }

        private static void PlayClip(Animator anim, string state, float normalizedTime = 0f, float speed = 1f)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;
            anim.speed = speed;
            anim.Play(state, 0, normalizedTime);
        }

        private void UpdateAim(bool canControl)
        {
            bool wantAim = canControl && _aimAction != null && _aimAction.IsPressed() && !_isReloading;
            if (_isAiming != wantAim)
            {
                _isAiming = wantAim;
                SetBoolBoth(AimParam, _isAiming);
            }
        }

        private void UpdateShoot(bool canControl)
        {
            if (!canControl || _isReloading) return;
            if (_attackAction == null || !_attackAction.WasPressedThisFrame()) return;
            if (_ammo <= 0 || Time.time < _nextFireTime) return;

            Fire();
        }

        private void Fire()
        {
            _ammo--;
            _nextFireTime = Time.time + 1f / fireRate;

            SetTriggerBoth(ShootParam);

            if (viewCamera == null) return;

            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, range, shootMask, QueryTriggerInteraction.Ignore))
            {
                var enemy = hit.collider.GetComponentInParent<EnemyBase>();
                if (enemy != null)
                    enemy.TakeDamage(Mathf.RoundToInt(damage));
            }
        }

        private void UpdateReload(bool canControl)
        {
            if (_reloadAction == null || !canControl) return;

            if (!_isReloading && _reloadAction.WasPressedThisFrame())
            {
                _isReloading = true;
                _reloadEndTime = Time.time + reloadTime;
                SetBoolBoth(AimParam, false);
                SetBoolBoth(ReloadingParam, true);
                _isAiming = false;
            }
            else if (_isReloading && Time.time >= _reloadEndTime)
            {
                _ammo = maxAmmo;
                _isReloading = false;
                SetBoolBoth(ReloadingParam, false);
            }
        }

        private void UpdateAnimatorSpeed()
        {
            if (weaponAnimator == null && weaponArmAnimator == null) return;

            float move01 = player != null
                ? Mathf.Clamp01(player.HorizontalSpeed / Mathf.Max(player.RunSpeed, 0.01f))
                : 0f;

            float speed = move01 <= 0.01f ? 0f : (move01 >= 0.99f ? 2f : 1f);
            SetFloatBoth(SpeedParam, speed, 0.2f, Time.deltaTime);
        }

        private void UpdateFov()
        {
            if (viewCamera == null) return;
            float target = _isAiming ? aimFov : normalFov;
            float current = viewCamera.fieldOfView;
            float next = Mathf.SmoothDamp(current, target, ref _fovVelocity, fovSmoothTime);
            if (Mathf.Abs(next - current) > 0.01f)
                viewCamera.fieldOfView = next;
        }
    }
}