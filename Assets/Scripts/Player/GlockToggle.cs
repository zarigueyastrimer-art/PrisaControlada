using UnityEngine;
using UnityEngine.InputSystem;
using PrisaControlada.Enemies;
using PrisaControlada.Items;

namespace PrisaControlada.Player
{
    public class GlockToggle : MonoBehaviour
    {
        public Key equipKey = Key.Digit3;

        bool _on;
        bool _prev;
        bool _kbWarned;
        bool _getting;
        Busy _busy = Busy.None;
        Transform _model;
        PlayerController _player;

        enum Busy { None, Shoot, Reload }
        enum ReloadPhase { None, Reloading, Holding, Getting }
        float _reloadHoldDuration = 1f;
        ReloadPhase _reloadPhase = ReloadPhase.None;
        float _reloadTimer;

        // Expulsion retardada del cargador: al apretar R se programa para 1s despues,
        // asi no sale volando instantaneamente al iniciar la recarga.
        float _magEjectTimer = -1f;
        int _magEjectBullets;
        bool _magEjected;

        // --- Aim (igual que las otras armas: posicion normal vs posicion aim + zoom) ---
        [Header("Aim - Posicion ABSOLUTA del glok en modo aim (reemplaza, no suma)")]
        public Vector3 aimLocalPos;
        public Vector3 aimLocalRot;
        public float aimLerpDuration = 0.2f;
        [Header("Aim - Edicion en vivo")]
        [Tooltip("Fuerza la pose IdelAim en bucle para acomodar el arma: arrastra el arma a la posicion deseada y se captura el offset en aimLocalPos/aimLocalRot.")]
        public bool holdAimPose;
        [Header("Aim - Zoom (FOV)")]
        public float normalFov = 70f;
        public float aimFov = 55f;
        public float fovSmoothTime = 0.15f;

        // --- Disparo (hitscan / line trace) ---
        [Header("Disparo (hitscan)")]
        [SerializeField] public WeaponVFX weaponVFX;
        [SerializeField] private float damage = 25f;
        [SerializeField] private float spread = 2f;            // dispersion cadera (grados)
        [SerializeField] private float aimSpreadMultiplier = 0.4f;
        [SerializeField] private float range = 200f;
        [SerializeField] private LayerMask shootMask = ~0;
        [SerializeField] private bool showLineTrace = true;
        [SerializeField] private Color lineTraceColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("Disparo - Aim Trace (DESDE el arma, modificable)")]
        [Tooltip("Si true, el disparo en aim sale segun el forward del arma; si false, usa el forward del modelo.")]
        [SerializeField] private bool aimUseMuzzleForward = true;
        [Tooltip("Offset (en espacio local del glok) del origen del ray cuando apuntas.")]
        [SerializeField] private Vector3 aimShotOriginOffset;
        [Tooltip("Offset de rotacion (euler, grados) aplicado a la direccion del ray cuando apuntas.")]
        [SerializeField] private Vector3 aimShotDirectionOffset;

        [Header("Recoil (camara) - glok: bajo")]
        [SerializeField] private float recoilAmount = 1.5f;
        [SerializeField] private float recoilRandomAmount = 0.8f;

        [Header("Municion")]
        [SerializeField] private int magazineSize = 20;
        [SerializeField] private int startMagazine = 20;
        [SerializeField] private int startReserve = 60;

        [Header("Magazine Drop (al recargar se expulsa el cargador)")]
        [SerializeField] public GameObject magazinePrefab;

        PlayerCamera _playerCamera;
        bool _aiming;
        int _magazine;
        int _reserve;
        bool _hasAimParam;
        Vector3 _aimBaseLocalPos;
        Quaternion _aimBaseLocalRot;
        Camera _viewCamera;
        float _fovVelocity;
        Vector3 _lastAppliedPos;
        Quaternion _lastAppliedRot;
        bool _lastAppliedSet;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        float _speedLogTimer;
        int _lastBand = -1;

