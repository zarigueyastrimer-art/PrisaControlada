using UnityEngine;
using UnityEngine.InputSystem;
using PrisaControlada.Enemies;

namespace PrisaControlada.Player
{
    public class ShotgunToggle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform weaponRoot;
        [SerializeField] private Animator armAnimator;
        [SerializeField] private Animator gunAnimator;
        [SerializeField] private InputActionAsset inputActions;

        [Header("Reload Sequence")]
        [SerializeField] private float holdLastFrameDuration = 1f;
        [SerializeField] private float reloadFreezeNormalized = 0.98f;
        [SerializeField] private Vector3 reloadPosOffset;
        [SerializeField] private Vector3 reloadRotOffset;

        [Header("Aim Position (ShotgunArm transform)")]
        [SerializeField] private Vector3 aimLocalPos;
        [SerializeField] private Vector3 aimLocalRot;
        [SerializeField] private float aimLerpDuration = 0.3f;
        [SerializeField] private bool holdAimPose;

        [Header("Aim Animation")]
        [SerializeField] private string aimAnimationName = "IdleAim";
        [SerializeField] private bool loopAimAnimation;

        [Header("Aim Zoom (FOV)")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Camera weaponCamera;
        [SerializeField] private float normalFov = 70f;
        [SerializeField] private float aimFov = 45f;
        [SerializeField] private float fovSmoothTime = 0.15f;

        [Header("Disparo (perdigones)")]
        [SerializeField] private int pellets = 8;
        [SerializeField] private float damagePerPellet = 14f;
        [SerializeField] private float spread = 4f;
        [SerializeField] private float aimSpreadMultiplier = 0.4f;
        [SerializeField] private float range = 100f;
        [SerializeField] private float fireRate = 1.2f;
        [SerializeField] private LayerMask shootMask = ~0;
        [SerializeField] private WeaponVFX weaponVFX;
        [SerializeField] private bool showLineTrace = true;

        [Header("Disparo - Aim Trace (estilo revolver)")]
        [SerializeField] private bool aimUseMuzzleForward = true;
        [SerializeField] private Vector3 aimShotOriginOffset;
        [SerializeField] private Vector3 aimShotDirectionOffset;
        [SerializeField] private Color lineTraceColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("Municion (escopeta: 1 cartucho en el canion)")]
        [SerializeField] private int startReserveShells = 4;

        private InputAction _toggleAction;
        private InputAction _shootAction;
        private InputAction _reloadAction;
        private InputAction _aimAction;
        private bool _equipped;
        private bool _shooting;
        private float _shootStartTime;
        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;
        private Vector3 _aimBaseLocalPos;
        private Quaternion _aimBaseLocalRot;
        private bool _aiming;

        private enum ReloadPhase { None, Reloading, Holding, Getting }
        private ReloadPhase _reloadPhase = ReloadPhase.None;
        private float _phaseStartTime;
        private float _fovVelocity;
        private float _nextFireTime;
        private int _shellsLoaded = 1;
        private int _reserveShells;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int ReloadingHash = Animator.StringToHash("Reloading");
        private static readonly int AimHash = Animator.StringToHash("Aim");

        private void Awake()
        {
            if (inputActions != null)
            {
                _toggleAction = inputActions.FindAction("Next");
                _shootAction = inputActions.FindAction("Click");
                _reloadAction = inputActions.FindAction("Reload");
                _aimAction = inputActions.FindAction("Aim");
            }
        }

        private void OnEnable()
        {
            _toggleAction?.Enable();
            _shootAction?.Enable();
            _reloadAction?.Enable();
            _aimAction?.Enable();
        }

        private void OnDisable()
        {
            _toggleAction?.Disable();
            _shootAction?.Disable();
            _reloadAction?.Disable();
            _aimAction?.Disable();
        }

        private void Start()
        {
            if (weaponRoot != null)
            {
                weaponRoot.gameObject.SetActive(false);
                _baseLocalPos = weaponRoot.localPosition;
                _baseLocalRot = weaponRoot.localRotation;
            }

            if (armAnimator != null)
            {
                _aimBaseLocalPos = armAnimator.transform.localPosition;
                _aimBaseLocalRot = armAnimator.transform.localRotation;
            }

            _reserveShells = startReserveShells;
            _shellsLoaded = 1;
        }

