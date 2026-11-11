using System.Collections.Generic;
using PrisaControlada.Enemies;
using PrisaControlada.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.Audio
{
    // Agente de audio de ambiente. Decide que clips reproducir segun un medidor
    // de "tension" (0..1) que va bajando solo y se modifica por eventos del juego.
    // - Ambiente base: loop permanente.
    // - Ambiente secundario: un clip a la vez, con fade in/out segun la tension.
    // - Sonidos extra: one-shot aleatorios con cooldown.
    public class AudioTensionAgent : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private InventoryController inventory;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private EnemySpawner enemySpawner;

        [Header("Ambiente base (loop)")]
        [SerializeField] private AudioClip baseClip;
        [SerializeField, Range(0f, 1f)] private float baseVolume = 1f;
        [SerializeField] private float baseMinDistance = 0f;
        [SerializeField] private float baseMaxDistance = 0f;

        [Header("Ambiente secundario (one-shot)")]
        [SerializeField] private AudioClip[] secondaryClips;
        [SerializeField, Range(0f, 1f)] private float secondaryMaxVolume = 0.6f;
        [SerializeField] private float secondaryFadeSpeed = 0.5f;

        [Header("Sonidos extra (one-shot)")]
        [SerializeField] private AudioClip[] extraClips;
        [SerializeField, Range(0f, 1f)] private float extraVolume = 0.4f;
        [Header("Cooldowns")]
        [SerializeField] private float secondaryCooldown = 90f; // 1.30 min antes de poder volver a poner un secundario
        [SerializeField] private float extraCooldownMin = 15f;
        [SerializeField] private float extraCooldownMax = 120f;

        [Header("Tension")]
        [SerializeField, Range(0f, 1f)] private float tension = 0.5f;
        [SerializeField] private float tensionDecayPerSecond = 0.002f; // 0.01 cada 5s
        // Entre estos dos umbrales la tension se considera "normal":
        // por debajo de low entra ambiente secundario, por encima de high se relaja.
        [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.3f;
        [SerializeField, Range(0f, 1f)] private float highThreshold = 0.7f;
        [SerializeField] private float relaxDelay = 15f; // segundos antes de empezar el fade-out del secundario
        [SerializeField] public float enemyProximityRange = 15f; // rango en metros para considerar "cerca" de un enemigo

        private AudioSource _baseSource;
        private AudioSource _secondarySource;
        private AudioSource _extraSource;

        private float _lastAmmo;
        private float _lastHealth;
        private float _lastExtraTime;
        private float _nextSecondaryTime;
        private float _nextExtraTime;
        private float _secondaryTargetVolume;
        private bool _secondaryPlaying;
        private float _relaxTimer;
        private bool _relaxing;

        private readonly List<EnemyBase> _enemies = new List<EnemyBase>();

        private void Awake()
        {
            _baseSource = gameObject.AddComponent<AudioSource>();
            _baseSource.playOnAwake = false;
            _baseSource.loop = true;
            _baseSource.spatialBlend = 0f;
            _baseSource.volume = baseVolume;
            _baseSource.clip = baseClip;
            if (baseClip != null) _baseSource.Play();

            _secondarySource = gameObject.AddComponent<AudioSource>();
            _secondarySource.playOnAwake = false;
            _secondarySource.loop = false;
            _secondarySource.spatialBlend = 0f;
            _secondarySource.volume = 0f;

            _extraSource = gameObject.AddComponent<AudioSource>();
            _extraSource.playOnAwake = false;
            _extraSource.loop = false;
            _extraSource.spatialBlend = 0f;
            _extraSource.volume = extraVolume;

            _nextExtraTime = Time.time + Random.Range(extraCooldownMin, extraCooldownMax);
            _nextSecondaryTime = Time.time + secondaryCooldown;
            _lastExtraTime = Time.time;
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged += OnPlayerDamaged;
                playerHealth.OnHealed += OnPlayerHealed;
            }
            if (inventory != null)
                inventory.InventoryChanged += OnInventoryChanged;
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged -= OnPlayerDamaged;
                playerHealth.OnHealed -= OnPlayerHealed;
            }
            if (inventory != null)
                inventory.InventoryChanged -= OnInventoryChanged;
        }

        private GameObject _sunObj;

        private void Start()
        {
            _sunObj = GameObject.Find("Sun");
            if (playerHealth != null) _lastHealth = playerHealth.CurrentHealth;
            if (playerController != null) { }
        }

        private void Update()
        {
            UpdateTension();
            UpdateBaseVolume();
            UpdateSecondary();
            UpdateExtras();
            HandleDebugInput();
        }

        private void HandleDebugInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame && extraClips != null && extraClips.Length > 0)
            {
                var clip = extraClips[Random.Range(0, extraClips.Length)];
                _extraSource.PlayOneShot(clip, extraVolume);
                tension = Mathf.Clamp01(tension + 0.05f);
            }
            if (kb.f2Key.wasPressedThisFrame && secondaryClips != null && secondaryClips.Length > 0 && Time.time >= _nextSecondaryTime)
            {
                var clip = secondaryClips[Random.Range(0, secondaryClips.Length)];
                _secondarySource.clip = clip;
                _secondarySource.volume = 0f;
                _secondarySource.Play();
                _secondaryPlaying = true;
                _secondaryTargetVolume = secondaryMaxVolume;
                _relaxing = false;
                _relaxTimer = 0f;
                tension = Mathf.Clamp01(tension + tensionDecayPerSecond * Time.deltaTime);
            }
            if (kb.f3Key.wasPressedThisFrame)
            {
                tension = 0f;
                _relaxing = true;
                _relaxTimer = 0f;
                _secondaryTargetVolume = 0f;
            }
            if (kb.f4Key.wasPressedThisFrame)
            {
                tension = 1f;
                _relaxing = false;
                _relaxTimer = 0f;
            }
            if (kb.lKey.wasPressedThisFrame && _sunObj != null)
            {
                _sunObj.SetActive(!_sunObj.activeSelf);
            }
        }

        private void UpdateTension()
        {
            // Tension decae sola con el tiempo.
            tension = Mathf.Clamp01(tension - tensionDecayPerSecond * Time.deltaTime);

            // Polling de enemigos vivos y cercanos.
            RefreshEnemies();
            float minEnemyDist = float.MaxValue;
            int aliveCount = 0;
            foreach (var e in _enemies)
            {
                if (e == null || e.Dead) continue;
                aliveCount++;
                if (playerController != null)
                {
                    float d = Vector3.Distance(e.transform.position, playerController.transform.position);
                    if (d < minEnemyDist) minEnemyDist = d;
                }
            }
            // Cuantos mas enemigos vivos y mas cerca, mas tension.
            if (aliveCount > 0)
            {
                float proximity = Mathf.Clamp01(1f - (minEnemyDist / 30f));
                tension = Mathf.Clamp01(tension + proximity * 0.0005f * Time.deltaTime);

                // Si estas cerca de un enemigo (distancia < enemyProximityRange), sube tension 0.005/s acumulativa.
                if (minEnemyDist < enemyProximityRange)
                    tension = Mathf.Clamp01(tension + 0.005f * Time.deltaTime);
            }

            // Polling de ammo (si bajo, baja tension).
            PollAmmo();

            // Cuando la tension esta por encima del highThreshold, el director busca relajar.
            if (tension > highThreshold)
            {
                if (!_relaxing)
                {
                    _relaxing = true;
                    _relaxTimer = relaxDelay;
                }
            }
            else if (tension < lowThreshold)
            {
                // Tension baja -> mete tencion (sube como decae).
                tension = Mathf.Clamp01(tension + tensionDecayPerSecond * Time.deltaTime);
            }
            else
            {
                // Zona normal: no hace nada especial.
                _relaxing = false;
                _relaxTimer = 0f;
            }
        }

        private void PollAmmo()
        {
            float ammo = 0f;
            // WeaponController (revolver)
            var wcs = FindObjectsOfType<WeaponController>();
            foreach (var wc in wcs) ammo += wc.ReserveAmmo;
            // GlockToggle
            var gts = FindObjectsOfType<GlockToggle>();
            foreach (var gt in gts) ammo += gt.Reserve;
            // ShotgunToggle
            var sts = FindObjectsOfType<ShotgunToggle>();
            foreach (var st in sts) ammo += st.ReserveShells;

            if (_lastAmmo == 0f && ammo > 0f) _lastAmmo = ammo;
            if (ammo > _lastAmmo + 0.01f)
            {
                // Encontro balas -> baja tension.
                tension = Mathf.Clamp01(tension - 0.05f);
                _lastAmmo = ammo;
            }
            else if (ammo < _lastAmmo - 0.01f)
            {
                _lastAmmo = ammo;
            }
            if (_lastAmmo == 0f) _lastAmmo = ammo;
        }

        private void OnPlayerDamaged(int currentHealth)
        {
            // Recibir golpe sube tension en 0.2 (no a 1 de una).
            tension = Mathf.Clamp01(tension + 0.2f);
            _relaxing = false;
            _relaxTimer = 0f;
        }

        private void OnPlayerHealed(int currentHealth)
        {
            // Curarse no cambia tension directamente.
        }

        private void OnInventoryChanged()
        {
            // Pickup de ammo ya se detecta en PollAmmo.
        }

        private void UpdateBaseVolume()
        {
            float target = baseVolume;
            if (tension < lowThreshold)
            {
                // Tension baja -> baja volumen del base.
                target = baseVolume * Mathf.Lerp(0.3f, 1f, tension / lowThreshold);
            }
            _baseSource.volume = Mathf.MoveTowards(_baseSource.volume, target, Time.deltaTime * 0.3f);
        }

        private void UpdateSecondary()
        {
            // Detectar si el clip secundario termino naturalmente.
            if (_secondaryPlaying && !_secondarySource.isPlaying)
            {
                _secondaryPlaying = false;
                _secondaryTargetVolume = 0f;
                _nextSecondaryTime = Time.time + secondaryCooldown;
            }

            bool shouldPlaySecondary = tension < lowThreshold && Time.time >= _nextSecondaryTime;

            if (shouldPlaySecondary && !_secondaryPlaying)
            {
                if (secondaryClips != null && secondaryClips.Length > 0)
                {
                    var clip = secondaryClips[Random.Range(0, secondaryClips.Length)];
                    _secondarySource.clip = clip;
                    _secondarySource.volume = 0f;
                    _secondarySource.Play();
                    _secondaryPlaying = true;
                    _secondaryTargetVolume = secondaryMaxVolume;

                    // Ejecutar un ambiente secundario sube tension como decae (0.002/s).
                    tension = Mathf.Clamp01(tension + tensionDecayPerSecond * Time.deltaTime);
                }
            }
            else if (!shouldPlaySecondary && _secondaryPlaying)
            {
                // Tension subio -> empezar a relajar.
                if (!_relaxing)
                {
                    _relaxing = true;
                    _relaxTimer = relaxDelay;
                }
            }

            if (_relaxing)
            {
                _relaxTimer -= Time.deltaTime;
                if (_relaxTimer <= 0f)
                {
                    _secondaryTargetVolume = 0f;
                    _relaxing = false;
                }
            }

            if (_secondaryPlaying)
            {
                _secondarySource.volume = Mathf.MoveTowards(_secondarySource.volume, _secondaryTargetVolume, Time.deltaTime * secondaryFadeSpeed);
                if (_secondaryTargetVolume <= 0f && _secondarySource.volume <= 0.001f)
                {
                    _secondarySource.Stop();
                    _secondaryPlaying = false;
                    _nextSecondaryTime = Time.time + secondaryCooldown;
                }
            }
        }

        private void UpdateExtras()
        {
            if (Time.time < _nextExtraTime) return;
            if (extraClips == null || extraClips.Length == 0) return;

            var clip = extraClips[Random.Range(0, extraClips.Length)];
            _extraSource.PlayOneShot(clip, extraVolume);

            // Ejecutar un sonido extra sube tension en 0.05.
            tension = Mathf.Clamp01(tension + 0.05f);

            float cooldown = Random.Range(extraCooldownMin, extraCooldownMax);
            // Si tension baja, reduce el cooldown (mas extras).
            if (tension < lowThreshold) cooldown *= 0.5f;
            _nextExtraTime = Time.time + cooldown;
        }

        private void RefreshEnemies()
        {
            _enemies.Clear();
            if (enemySpawner != null)
            {
                foreach (var e in enemySpawner.Spawned)
                    if (e != null) _enemies.Add(e);
            }
            else
            {
                foreach (var e in FindObjectsOfType<EnemyBase>())
                    _enemies.Add(e);
            }
        }
    }
}