        void Awake()
        {
            if (transform.childCount > 0) _model = transform.GetChild(0);
            Debug.Log("[Glock] GlockToggle Awake | hijos=" + transform.childCount +
                      " | modelo=" + (_model != null ? _model.name : "NULL"));
            if (_model == null)
                Debug.LogWarning("[Glock] No se encontro el modelo hijo (child 0).");
            if (_model != null)
            {
                _aimBaseLocalPos = _model.localPosition;
                _aimBaseLocalRot = _model.localRotation;
            }
            _viewCamera = Camera.main;
            _playerCamera = _viewCamera != null ? _viewCamera.GetComponent<PlayerCamera>() : null;
            _player = FindObjectOfType<PlayerController>();
            EnsureAnimator();
            if (_model != null) _model.gameObject.SetActive(false);
            _on = false;
            _magazine = startMagazine;
            _reserve = startReserve;
        }

        public int Magazine => _magazine;
        public int Reserve => _reserve;
        public int MagazineSize => magazineSize;
        public void AddReserveAmmo(int n) { if (n > 0) _reserve += n; }

        // True mientras dispara/recarga/equipa/apunta: asi el InventoryController no
        // deja guardar ni tirar el arma en medio de una accion.
        public bool IsBusy => _busy != Busy.None || _reloadPhase != ReloadPhase.None || _getting || _aiming;
        public Camera ViewCamera => _viewCamera;

        void LogAmmo(string tag)
        {
            Debug.Log($"<color=#ffcc66>[Glock] {tag} -> cargador: {_magazine}/{magazineSize} | reserva: {_reserve}</color>");
        }


        // El prefab no serializa las referencias del Animator; las asignamos en runtime
        // desde Resources para reproducir el idle de brazo.
        void EnsureAnimator()
        {
            if (_model == null) return;
            var anim = _model.GetComponent<Animator>();
            if (anim == null) anim = _model.gameObject.AddComponent<Animator>();
            if (anim.avatar == null)
            {
                var av = Resources.Load<Avatar>("GlockAvatar");
                if (av != null) anim.avatar = av;
                else Debug.LogWarning("[Glock] No se encontro GlockAvatar en Resources.");
            }
            if (anim.runtimeAnimatorController == null)
            {
                var ctrl = Resources.Load<RuntimeAnimatorController>("GlockAnimator");
                if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                else Debug.LogWarning("[Glock] No se encontro GlockAnimator en Resources.");
            }
            anim.applyRootMotion = false;
            anim.enabled = true;
            _hasAimParam = HasAimParam(anim);
        }

        bool HasAimParam(Animator anim)
        {
            for (int i = 0; i < anim.parameterCount; i++)
                if (anim.GetParameter(i).nameHash == Animator.StringToHash("Aim")) return true;
            return false;
        }