        private void Update()
        {
            if (weaponRoot == null) return;

            if (_toggleAction != null && _toggleAction.WasPressedThisFrame())
            {
                _equipped = !_equipped;
                weaponRoot.gameObject.SetActive(_equipped);
                _shooting = false;
                _reloadPhase = ReloadPhase.None;
                SetReloading(armAnimator, false);
                SetReloading(gunAnimator, false);
                SetAiming(armAnimator, false);
                SetAiming(gunAnimator, false);
                if (_equipped) PlayAnim("Get");
                return;
            }

            if (!_equipped) return;

            bool aimHeld = _aimAction != null && _aimAction.IsPressed();
            if (_reloadPhase != ReloadPhase.None)
            {
                SetAiming(armAnimator, false);
                SetAiming(gunAnimator, false);
            }
            else
            {
                bool aimActive = aimHeld || holdAimPose;
                SetAiming(armAnimator, aimActive);
                SetAiming(gunAnimator, aimActive);
            }

            if (_shooting || _reloadPhase != ReloadPhase.None) return;

            if (_reloadAction != null && _reloadAction.WasPressedThisFrame())
            {
                if (_reloadPhase == ReloadPhase.None && _shellsLoaded < 1 && _reserveShells > 0)
                {
                    _reloadPhase = ReloadPhase.Reloading;
                    _phaseStartTime = Time.time;
                    SetReloading(armAnimator, true);
                    SetReloading(gunAnimator, false);
                    if (armAnimator != null && armAnimator.runtimeAnimatorController != null)
                        armAnimator.Play("Reload", 0, 0f);
                    Debug.Log($"<color=#77ccff>[Shotgun] Recarga iniciada. reserva={_reserveShells}</color>");
                }
                else if (_shellsLoaded >= 1)
                {
                    Debug.Log("<color=#ff7777>[Shotgun] Ya tenes 1 cartucho en el canion.</color>");
                }
                else if (_reserveShells <= 0)
                {
                    Debug.Log("<color=#ff7777>[Shotgun] Sin cartuchos en reserva: busca una caja de balas.</color>");
                }
                return;
            }

            if (_shootAction != null && _shootAction.WasPressedThisFrame())
            {
                if (Time.time >= _nextFireTime && _reloadPhase == ReloadPhase.None)
                {
                    if (_shellsLoaded <= 0)
                    {
                        Debug.Log("<color=#ff7777>[Shotgun] Vacia: apreta R para recargar.</color>");
                        return;
                    }
                    _shooting = true;
                    _shootStartTime = Time.time;
                    _nextFireTime = Time.time + (fireRate > 0f ? 1f / fireRate : 0.2f);
                    PlayAnim("Shoot");
                    FireShotgun();
                    _shellsLoaded--;
                    Debug.Log($"<color=#ffcc66>[Shotgun] Disparo. canion={_shellsLoaded} reserva={_reserveShells}</color>");
                }
            }
        }

        private void LateUpdate()
        {
            if (!_equipped)
            {
                _aiming = false;
                UpdateAimFov();
                return;
            }

            UpdateAimTransform();

            if (_reloadPhase != ReloadPhase.None)
            {
                HandleReloadSequence();
                weaponRoot.localPosition = _baseLocalPos + reloadPosOffset;
                weaponRoot.localRotation = _baseLocalRot * Quaternion.Euler(reloadRotOffset);
                return;
            }

            if (_shooting)
            {
                if (armAnimator != null)
                {
                    AnimatorStateInfo state = armAnimator.GetCurrentAnimatorStateInfo(0);
                    if (state.IsName("Shoot") && state.normalizedTime >= 1f)
                        _shooting = false;
                    else if (!state.IsName("Shoot") && Time.time - _shootStartTime > 0.2f)
                        _shooting = false;
                }
                else { _shooting = false; }
                if (_shooting) return;
            }

            weaponRoot.localPosition = _baseLocalPos;
            weaponRoot.localRotation = _baseLocalRot;

            PlayerController player = GetComponent<PlayerController>();
            float rawSpeed = player != null ? player.HorizontalSpeed : 0f;
            float walkSpeed = player != null ? player.WalkSpeed : 4f;
            float normalizedSpeed = walkSpeed > 0f ? rawSpeed / walkSpeed : 0f;

            if (armAnimator != null && armAnimator.runtimeAnimatorController != null)
                armAnimator.SetFloat(SpeedHash, normalizedSpeed);
            if (gunAnimator != null && gunAnimator.runtimeAnimatorController != null)
                gunAnimator.SetFloat(SpeedHash, normalizedSpeed);
        }

