using PrisaControlada.Enemies;
using PrisaControlada.Items;
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
        [SerializeField] private Camera weaponCamera;
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private WeaponVFX weaponVFX;

        [Header("Weapon Stats")]
        [SerializeField] private float damage = 35f;
        [SerializeField] private float fireRate = 1.5f;
        [SerializeField] private float shotDelay = 0.2f;
        [SerializeField] private float range = 100f;
        [SerializeField] private int maxAmmo = 8;
        [SerializeField] private float reloadTime = 1.8f;
        [SerializeField] private LayerMask shootMask = ~0;

        [Header("Reserve Ammo (externo al tambor)")]
        [Tooltip("Balas externas del jugador (reserva). Al recargar, las del tambor se pierden y se cargan estas (si no hay suficientes, solo se cargan las que haya).")]
        [SerializeField] private int reserveAmmo = 12;

        [Header("Reload Bullet Drop")]
        [Tooltip("Prefab de UNA bala suelta (BulletPickup). Al recargar se instancia 1 por cada bala que quedaba en el tambor, en una posicion cercana que no se ve en la camara.")]
        [SerializeField] private GameObject bulletPickupPrefab;
        [Tooltip("Distancia detras de la camara donde se instancian las balas al recargar.")]
        [SerializeField] private float bulletDropDistance = 1.2f;
        [Tooltip("Desplazamiento lateral/vertical aleatorio al soltar cada bala.")]
        [SerializeField] private float bulletDropSpread = 0.25f;
        [Tooltip("Alto maximo por encima de la camara (0 = mismo nivel).")]
        [SerializeField] private float bulletDropUpOffset = 0f;
        [Tooltip("Velocidad inicial de cada bala al soltarse (rebote).")]
        [SerializeField] private float bulletDropSpeed = 2f;
        [Tooltip("Velocidad angular inicial aleatoria de cada bala.")]
        [SerializeField] private float bulletDropSpin = 720f;
        [Tooltip("Multiplicador de elasticidad al rebotar contra el suelo/paredes.")]
        [SerializeField] private float bulletDropBounciness = 0.5f;
        [Tooltip("Alto desde el que se sueltan para que caigan y reboten.")]
        [SerializeField] private float bulletDropHeight = 0.6f;

        [Header("Recoil")]
        [SerializeField] private float recoilAmount = 4f;
        [SerializeField] private float recoilRandomAmount = 2f;

        [Header("Equip / Unequip")]
        [SerializeField] private bool startEquipped = false;

        [Header("Aim")]
        [SerializeField] private float normalFov = 70f;
        [SerializeField] private float aimFov = 45f;
        [SerializeField] private float fovSmoothTime = 0.15f;
        [SerializeField] private Vector3 aimPositionOffset = new Vector3(-0.0476f, 0.001f, 0.0099f);
        [SerializeField] private Vector3 aimRotationOffset = new Vector3(350.27698f, 8.039391f, 341.54263f);
        [SerializeField] private float aimPositionSpeed = 8f;

        [Header("Reload")]
        [SerializeField] private Vector3 reloadPositionOffset = new Vector3(0.05f, -0.25f, 0.1f);
        [SerializeField] private Vector3 reloadRotationOffset = new Vector3(0f, 0f, 0f);
        [SerializeField, Range(0f, 3f)] private float reloadPositionDelay = 0.3f;
        private const float ReloadAnimGrace = 4f;

        [Header("Rotation Lag")]
        [SerializeField, Range(0f, 0.5f)] private float rotationLagStrength = 0.12f;
        [SerializeField, Range(0f, 20f)] private float rotationLagRecovery = 7f;
        [SerializeField, Range(0f, 15f)] private float maxRotationLag = 5f;

        [Header("TEMP (aim-only)")]
        [SerializeField] private bool aimOnlyMode = false;

        [Header("Tiro (linea del canon)")]
        [SerializeField] private bool useMuzzleForward = true;
        [SerializeField] private Vector3 shotDirectionOffset = Vector3.zero;
        [SerializeField] private Vector3 shotOriginOffset = Vector3.zero;

        [Header("Tiro cadera (sin aim)")]
        [SerializeField] private bool hipFireFromScreenCenter = true;
        [SerializeField] private Vector3 hipShotDirectionOffset = Vector3.zero;
        [SerializeField] private Vector3 hipShotOriginOffset = Vector3.zero;
        [Tooltip("Dispersion aleatoria del tiro de cadera (grados). Mas alto = menos preciso.")]
        [SerializeField, Range(0f, 10f)] private float hipFireSpread = 2f;

        [Header("Debug")]
        [SerializeField] private bool debugShowLineTrace = true;
        [SerializeField] private Color lineTraceColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color barrelLineColor = new Color(0.2f, 1f, 1f, 1f);

        private static readonly int AimParam = Animator.StringToHash("Aim");
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int ShootParam = Animator.StringToHash("Shoot");
        private static readonly int ReloadingParam = Animator.StringToHash("Reloading");
        private static readonly int GetParam = Animator.StringToHash("Get");

        private InputAction _attackAction;
        private InputAction _aimAction;
        private InputAction _reloadAction;
        private InputAction _weaponToggleAction;

        private bool _equipped;
        private bool _isAiming;
        private bool _isReloading;
        private bool _inTransition;
        private bool _wasInAimOnly;
        private int _ammo;
        private int _bulletsBeforeReload;
        private float _nextFireTime;
        private float _reloadEndTime;
        private float _reloadStartTime;
        private bool _wasAtReloadPos;
        private float _fovVelocity;
        private Vector3 _weaponRestPos;
        private Quaternion _weaponRestRot;
        private Quaternion _lagRot = Quaternion.identity;
        private Quaternion _prevCamRot = Quaternion.identity;
        private PlayerCamera _punchCamera;
        private float _pendingShotTime = -1f;

        public bool IsEquipped => _equipped;
        public bool IsAiming => _isAiming;
        public bool IsReloading => _isReloading;
        public bool IsShooting => _pendingShotTime > 0f || IsShootAnimPlaying();
        public bool IsBusy => _isAiming || _isReloading || IsShooting;
        public int Ammo => _ammo;
        public int MaxAmmo => maxAmmo;
        public int ReserveAmmo => reserveAmmo;
        public Camera ViewCamera => viewCamera;

        public void AddReserveAmmo(int amount)
        {
            if (amount < 0) return;
            reserveAmmo += amount;
            LogAmmo($"<color=#77ff77>[Weapon] Municion externa agregada: +{amount}.</color>");
        }

        private void LogAmmo(string prefix)
        {
            Debug.Log($"{prefix} <color=#ffcc66>[Weapon] Balas del jugador: tambor={_ammo}/{maxAmmo} reserva={reserveAmmo}</color>");
        }

        public void Equip()
        {
            SetEquipped(true);
        }

        public void Unequip()
        {
            if (_isReloading)
            {
                _isReloading = false;
                _reloadEndTime = 0f;
                SetBoolBoth(ReloadingParam, false);
                _inTransition = true;
            }
            SetEquipped(false);
        }

        private void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
            if (viewCamera != null) _punchCamera = viewCamera.GetComponent<PlayerCamera>();
            if (weaponCamera == null && viewCamera != null)
            {
                var overlay = viewCamera.transform.Find("WeaponCamera");
                if (overlay != null) weaponCamera = overlay.GetComponent<Camera>();
            }
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
            if (bulletPickupPrefab == null)
                bulletPickupPrefab = Resources.Load<GameObject>("Items/BulletPickup");

            if (inputActions != null)
            {
                _attackAction = inputActions.FindAction("Attack");
                _aimAction = inputActions.FindAction("Aim");
                _reloadAction = inputActions.FindAction("Reload");
                _weaponToggleAction = inputActions.FindAction("Previous");
            }
            if (viewCamera != null) _prevCamRot = viewCamera.transform.rotation;
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
            TryAutoLoadControllers();

            _ammo = maxAmmo;
            if (weaponRoot != null)
            {
                _weaponRestPos = weaponRoot.localPosition;
                _weaponRestRot = weaponRoot.localRotation;
                if (weaponVFX == null) weaponVFX = weaponRoot.gameObject.GetComponent<WeaponVFX>();
                if (weaponVFX == null) weaponVFX = weaponRoot.gameObject.AddComponent<WeaponVFX>();
            }
            WeaponPickup scenePickup = FindObjectOfType<WeaponPickup>();
            if (scenePickup != null && startEquipped)
                scenePickup.gameObject.SetActive(false);
            if (startEquipped)
                SetEquipped(true);
            else
                SetEquipped(false);
            LogAmmo("<color=#77ccff>[Weapon] Estado inicial de balas.</color>");
        }

        private void Update()
        {
            bool canControl = player == null || player.ControlEnabled;

            UpdateRotationLag();

            if (!_equipped) return;

            ResetTriggerBoth(ShootParam);

            if (aimOnlyMode)
            {
                if (_attackAction != null && _attackAction.WasPressedThisFrame())
                    Debug.LogWarning("<color=#ff7777>[Weapon] Click ignorado: aimOnlyMode ACTIVO (modo posicionamiento)</color>");
                // TEMPORAL: solo el loop de idleaim. NO se aplica posicion/zoom de aim
                // para poder mover el brazo/arma libremente sin que vuelva a su posicion.
                _wasInAimOnly = true;
                return;
            }
            if (_wasInAimOnly)
            {
                // Saliendo del modo aim-only: el arma vuelve sola al reposo (no se toma
                // la posicion manual como reposo, eso es para ajustar el aim via offsets).
                _wasInAimOnly = false;
                _inTransition = true;
            }

            UpdateAim(canControl);
            UpdateShoot(canControl);
            UpdateReload(canControl);
            UpdateAnimatorSpeed();
            UpdateAimPosition();
            UpdateFov();
            UpdatePendingShot();
        }

        private void TryAutoLoadControllers()
        {
            if (weaponArmAnimator != null && weaponArmAnimator.runtimeAnimatorController == null)
            {
                string goName = weaponArmAnimator.gameObject.name;
                RuntimeAnimatorController ctrl = Resources.Load<RuntimeAnimatorController>(goName + "Animator");
                if (ctrl != null)
                {
                    weaponArmAnimator.runtimeAnimatorController = ctrl;
                    Debug.Log($"[Weapon] Auto-loaded controller for {goName}: {ctrl.name}");
                }
            }
            if (weaponAnimator != null && weaponAnimator.runtimeAnimatorController == null)
            {
                string goName = weaponAnimator.gameObject.name;
                RuntimeAnimatorController ctrl = Resources.Load<RuntimeAnimatorController>(goName + "Animator");
                if (ctrl != null)
                {
                    weaponAnimator.runtimeAnimatorController = ctrl;
                    Debug.Log($"[Weapon] Auto-loaded controller for {goName}: {ctrl.name}");
                }
            }
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
            if (!value) _pendingShotTime = -1f;

            if (weaponRoot != null) weaponRoot.gameObject.SetActive(value);
            _isAiming = false;
            SetBoolBoth(AimParam, aimOnlyMode);

            if (value)
            {
                bool hasGetParam = HasParam(weaponArmAnimator, "Get") || HasParam(weaponAnimator, "Get");
                if (hasGetParam)
                {
                    PlayClip(weaponArmAnimator, "Get");
                    PlayClip(weaponAnimator, "Get");
                }
                else if (aimOnlyMode)
                {
                    PlayClip(weaponAnimator, "IdleAim");
                    PlayClip(weaponArmAnimator, "IdleAim");
                }
                else
                {
                    PlayClip(weaponAnimator, "Idle");
                    PlayClip(weaponArmAnimator, "Idle");
                }
            }
            else
            {
                PlayClip(weaponAnimator, "Empty");
                PlayClip(weaponArmAnimator, "Empty");
            }
        }

        private static bool HasParam(Animator anim, string paramName)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return false;
            foreach (AnimatorControllerParameter p in anim.parameters)
                if (p.name == paramName) return true;
            return false;
        }

        private void SetBoolBoth(int param, bool value)
        {
            if (weaponAnimator != null && weaponAnimator.runtimeAnimatorController != null) weaponAnimator.SetBool(param, value);
            if (weaponArmAnimator != null && weaponArmAnimator.runtimeAnimatorController != null) weaponArmAnimator.SetBool(param, value);
        }

        private void SetFloatBoth(int param, float value, float dampTime, float deltaTime)
        {
            if (weaponAnimator != null && weaponAnimator.runtimeAnimatorController != null) weaponAnimator.SetFloat(param, value, dampTime, deltaTime);
            if (weaponArmAnimator != null && weaponArmAnimator.runtimeAnimatorController != null) weaponArmAnimator.SetFloat(param, value, dampTime, deltaTime);
        }

        private void SetTriggerBoth(int param)
        {
            if (weaponAnimator != null && weaponAnimator.runtimeAnimatorController != null) weaponAnimator.SetTrigger(param);
            if (weaponArmAnimator != null && weaponArmAnimator.runtimeAnimatorController != null) weaponArmAnimator.SetTrigger(param);
        }

        private void ResetTriggerBoth(int param)
        {
            if (weaponAnimator != null && weaponAnimator.runtimeAnimatorController != null) weaponAnimator.ResetTrigger(param);
            if (weaponArmAnimator != null && weaponArmAnimator.runtimeAnimatorController != null) weaponArmAnimator.ResetTrigger(param);
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
                _inTransition = true;
                SetBoolBoth(AimParam, _isAiming);
            }
        }

        private void UpdateShoot(bool canControl)
        {
            if (_attackAction == null) return;
            if (!_attackAction.WasPressedThisFrame()) return;

            if (!canControl || _isReloading || IsReloadAnimPlaying() || (player != null && player.IsSprinting))
            {
                string reason = !canControl ? "sin control"
                    : ((_isReloading || IsReloadAnimPlaying()) ? "recargando (animacion en curso)" : "sprintando");
                Debug.LogWarning($"<color=#ff7777>[Weapon] Disparo bloqueado: {reason}. ammo={_ammo}/{maxAmmo} canControl={canControl} sprinting={(player != null && player.IsSprinting)}</color>");
                return;
            }

            if (Time.time < _nextFireTime || IsShootAnimPlaying() || _pendingShotTime > 0f)
            {
                Debug.LogWarning($"<color=#ff7777>[Weapon] Disparo bloqueado: animacion de disparo en curso (cooldown hasta que termine). ammo={_ammo}/{maxAmmo}</color>");
                return;
            }

            if (_ammo <= 0)
            {
                DryFire();
                return;
            }

            Fire();
        }

        private bool IsShootAnimPlaying()
        {
            return IsShootStatePlaying(weaponAnimator) || IsShootStatePlaying(weaponArmAnimator);
        }

        private bool IsReloadAnimPlaying()
        {
            return IsInState(weaponAnimator, "Reload") || IsInState(weaponArmAnimator, "Reload");
        }

        private static bool IsInState(Animator anim, string stateName)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return false;
            return anim.GetCurrentAnimatorStateInfo(0).IsName(stateName)
                || anim.GetNextAnimatorStateInfo(0).IsName(stateName);
        }

        private static bool IsShootStatePlaying(Animator anim)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return false;
            AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
            if ((info.IsName("Shoot") || info.IsName("ShootAim")) && info.normalizedTime < 1f) return true;
            AnimatorStateInfo next = anim.GetNextAnimatorStateInfo(0);
            return (next.IsName("Shoot") || next.IsName("ShootAim")) && next.normalizedTime < 1f;
        }

        private void DryFire()
        {
            string clip = _isAiming ? "ShootAim" : "Shoot";
            if (weaponAnimator != null) PlayClip(weaponAnimator, clip);
            _nextFireTime = Time.time + 1f / fireRate;
            Debug.Log("<color=#ff7777>[Weapon] Disparo en vacio (sin balas): tambor gira, SIN recoil. Presiona R para recargar</color>");
            LogAmmo("");
        }

        private void Fire()
        {
            SetTriggerBoth(ShootParam);
            _nextFireTime = Time.time + 1f / fireRate;
            _pendingShotTime = Time.time + shotDelay;
            Debug.Log($"<color=#77ff77>[Weapon] Disparo iniciado (animacion). Impacto en {shotDelay}s. ammo={_ammo}/{maxAmmo}</color>");
        }

        private void UpdatePendingShot()
        {
            if (_pendingShotTime < 0f) return;
            if (Time.time < _pendingShotTime) return;
            _pendingShotTime = -1f;
            if (!_equipped || _ammo <= 0 || _isReloading) return;

            _ammo--;
            ApplyRecoil();
            if (weaponVFX != null)
            {
                weaponVFX.PlayMuzzle();
                weaponVFX.PlayBarrelSmoke();
            }
            Debug.Log($"<color=#77ff77>[Weapon] DISPARO (impacto). ammo={_ammo}/{maxAmmo}</color>");
            if (_ammo <= 0)
                LogAmmo("<color=#ff7777>[Weapon] Cargador vacio. Presiona R para recargar.</color>");

            if (viewCamera == null) return;

            Quaternion weaponRot = weaponRoot != null ? weaponRoot.rotation : Quaternion.identity;
            Vector3 rayOrigin;
            Vector3 rayDir;

            if (!_isAiming && hipFireFromScreenCenter)
            {
                // Cadera: la bala sale del centro de la pantalla (el crosshair), asi
                // pega donde apunta el jugador aunque el arma este en posicion de reposo.
                rayOrigin = viewCamera.transform.position;
                rayDir = viewCamera.transform.forward;
                if (hipShotOriginOffset != Vector3.zero)
                    rayOrigin += weaponRot * hipShotOriginOffset;
                if (hipShotDirectionOffset != Vector3.zero)
                    rayDir = weaponRot * Quaternion.Euler(hipShotDirectionOffset) * Quaternion.Inverse(weaponRot) * rayDir;

                // Disparo de cadera impreciso: rotacion aleatoria alrededor de la
                // direccion apuntada, asi conviene apuntar (aim no tiene spread).
                if (hipFireSpread > 0f)
                {
                    float angle = Random.Range(0f, hipFireSpread);
                    Vector3 axis = Random.onUnitSphere;
                    rayDir = Quaternion.AngleAxis(angle, axis) * rayDir;
                }
            }
            else
            {
                rayOrigin = viewCamera.transform.position;
                rayDir = viewCamera.transform.forward;
                if (weaponVFX != null && weaponVFX.HasMuzzle)
                {
                    rayOrigin = weaponVFX.MuzzlePosition;
                    rayDir = useMuzzleForward ? weaponVFX.MuzzleForward : weaponRoot.forward;
                }
                if (shotOriginOffset != Vector3.zero)
                    rayOrigin += weaponRot * shotOriginOffset;
                if (shotDirectionOffset != Vector3.zero)
                    rayDir = weaponRot * Quaternion.Euler(shotDirectionOffset) * Quaternion.Inverse(weaponRot) * rayDir;
            }
            Ray ray = new Ray(rayOrigin, rayDir);
            Debug.Log($"<color=#ff66ff>[Weapon] Tiro: origin={rayOrigin} dir={rayDir} (offsetRot={shotDirectionOffset} offsetPos={shotOriginOffset})</color>");
            bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, range, shootMask, QueryTriggerInteraction.Ignore);
            if (hitSomething)
            {
                var enemy = hit.collider.GetComponentInParent<EnemyBase>();
                if (enemy != null)
                    enemy.TakeDamage(Mathf.RoundToInt(damage), ray.direction, hit.collider, hit.point);
                if (weaponVFX != null)
                {
                    weaponVFX.PlayImpact(hit.point, hit.normal);
                    if (enemy == null)
                        weaponVFX.PlayBulletHole(hit.point, hit.normal);
                }
                Debug.Log($"<color=#ffcc66>[Weapon] Raycast: impacto en '{hit.collider.name}' enemy={(enemy != null)} {(enemy != null ? $"dano={Mathf.RoundToInt(damage)}" : "")}</color>");
            }
            else
            {
                Debug.Log("<color=#ffcc66>[Weapon] Raycast: no impacto nada</color>");
            }

            Vector3 endPoint = hitSomething ? hit.point : ray.origin + ray.direction * range;
            if (weaponVFX != null)
                weaponVFX.PlayTracer(ray.origin, endPoint);

            if (debugShowLineTrace && weaponVFX != null)
            {
                Vector3 start = ray.origin;
                weaponVFX.DrawLineTrace(start, endPoint, lineTraceColor, 2f);
                Debug.Log($"<color=#ff66ff>[Weapon] LineTrace: muzzle={weaponVFX.HasMuzzle} start={start} end={endPoint}</color>");
            }
        }

        private void ApplyRecoil()
        {
            float up = recoilAmount + Random.Range(0f, recoilRandomAmount);
            float side = Random.Range(-recoilRandomAmount, recoilRandomAmount) * 0.5f;
            if (_punchCamera != null)
                _punchCamera.AddRecoil(up, side);
            Debug.Log($"<color=#ffb3ff>[Weapon] Recoil (camara): up={up:0.00} side={side:0.00}</color>");
        }

        private void UpdateReload(bool canControl)
        {
            if (_reloadAction == null) return;

            if (_isReloading && Time.time >= _reloadEndTime)
            {
                // Las balas del tambor se "pierden" y se cargan las externas (reserva).
                // Si la reserva no alcanza para llenar el tambor, solo se cargan las que hay.
                int loaded = Mathf.Min(maxAmmo, reserveAmmo);
                reserveAmmo -= loaded;
                _ammo = loaded;
                _isReloading = false;
                SetBoolBoth(ReloadingParam, false);
                _inTransition = true;
                DropBulletsFromChamber(_bulletsBeforeReload);
                LogAmmo($"<color=#77ccff>[Weapon] Recarga completa (se soltaron {_bulletsBeforeReload} balas del tambor como items).</color>");
                return;
            }

            if (!canControl) return;

            if (!_isReloading && _reloadAction.WasPressedThisFrame())
            {
                if (IsShootAnimPlaying())
                {
                    Debug.Log($"<color=#77ccff>[Weapon] Recarga ignorada: animacion de disparo en curso. Espera a que termine.</color>");
                    return;
                }
                if (_ammo >= maxAmmo)
                {
                    Debug.Log($"<color=#77ccff>[Weapon] Recarga ignorada: cargador lleno. ammo={_ammo}/{maxAmmo}</color>");
                    return;
                }
                if (reserveAmmo <= 0)
                {
                    LogAmmo("<color=#ff7777>[Weapon] No podes recargar: no te quedan balas externas.</color>");
                    return;
                }
                _isReloading = true;
                _bulletsBeforeReload = _ammo;
                _reloadStartTime = Time.time;
                _reloadEndTime = _reloadStartTime + reloadTime;
                SetBoolBoth(AimParam, false);
                SetBoolBoth(ReloadingParam, true);
                _isAiming = false;
                _inTransition = true;
                LogAmmo($"<color=#77ccff>[Weapon] Recarga iniciada (dura {reloadTime}s). Balas en el tambor que se van a soltar: {_bulletsBeforeReload}.</color>");
            }
        }

        private void DropBulletsFromChamber(int count)
        {
            if (count <= 0 || bulletPickupPrefab == null || viewCamera == null) return;

            // Punto base detras de la camara: fuera de su frustum pero cercano al jugador.
            Transform cam = viewCamera.transform;
            Vector3 basePos = cam.position - cam.forward * bulletDropDistance + Vector3.up * bulletDropUpOffset;

            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-bulletDropSpread, bulletDropSpread),
                    Random.Range(-bulletDropSpread, bulletDropSpread),
                    Random.Range(-bulletDropSpread, bulletDropSpread));
                Vector3 pos = basePos + offset;

                // Soltarlas con altura para que caigan y reboten, evitando que queden en el aire.
                if (Physics.Raycast(pos + Vector3.up, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                    pos = hit.point + Vector3.up * bulletDropHeight;

                Quaternion rot = Random.rotationUniform;
                GameObject bullet = Instantiate(bulletPickupPrefab, pos, rot);
                bullet.name = $"ChamberBullet_{i}";

                // Fisica: impulso inicial aleatorio + giro para que rebote natural.
                Rigidbody rb = bullet.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // CCD: evita que la bala (collider chico) atraviese el suelo/paredes a alta velocidad.
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.maxDepenetrationVelocity = 4f;
                    rb.solverVelocityIterations = 10;
                    Vector3 dir = Random.onUnitSphere;
                    dir.y = Mathf.Abs(dir.y) + 0.3f;
                    rb.linearVelocity = dir.normalized * bulletDropSpeed;
                    rb.angularVelocity = Random.onUnitSphere * bulletDropSpin * Mathf.Deg2Rad;
                }

                // Collider real un poco mas grande de lo visual: evita que la bala
                // atraviese el suelo aunque el prefab viejo no tenga CCD configurado.
                foreach (Collider col in bullet.GetComponentsInChildren<Collider>())
                {
                    if (col.isTrigger) continue;
                    if (col is CapsuleCollider capsule)
                    {
                        capsule.radius = Mathf.Max(capsule.radius, 0.03f);
                        capsule.height = Mathf.Max(capsule.height, 0.16f);
                    }
                    else if (col is SphereCollider sphere)
                    {
                        sphere.radius = Mathf.Max(sphere.radius, 0.03f);
                    }
                }

                // Elasticidad para un rebote lindo contra el suelo/paredes.
                foreach (Collider col in bullet.GetComponentsInChildren<Collider>())
                {
                    PhysicsMaterial mat = col.material != null ? col.material : new PhysicsMaterial();
                    mat.bounciness = bulletDropBounciness;
                    mat.bounceCombine = PhysicsMaterialCombine.Maximum;
                    col.material = mat;
                }
            }

            Debug.Log($"<color=#ffcc66>[Weapon] Soltadas {count} bala(s) de la recamara detras de la camara (pos={basePos}).</color>");
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

        private void UpdateAimPosition()
        {
            if (weaponRoot == null) return;

            // Red de seguridad: si la recarga ya termino pero el animator quedo
            // atascado en Reload, forzarlo a salir para que el arma vuelva al reposo.
            if (!_isReloading && IsReloadAnimPlaying() && Time.time > _reloadEndTime + ReloadAnimGrace)
            {
                Debug.LogWarning("<color=#ff7777>[Weapon] Animator de recarga atascado: forzando salida del estado Reload.</color>");
                SetBoolBoth(ReloadingParam, false);
                PlayClip(weaponAnimator, _isAiming ? "IdleAim" : "Idle");
                PlayClip(weaponArmAnimator, _isAiming ? "IdleAim" : "Idle");
            }

            // El aim es ABSOLUTO: apuntar mueve el arma a aimPositionOffset/aimRotationOffset
            // tal cual, sin depender de la posicion normal. Asi cambiar el reposo no descentra
            // el apuntado.
            Vector3 targetPos;
            Quaternion baseTargetRot;
            // El arma se queda en posicion de recarga solo mientras la recarga este activa o
            // su animacion siga en curso DENTRO de una ventana de tiempo. Asi, aunque el
            // animator quede tildado en Reload, el arma SIEMPRE vuelve al reposo al cerrarse
            // la ventana.
            // El arma baja SOLO mientras la recarga logica este activa. Cuando se
            // completa (ammo recargado) vuelve al reposo/apuntado, aunque la animacion
            // del brazo siga unos frames mas.
            bool hasReloadOffset = reloadPositionOffset != Vector3.zero || reloadRotationOffset != Vector3.zero;
            bool atReloadPos = _isReloading && hasReloadOffset && Time.time >= _reloadStartTime + reloadPositionDelay;
            if (atReloadPos != _wasAtReloadPos)
            {
                _inTransition = true;
                _wasAtReloadPos = atReloadPos;
            }
            if (atReloadPos)
            {
                targetPos = reloadPositionOffset;
                baseTargetRot = Quaternion.Euler(reloadRotationOffset);
            }
            else if (_isAiming)
            {
                targetPos = aimPositionOffset;
                baseTargetRot = Quaternion.Euler(aimRotationOffset);
            }
            else
            {
                targetPos = _weaponRestPos;
                baseTargetRot = _weaponRestRot;
            }
            Quaternion targetRot = baseTargetRot * _lagRot;

            bool atTarget = Vector3.Distance(weaponRoot.localPosition, targetPos) < 0.001f &&
                            Quaternion.Angle(weaponRoot.localRotation, targetRot) < 0.1f;

            if (_inTransition)
            {
                // La transicion (apuntar/dejar de apuntar) la maneja el script: no recapturar.
                if (atTarget) _inTransition = false;
            }
            else if (!_isReloading && !_isAiming && !atTarget
                && Quaternion.Angle(_lagRot, Quaternion.identity) < 0.5f)
            {
                // El usuario movio el arma a mano (sin apuntar): eso pasa a ser el nuevo
                // reposo, asi no vuelve a la posicion anterior.
                _weaponRestPos = weaponRoot.localPosition;
                _weaponRestRot = weaponRoot.localRotation;
                targetPos = _weaponRestPos;
                targetRot = _weaponRestRot;
            }

            float t = 1f - Mathf.Exp(-aimPositionSpeed * Time.deltaTime);
            weaponRoot.localPosition = Vector3.Lerp(weaponRoot.localPosition, targetPos, t);
            weaponRoot.localRotation = Quaternion.Slerp(weaponRoot.localRotation, targetRot, t);
        }

        private void UpdateRotationLag()
        {
            if (viewCamera == null) return;

            Quaternion camRot = viewCamera.transform.rotation;
            Quaternion delta = camRot * Quaternion.Inverse(_prevCamRot);
            _prevCamRot = camRot;

            if (!_equipped || aimOnlyMode)
            {
                _lagRot = Quaternion.identity;
                return;
            }

            Vector3 eul = delta.eulerAngles;
            Vector3 d = new Vector3(
                Mathf.DeltaAngle(0f, eul.x),
                Mathf.DeltaAngle(0f, eul.y),
                Mathf.DeltaAngle(0f, eul.z));

            _lagRot = Quaternion.Euler(-d * rotationLagStrength) * _lagRot;
            _lagRot = Quaternion.RotateTowards(Quaternion.identity, _lagRot, maxRotationLag);
            _lagRot = Quaternion.Slerp(_lagRot, Quaternion.identity, rotationLagRecovery * Time.deltaTime);
        }

        private void UpdateFov()
        {
            if (viewCamera == null) return;
            float target = _isAiming ? aimFov : normalFov;
            float current = viewCamera.fieldOfView;
            float next = Mathf.SmoothDamp(current, target, ref _fovVelocity, fovSmoothTime);
            if (Mathf.Abs(next - current) > 0.01f)
            {
                viewCamera.fieldOfView = next;
                if (weaponCamera != null && Mathf.Abs(weaponCamera.fieldOfView - next) > 0.01f)
                    weaponCamera.fieldOfView = next;
            }
        }
    }
}