        // Reproduce la animacion "Get" al equipar y "GetReverse" al desequipar (solo brazos).
        // Si no existe el estado, pasa directo a la locomocion u oculta.
        void PlayGet()
        {
            _getting = true;
            _busy = Busy.None;
            _reloadPhase = ReloadPhase.None;
            _lastBand = -1;
            _aiming = false;
            _magEjectTimer = -1f;
            _magEjected = false;
            if (_model != null)
            {
                // Al equipar, arrancamos desde la pose normal (por si quedo en aim).
                _model.localPosition = _aimBaseLocalPos;
                _model.localRotation = _aimBaseLocalRot;
            }
            if (_model == null) { _getting = false; return; }
            var anim = _model.GetComponent<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null) { _getting = false; return; }
            // Equipar (_on=true) -> "Get"; desequipar (_on=false) -> "GetReverse".
            string stateName = _on ? "Get" : "GetReverse";
            int h = Animator.StringToHash(stateName);
            if (!anim.HasState(0, h))
            {
                _getting = false;
                if (_on) anim.Play("ArmBlend", 0, 0f);
                else _model.gameObject.SetActive(false);
                return;
            }
            anim.Play(stateName, 0, 0f);
            // El arma se queda en su pose default (corredera cerrada / ShootFreeze):
            // al equipar la "dibujamos" con GunGet; al desequipar la dejamos en ShootFreeze
            // (no reproducimos el get invertido del arma).
            if (_on)
            {
                if (anim.HasState(1, Animator.StringToHash("GunGet")))
                    anim.Play("GunGet", 1, 0f);
            }
            else
            {
                if (anim.HasState(1, Animator.StringToHash("ShootFreeze")))
                    anim.Play("ShootFreeze", 1, 0f);
            }
            if (_on) LogAmmo("Equipado");
        }

        // Cuando la animacion "Get"/"GetReverse" termina, volvemos a la locomocion (si
        // sigue equipada) u ocultamos el modelo (si se desequipo). El arma vuelve a la
        // pose congelada (ShootFreeze) en la capa Gun.
        void HandleGet()
        {
            if (!_getting || _model == null) return;
            var anim = _model.GetComponent<Animator>();
            if (anim == null) { _getting = false; return; }
            var st = anim.GetCurrentAnimatorStateInfo(0);
            if ((st.IsName("Get") || st.IsName("GetReverse")) && st.normalizedTime >= 0.95f)
            {
                _getting = false;
                if (anim.HasState(1, Animator.StringToHash("ShootFreeze")))
                    anim.Play("ShootFreeze", 1, 0f);
                if (_on)
                {
                    anim.Play("ArmBlend", 0, 0f);
                    _lastBand = -1;
                }
                else
                {
                    _model.gameObject.SetActive(false);
                }
            }
        }

        // Disparo (click) y recarga (R): solo animacion (no logica de dano). Reproduce
        // el estado en la capa del brazo (0) y en la del arma (1). Al terminar, el brazo
        // vuelve a ArmBlend y el arma a ShootFreeze (corredera cerrada).
        void HandleShootReload()
        {
            if (!_on || _getting || _model == null) return;
            var anim = _model.GetComponent<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null) return;

            if (_busy == Busy.None)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    if (_magazine <= 0)
                    {
                        Debug.Log("<color=#ff7777>[Glock] Cargador vacio: recarga con R.</color>");
                        LogAmmo("Cargador vacio");
                        return;
                    }
                    // Si esta apuntando, disparo aim (brazo + arma); sino, disparo normal.
                    string armShoot = "Shoot", gunShoot = "GunShoot";
                    if (_aiming)
                    {
                        if (anim.HasState(0, Animator.StringToHash("ShootAim")) && anim.HasState(1, Animator.StringToHash("GunShootAim")))
                        { armShoot = "ShootAim"; gunShoot = "GunShootAim"; }
                    }
                    Fire(); // hitscan: cadera = centro de pantalla; aim = desde el arma
                    TryPlay(anim, armShoot, gunShoot, Busy.Shoot);
                    _magazine--;
                    LogAmmo("Disparo");
                }
                else
                {
                    var kb = Keyboard.current;
                    if (kb != null && kb.rKey.wasPressedThisFrame)
                    {
                        if (_magazine >= magazineSize)
                        {
                            Debug.Log("<color=#ff7777>[Glock] Cargador lleno.</color>");
                        }
                        else if (_reserve <= 0)
                        {
                            Debug.Log("<color=#ff7777>[Glock] No podes recargar: no te quedan balas en reserva.</color>");
                        }
                        else
                        {
                            // Programa la expulsion del cargador actual 1s despues (no
                            // instantaneo). Al completar la recarga carga uno lleno.
                            _magEjectTimer = 1f;
                            _magEjectBullets = _magazine;
                            _magEjected = false;
                            TryPlay(anim, "Reload", "GunReload", Busy.Reload);
                            _reloadPhase = ReloadPhase.Reloading;
                        }
                    }
                }
            }

            if (_busy == Busy.Shoot)
            {
                // Disparo: al terminar vuelve a locomocion / corredera cerrada.
                var st = anim.GetCurrentAnimatorStateInfo(0);
                if ((st.IsName("Shoot") || st.IsName("ShootAim")) && st.normalizedTime >= 0.98f)
                {
                    _busy = Busy.None;
                    // Brazo: si sigue apuntando, vuelve DIRECTO a AimIdle (sin pasar por
                    // idle -> aim, que generaba una transicion rara).
                    if (_aiming)
                    {
                        if (anim.HasState(0, Animator.StringToHash("AimIdle"))) anim.Play("AimIdle", 0, 0f);
                    }
                    else
                    {
                        if (anim.HasState(0, Animator.StringToHash("ArmBlend"))) anim.Play("ArmBlend", 0, 0f);
                    }
                    // Arma: pose de aim congelada (ultimo frame) si sigue apuntando, sino ShootFreeze.
                    PlayGunIdle(anim);
                    _lastBand = -1;
                }
            }
            else if (_busy == Busy.Reload)
            {
                HandleReload(anim);
            }
        }

        // Secuencia de recarga (igual que la escopeta): reload -> mantiene el ultimo
        // frame un tiempo (timer) -> reproduce "Get" (brazo+arma) -> recien ahi el idle.
        void HandleReload(Animator anim)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (_reloadPhase == ReloadPhase.Reloading)
            {
                if (st.IsName("Reload") && st.normalizedTime >= 0.98f)
                {
                    _reloadPhase = ReloadPhase.Holding;
                    _reloadTimer = _reloadHoldDuration;
                }
            }
            else if (_reloadPhase == ReloadPhase.Holding)
            {
                _reloadTimer -= Time.deltaTime;
                if (_reloadTimer <= 0f)
                {
                    _reloadPhase = ReloadPhase.Getting;
                    anim.Play("Get", 0, 0f);
                    if (anim.HasState(1, Animator.StringToHash("GunGet"))) anim.Play("GunGet", 1, 0f);
                }
            }
                else if (_reloadPhase == ReloadPhase.Getting)
                {
                        if (st.IsName("Get") && st.normalizedTime >= 0.95f)
                        {
                            // Carga un cargador lleno desde la reserva (consumo = magazineSize).
                            int take = Mathf.Min(magazineSize, _reserve);
                            _magazine = take;
                            _reserve -= take;
                            _magEjectTimer = -1f;
                            _magEjected = false;
                            LogAmmo("Recarga");
                            _busy = Busy.None;
                            _reloadPhase = ReloadPhase.None;
                            if (anim.HasState(0, Animator.StringToHash("ArmBlend"))) anim.Play("ArmBlend", 0, 0f);
                            if (anim.HasState(1, Animator.StringToHash("ShootFreeze"))) anim.Play("ShootFreeze", 1, 0f);
                            _lastBand = -1;
                        }
                }
        }

        void TryPlay(Animator anim, string armState, string gunState, Busy kind)
        {
            if (anim.HasState(0, Animator.StringToHash(armState)) && anim.HasState(1, Animator.StringToHash(gunState)))
            {
                anim.Play(armState, 0, 0f);
                anim.Play(gunState, 1, 0f);
                _busy = kind;
            }
        }

        // Expulsa el cargador actual como item fisico con impulso + giro + rebote
        // (igual que WeaponController.DropBulletsFromChamber del revolver). Al
        // recogerlo devuelve sus balas a la reserva (GlockMagazinePickup).
        void SpawnEjectedMagazine(int bullets)
        {
            if (magazinePrefab == null)
            {
                Debug.LogWarning("<color=#ff7777>[Glock] magazinePrefab es NULL: no se expulsa el cargador. Corre Tools -> Setup Glock para enlazar el prefab.</color>");
                return;
            }
            Transform cam = (_playerCamera != null ? _playerCamera.transform : (_viewCamera != null ? _viewCamera.transform : null));
            if (cam == null) return;

            Vector3 pos = cam.position + cam.right * 0.35f + cam.forward * 0.2f - cam.up * 0.1f;
            Quaternion rot = Random.rotationUniform;
            var mag = Instantiate(magazinePrefab, pos, rot) as GameObject;
            if (mag == null) return;
            mag.name = "GlockMagazine_Dropped";

            var pickup = mag.GetComponent<GlockMagazinePickup>();
            if (pickup != null) pickup.bullets = bullets;

            var rb = mag.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                Vector3 dir = (cam.right * 0.6f + cam.forward * 0.2f - cam.up * 0.6f).normalized;
                rb.linearVelocity = dir * 3f;
                rb.angularVelocity = Random.onUnitSphere * (720f * Mathf.Deg2Rad);
            }

            foreach (Collider col in mag.GetComponentsInChildren<Collider>())
            {
                if (col.isTrigger) continue;
                var pm = col.material != null ? col.material : new PhysicsMaterial();
                pm.bounciness = 0.4f;
                pm.bounceCombine = PhysicsMaterialCombine.Maximum;
                col.material = pm;
            }

            Debug.Log($"<color=#ffcc66>[Glock] Cargador expulsado ({bullets} balas). Recogelo para devolverlas a la reserva.</color>");
        }

        // Pone la capa Gun en la pose de reposo correcta segun si se esta apuntando:
        // apuntando -> GunAimFreeze (pose de aim congelada en su ULTIMO frame, no en el 0);
        // sino -> ShootFreeze (default). No debe sobreescribir un disparo/recarga en curso.
        void PlayGunIdle(Animator anim)
        {
            if (_aiming)
            {
                // NormalizedTime 1 => ultimo frame del clip de aim (pose de aim sostenida),
                // evita el salto hacia atras que hacia el frame 0.
                if (anim.HasState(1, Animator.StringToHash("GunAimFreeze"))) anim.Play("GunAimFreeze", 1, 1f);
            }
            else
            {
                if (anim.HasState(1, Animator.StringToHash("ShootFreeze"))) anim.Play("ShootFreeze", 1, 0f);
            }
        }

        void SetGunIdle(Animator anim)
        {
            if (_busy != Busy.None) return;
            var gs = anim.GetCurrentAnimatorStateInfo(1);
            if (gs.IsName("GunShoot") || gs.IsName("GunShootAim") || gs.IsName("GunReload") || gs.IsName("GunGet")) return;
            string desired = _aiming ? "GunAimFreeze" : "ShootFreeze";
            if (gs.IsName(desired)) return;
            PlayGunIdle(anim);
        }

        // Hitscan del glock:
        // - Cadera: el ray sale de la camara hacia el CENTRO de la pantalla (mitad).
        // - Apuntando: el ray sale DESDE el arma (muzzle / modelo), con offsets modificables
        //   (aimShotOriginOffset / aimShotDirectionOffset) para ajustar el line trace.
        void Fire()
        {
            if (_viewCamera == null) _viewCamera = Camera.main;
            if (_viewCamera == null) return;

            bool aiming = _aiming;
            Vector3 rayOrigin;
            Vector3 baseDir;

            if (aiming)
            {
                // Apuntando: SIEMPRE desde el arma (modelo/glok, o muzzle si hay WeaponVFX).
                if (weaponVFX != null && weaponVFX.HasMuzzle)
                {
                    rayOrigin = weaponVFX.MuzzlePosition;
                    baseDir = aimUseMuzzleForward ? weaponVFX.MuzzleForward : (_model != null ? _model.forward : transform.forward);
                }
                else
                {
                    rayOrigin = _model != null ? _model.position : transform.position;
                    baseDir = aimUseMuzzleForward ? (_model != null ? _model.forward : transform.forward) : transform.forward;
                }
                if (_model != null)
                {
                    Quaternion rot = _model.rotation;
                    if (aimShotOriginOffset != Vector3.zero) rayOrigin += rot * aimShotOriginOffset;
                    if (aimShotDirectionOffset != Vector3.zero)
                        baseDir = rot * Quaternion.Euler(aimShotDirectionOffset) * Quaternion.Inverse(rot) * baseDir;
                }
            }
            else
            {
                // Cadera: desde la camara, hacia el centro de la pantalla.
                rayOrigin = _viewCamera.transform.position;
                baseDir = _viewCamera.transform.forward;
            }

            float spreadDeg = spread * (aiming ? aimSpreadMultiplier : 1f);
            if (weaponVFX != null) weaponVFX.PlayMuzzle();

            Vector3 dir = baseDir;
            if (spreadDeg > 0f)
            {
                float angle = Random.Range(0f, spreadDeg);
                dir = Quaternion.AngleAxis(angle, Random.onUnitSphere) * dir;
            }

            Ray ray = new Ray(rayOrigin, dir);
            bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, range, shootMask, QueryTriggerInteraction.Ignore);
            Vector3 endPoint = hitSomething ? hit.point : ray.origin + ray.direction * range;

            if (hitSomething)
            {
                EnemyBase enemy = hit.collider.GetComponentInParent<EnemyBase>();
                if (enemy != null) enemy.TakeDamage(Mathf.RoundToInt(damage), ray.direction, hit.collider, hit.point);
                if (weaponVFX != null)
                {
                    weaponVFX.PlayImpact(hit.point, hit.normal);
                    if (enemy == null) weaponVFX.PlayBulletHole(hit.point, hit.normal);
                }
            }

            if (weaponVFX != null) weaponVFX.PlayTracer(rayOrigin, endPoint);
            if (showLineTrace)
            {
                if (weaponVFX != null) weaponVFX.DrawLineTrace(rayOrigin, endPoint, lineTraceColor, 1f);
                else Debug.DrawLine(rayOrigin, endPoint, lineTraceColor, 0.1f);
            }

            // Recoil de camara (glok: bajo).
            if (_playerCamera != null)
            {
                float up = recoilAmount + Random.Range(0f, recoilRandomAmount);
                float side = Random.Range(-recoilRandomAmount, recoilRandomAmount) * 0.5f;
                _playerCamera.AddRecoil(up, side);
            }
        }

        // Apuntado: al mantener click derecho, setea el bool "Aim" (la capa base pasa a
        // AimIdle) y desplaza el modelo a la posicion de aim (offset) con un lerp. Zoom FOV.
        void UpdateAim()
        {
            if (!_on || _getting || _model == null) return;
            var anim = _model.GetComponent<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null) return;
            if (!holdAimPose) _lastAppliedSet = false;

            bool aimHeld = false;
            var mouse = Mouse.current;
            if (mouse != null) aimHeld = mouse.rightButton.isPressed;
            aimHeld = aimHeld || holdAimPose;
            _aiming = aimHeld;

            if (_hasAimParam) anim.SetBool("Aim", aimHeld);

            if (holdAimPose)
            {
                // Modo edicion en vivo del aim: se fuerza la pose IdelAim en bucle.
                // - Si edita aimLocalPos/aimLocalRot en el inspector, el arma se mueve ahi.
                // - Si arrastra el arma en la escena, se captura el offset en esos campos.
                // Para diferenciar edicion de campo vs arrastre, comparamos contra la
                // ultima pose que NOSOTROS aplicamos (no contra la base).
                Vector3 desiredPos = aimLocalPos;
                Quaternion desiredRot = Quaternion.Euler(aimLocalRot);
                bool external = false;
                if (_lastAppliedSet)
                {
                    if (Vector3.Distance(_model.localPosition, _lastAppliedPos) > 1e-3f) external = true;
                    if (Quaternion.Angle(_model.localRotation, _lastAppliedRot) > 0.1f) external = true;
                }
                if (external)
                {
                    aimLocalPos = _model.localPosition;
                    aimLocalRot = _model.localRotation.eulerAngles;
                    _lastAppliedPos = _model.localPosition;
                    _lastAppliedRot = _model.localRotation;
                }
                else
                {
                    _model.localPosition = desiredPos;
                    _model.localRotation = desiredRot;
                    _lastAppliedPos = desiredPos;
                    _lastAppliedRot = desiredRot;
                }
                _lastAppliedSet = true;
                UpdateAimFov(true);
                return;
            }

            Vector3 targetPos = aimHeld ? aimLocalPos : _aimBaseLocalPos;
            Quaternion targetRot = aimHeld ? Quaternion.Euler(aimLocalRot) : _aimBaseLocalRot;
            float lambda = aimLerpDuration > 0f ? 1f / aimLerpDuration : float.MaxValue;
            float t = 1f - Mathf.Exp(-lambda * Time.deltaTime);
            _model.localPosition = Vector3.Lerp(_model.localPosition, targetPos, t);
            _model.localRotation = Quaternion.Slerp(_model.localRotation, targetRot, t);

            UpdateAimFov(aimHeld);
            SetGunIdle(anim);
        }

        void UpdateAimFov(bool aiming)
        {
            if (_viewCamera == null) _viewCamera = Camera.main;
            if (_viewCamera == null) return;
            float target = aiming ? aimFov : normalFov;
            float cur = _viewCamera.fieldOfView;
            float next = Mathf.SmoothDamp(cur, target, ref _fovVelocity, fovSmoothTime);
            if (Mathf.Abs(next - cur) > 0.01f) _viewCamera.fieldOfView = next;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                if (!_kbWarned) { Debug.LogWarning("[Glock] Keyboard.current es NULL."); _kbWarned = true; }
                return;
            }
            bool down = kb[equipKey].isPressed;
            if (down && !_prev)
            {
                _on = !_on;
                if (_on && _model != null) _model.gameObject.SetActive(true);
                // En desequipe NO ocultamos ya: PlayGet reproduce el get y HandleGet
                // oculta el modelo al terminar.
                PlayGet();
                Debug.Log("[Glock] " + (_on ? "ACTIVADA (get)" : "DESACTIVADA (get)"));
            }
            _prev = down;

            UpdateSpeed();
            HandleGet();
            HandleShootReload();
            UpdateAim();

            // Expulsion retardada del cargador (~1s despues de apretar R). Cuenta
            // independientemente de la fase de recarga, asi no se congela si la
            // recarga termina antes del segundo.
            if (_magEjectTimer > 0f)
            {
                _magEjectTimer -= Time.deltaTime;
                if (_magEjectTimer <= 0f && !_magEjected)
                {
                    _magEjected = true;
                    SpawnEjectedMagazine(_magEjectBullets);
                }
            }
        }

        // Igual que las otras armas: normaliza la velocidad del personaje
        // (HorizontalSpeed / WalkSpeed) y la pasa al Animator como "Speed" para
        // que el blend tree del brazo mezcle Idle(0)/Walk(1)/Run(2).
        void UpdateSpeed()
        {
            if (!_on || _model == null) return;
            var anim = _model.GetComponent<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null) return;
            if (_player == null) _player = FindObjectOfType<PlayerController>();
            float rawSpeed = _player != null ? _player.HorizontalSpeed : 0f;
            float walkSpeed = _player != null ? _player.WalkSpeed : 2f;
            float normalized = walkSpeed > 0f ? rawSpeed / walkSpeed : 0f;
            anim.SetFloat(SpeedHash, normalized);

            // Band de locomocion: 0=idle, 1=walk, 2=run. Al cambiar de band, reiniciamos
            // el estado del brazo desde el frame 0 para que cada animacion (idle/walk/run)
            // empiece por su primer frame y no continua desde donde quedo. Solo mientras
            // estamos en la locomocion (no durante el "Get").
            if (!_getting && anim.GetCurrentAnimatorStateInfo(0).IsName("ArmBlend"))
            {
                int band = normalized < 0.5f ? 0 : (normalized < 1.5f ? 1 : 2);
                if (band != _lastBand)
                {
                    _lastBand = band;
                    anim.Play("ArmBlend", 0, 0f);
                }
            }

            // LOG TEMPORAL de diagnostico (borrar despues):
            _speedLogTimer -= Time.deltaTime;
            if (_speedLogTimer <= 0f)
            {
                _speedLogTimer = 1f;
                Debug.Log("[Glock][diag] Speed=" + normalized.ToString("F2") +
                          " raw=" + rawSpeed.ToString("F2") + " walk=" + walkSpeed.ToString("F2"));
            }
        }

        public void Equip()
        {
            if (_on) return;
            _on = true;
            if (_model != null) _model.gameObject.SetActive(true);
            PlayGet();
            Debug.Log("[Glock] ACTIVADA (get)");
            var wc = GetComponentInParent<WeaponController>();
            if (wc != null) wc.Unequip();
            var st = FindFirstObjectByType<ShotgunToggle>();
            if (st != null) st.Unequip();
        }

        public void Unequip()
        {
            if (!_on) return;
            _on = false;
            _busy = Busy.None;
            _reloadPhase = ReloadPhase.None;
            _aiming = false;
            if (_viewCamera != null) _viewCamera.fieldOfView = normalFov;
            PlayGet();
            Debug.Log("[Glock] DESACTIVADA (get)");
        }

        // Desequipa la Glock SIN la animacion de guardar (GetReverse): se usa al
        // soltar/tirar el arma al suelo, para que el objeto caiga sin animar.
        public void SilentUnequip()
        {
            if (!_on) return;
            _on = false;
            _busy = Busy.None;
            _reloadPhase = ReloadPhase.None;
            _aiming = false;
            if (_viewCamera != null) _viewCamera.fieldOfView = normalFov;
            if (_model != null) _model.gameObject.SetActive(false);
        }
    }
}