        private void HandleReloadSequence()
        {
            switch (_reloadPhase)
            {
                case ReloadPhase.Reloading:
                    if (armAnimator != null)
                    {
                        if (!armAnimator.IsInTransition(0))
                        {
                            AnimatorStateInfo s = armAnimator.GetCurrentAnimatorStateInfo(0);
                            if (s.IsName("Reload") && s.normalizedTime >= reloadFreezeNormalized)
                            {
                                _reloadPhase = ReloadPhase.Holding;
                                _phaseStartTime = Time.time;
                                armAnimator.Play("Reload", 0, reloadFreezeNormalized);
                                armAnimator.speed = 0f;
                            }
                        }
                    }
                    else
                    {
                        _reloadPhase = ReloadPhase.Holding;
                        _phaseStartTime = Time.time;
                    }
                    break;

                case ReloadPhase.Holding:
                    if (armAnimator != null) armFreeze();
                    if (Time.time - _phaseStartTime >= holdLastFrameDuration)
                    {
                        _reloadPhase = ReloadPhase.Getting;
                        _phaseStartTime = Time.time;
                        if (armAnimator != null) armAnimator.speed = 1f;
                        SetReloading(armAnimator, false);
                        PlayAnim("Get");
                    }
                    break;

                case ReloadPhase.Getting:
                    if (armAnimator != null) armAnimator.speed = 1f;
                    if (armAnimator != null && !armAnimator.IsInTransition(0))
                    {
                        AnimatorStateInfo s = armAnimator.GetCurrentAnimatorStateInfo(0);
                        if (!s.IsName("Get"))
                        {
                            _reloadPhase = ReloadPhase.None;
                            _shellsLoaded = 1;
                            _reserveShells = Mathf.Max(0, _reserveShells - 1);
                            Debug.Log($"<color=#77ff77>[Shotgun] Recarga completa. canion={_shellsLoaded} reserva={_reserveShells}</color>");
                        }
                    }
                    else if (armAnimator == null)
                    {
                        _reloadPhase = ReloadPhase.None;
                        _shellsLoaded = 1;
                        _reserveShells = Mathf.Max(0, _reserveShells - 1);
                    }
                    break;
            }
        }

        private void armFreeze()
        {
            if (armAnimator != null) armAnimator.speed = 0f;
        }

        private void PlayAnim(string state)
        {
            if (armAnimator != null && armAnimator.runtimeAnimatorController != null)
                armAnimator.Play(state, 0, 0f);
            if (gunAnimator != null && gunAnimator.runtimeAnimatorController != null)
                gunAnimator.Play(state, 0, 0f);
        }

        private void SetReloading(Animator a, bool value)
        {
            if (a != null && a.runtimeAnimatorController != null)
                a.SetBool(ReloadingHash, value);
        }

        private void SetAiming(Animator a, bool value)
        {
            if (a != null && a.runtimeAnimatorController != null)
                a.SetBool(AimHash, value);
        }

        private void UpdateAimTransform()
        {
            if (armAnimator == null) return;

            bool aimHeld = _aimAction != null && _aimAction.IsPressed();
            _aiming = (aimHeld || holdAimPose) && _reloadPhase == ReloadPhase.None;

            UpdateAimFov();

            if (holdAimPose)
            {
                // Modo edicion en vivo del aim.
                // - Si el transform esta en la pose base (idle, o tras el reset que
                //   hace el Animator al re-equipar el arma), lo llevamos a la pose de
                //   aim guardada (base + aimLocalPos) para previsualizarla y que
                //   persista.
                // - Si el usuario lo arrastro (distinto de la base), capturamos el
                //   offset en aimLocalPos/aimLocalRot.
                // aimLocalPos es un OFFSET respecto a la pose idle, no la posicion
                // absoluta: hay que arrastrar el brazo a la posicion de aim real.
                Vector3 currentPos = armAnimator.transform.localPosition;
                if (Vector3.Distance(currentPos, _aimBaseLocalPos) > 1e-4f)
                {
                    aimLocalPos = currentPos - _aimBaseLocalPos;
                    aimLocalRot = (Quaternion.Inverse(_aimBaseLocalRot) * armAnimator.transform.localRotation).eulerAngles;
                }
                else
                {
                    armAnimator.transform.localPosition = _aimBaseLocalPos + aimLocalPos;
                    armAnimator.transform.localRotation = _aimBaseLocalRot * Quaternion.Euler(aimLocalRot);
                }
                return;
            }

            Vector3 targetPos = _aiming ? _aimBaseLocalPos + aimLocalPos : _aimBaseLocalPos;
            Quaternion targetRot = _aiming
                ? _aimBaseLocalRot * Quaternion.Euler(aimLocalRot)
                : _aimBaseLocalRot;

            float lambda = aimLerpDuration > 0f ? 1f / aimLerpDuration : float.MaxValue;
            float t = 1f - Mathf.Exp(-lambda * Time.deltaTime);

            armAnimator.transform.localPosition = Vector3.Lerp(armAnimator.transform.localPosition, targetPos, t);
            armAnimator.transform.localRotation = Quaternion.Slerp(armAnimator.transform.localRotation, targetRot, t);

            if (loopAimAnimation && _aiming && !armAnimator.IsInTransition(0))
            {
                AnimatorStateInfo s = armAnimator.GetCurrentAnimatorStateInfo(0);
                if (s.IsName(aimAnimationName) && s.normalizedTime >= 0.98f)
                    armAnimator.Play(aimAnimationName, 0, 0f);
            }
        }

        private void UpdateAimFov()
        {
            if (viewCamera == null) return;
            float target = _aiming ? aimFov : normalFov;
            float current = viewCamera.fieldOfView;
            float next = Mathf.SmoothDamp(current, target, ref _fovVelocity, fovSmoothTime);
            if (Mathf.Abs(next - current) > 0.01f)
            {
                viewCamera.fieldOfView = next;
            if (weaponCamera != null && Mathf.Abs(weaponCamera.fieldOfView - next) > 0.01f)
                weaponCamera.fieldOfView = next;
            }
        }

        private void FireShotgun()
        {
            if (viewCamera == null) return;

            bool aiming = _aiming;
            Vector3 rayOrigin;
            Vector3 baseDir;

            if (aiming && weaponVFX != null && weaponVFX.HasMuzzle)
            {
                rayOrigin = weaponVFX.MuzzlePosition;
                baseDir = aimUseMuzzleForward ? weaponVFX.MuzzleForward : weaponRoot.forward;
                if (weaponRoot != null)
                {
                    Quaternion weaponRot = weaponRoot.rotation;
                    if (aimShotOriginOffset != Vector3.zero)
                        rayOrigin += weaponRot * aimShotOriginOffset;
                    if (aimShotDirectionOffset != Vector3.zero)
                        baseDir = weaponRot * Quaternion.Euler(aimShotDirectionOffset) * Quaternion.Inverse(weaponRot) * baseDir;
                }
            }
            else
            {
                rayOrigin = viewCamera.transform.position;
                baseDir = viewCamera.transform.forward;
            }

            float spreadDeg = spread * (aiming ? aimSpreadMultiplier : 1f);

            if (weaponVFX != null) weaponVFX.PlayMuzzle();

            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = baseDir;
                if (spreadDeg > 0f)
                {
                    float angle = Random.Range(0f, spreadDeg);
                    Vector3 axis = Random.onUnitSphere;
                    dir = Quaternion.AngleAxis(angle, axis) * dir;
                }

                Ray ray = new Ray(rayOrigin, dir);
                bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, range, shootMask, QueryTriggerInteraction.Ignore);
                Vector3 endPoint = hitSomething ? hit.point : ray.origin + ray.direction * range;

                if (hitSomething)
                {
                    EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
                    if (enemy != null)
                        enemy.TakeDamage(Mathf.RoundToInt(damagePerPellet));
                    if (weaponVFX != null)
                    {
                        weaponVFX.PlayImpact(hit.point, hit.normal);
                        if (enemy == null) weaponVFX.PlayBulletHole(hit.point, hit.normal);
                    }
                }

                if (weaponVFX != null)
                    weaponVFX.PlayTracer(rayOrigin, endPoint);

                if (showLineTrace)
                {
                    if (weaponVFX != null) weaponVFX.DrawLineTrace(rayOrigin, endPoint, lineTraceColor, 1f);
                    else Debug.DrawLine(rayOrigin, endPoint, lineTraceColor, 0.1f);
                }
            }
        }

        // --- API para WeaponPickup (recoger/soltar el arma) ---
        public void Equip()
        {
            if (_equipped) return;
            _equipped = true;
            if (weaponRoot != null) weaponRoot.gameObject.SetActive(true);
            _shooting = false;
            _reloadPhase = ReloadPhase.None;
            if (armAnimator != null) { SetReloading(armAnimator, false); SetAiming(armAnimator, false); }
            if (gunAnimator != null) { SetReloading(gunAnimator, false); SetAiming(gunAnimator, false); }
            PlayAnim("Get");
        }

        public void Unequip()
        {
            _equipped = false;
            if (weaponRoot != null) weaponRoot.gameObject.SetActive(false);
            _shooting = false;
            _reloadPhase = ReloadPhase.None;
            if (armAnimator != null) { SetReloading(armAnimator, false); SetAiming(armAnimator, false); }
            if (gunAnimator != null) { SetReloading(gunAnimator, false); SetAiming(gunAnimator, false); }
        }

        public bool IsBusy => _reloadPhase != ReloadPhase.None || _shooting || _aiming;
        public bool IsEquipped => _equipped;
        public Camera ViewCamera => viewCamera;

        public int ShellsLoaded => _shellsLoaded;
        public int ReserveShells => _reserveShells;

        public void AddReserveShells(int n)
        {
            if (n <= 0) return;
            _reserveShells += n;
            Debug.Log($"<color=#77ff77>[Shotgun] +{n} cartuchos en reserva. reserva={_reserveShells}</color>");
        }
    }
}
