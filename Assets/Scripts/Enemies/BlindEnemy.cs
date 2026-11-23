using System.Collections.Generic;
using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Enemies
{
    //TP2 - Agustín Labarque
    public class BlindEnemy : EnemyBase
    {
        private enum BlindState { Wander, Stalk, Chase }

        [Header("Blind / Hearing")]
        [SerializeField] private float hearingRange = 16f;
        [SerializeField] private float minHeardLevel = 0.18f;
        [SerializeField] private float noiseMemorySeconds = 2f;
        [SerializeField] private float wanderSpeed = 1.1f;
        [SerializeField] private float stalkSpeed = 1.6f;
        [SerializeField] private float chaseSpeed = 2.8f;
        [SerializeField] private float loseSoundTime = 4f;
        [SerializeField] private float wanderRadius = 6f;
        [SerializeField] private float wanderDelayMin = 2f;
        [SerializeField] private float wanderDelayMax = 5f;

        [Header("First Encounter")]
        [SerializeField] private float teleportDistance = 2.5f;
        [SerializeField, Range(1f, 180f)] private float viewEscapeAngle = 75f;
        [SerializeField, Range(0f, 1f)] private float screamHeardLevel = 0.8f;

        [Header("Audio (3D loop)")]
        [SerializeField] private AudioClip blindExtraClip;
        [SerializeField, Range(0f, 1f)] private float blindExtraVolume = 1f;
        [SerializeField] private float blindExtraMinDistance = 2f;
        [SerializeField] private float blindExtraMaxDistance = 20f;

        [Header("Voice (one-shot)")]
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioClip hearYouClip;

        private AudioSource _extraSource;

        private MicNoiseMeter _mic;
        private BlindState _state = BlindState.Wander;
        private bool _warned;
        private float _soundSilenceTimer;
        private Vector3 _lastSoundPos;
        private float _loseTimer;
        private Vector3 _wanderTarget;
        private float _nextWanderTime;
        private Animator _animator;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        // El struct compartido NoiseStamp conserva tiempo, nivel y posicion de
        // cada ruido percibido sin acoplar el estado al mundo del juego.
        private readonly List<NoiseStamp> _noiseStamps = new List<NoiseStamp>();

        public bool Warned => _warned;

        protected override void Awake()
        {
            base.Awake();
            _animator = GetComponentInChildren<Animator>();

            _extraSource = gameObject.AddComponent<AudioSource>();
            _extraSource.playOnAwake = false;
            _extraSource.loop = true;
            _extraSource.spatialBlend = 1f;
            _extraSource.rolloffMode = AudioRolloffMode.Linear;
            _extraSource.minDistance = blindExtraMinDistance;
            _extraSource.maxDistance = blindExtraMaxDistance;
            _extraSource.dopplerLevel = 0f;
            _extraSource.volume = blindExtraVolume;
            _extraSource.clip = blindExtraClip;
            if (blindExtraClip != null)
                _extraSource.Play();
        }

        public override void ApplyDefaultStats()
        {
            maxHealth = 300;
            moveSpeed = 1.6f;
            contactDamage = 40;
            attackCooldown = 0.8f;
            viewRange = 0f;
            ResetHealthToMax();
        }

        protected override void Start()
        {
            base.Start();
            if (Player != null)
            {
                _mic = Player.GetComponent<MicNoiseMeter>();
                Player.OnNoiseMade += HandlePlayerNoise;
            }
        }

        protected override void OnDestroy()
        {
            if (Player != null)
                Player.OnNoiseMade -= HandlePlayerNoise;
            base.OnDestroy();
        }

        private void HandlePlayerNoise(float level)
        {
            if (Player == null) return;
            _noiseStamps.Add(new NoiseStamp
            {
                time = Time.time,
                level = level,
                position = Player.transform.position
            });
        }

        protected override void Update()
        {
            if (IsDead) return;

            base.Update();

            PruneNoise();
            float footstepLevel = GetFootstepLevel();
            float micLevel = GetMicLevel();
            bool isScreaming = micLevel >= screamHeardLevel;
            bool isHearing = footstepLevel >= minHeardLevel || micLevel >= minHeardLevel;
            bool routeBlocked = (isHearing || _state == BlindState.Stalk || _state == BlindState.Chase)
                && IsPursuitBlockedByClosedDoor();

            if (isScreaming || isHearing)
            {
                if (Player != null) _lastSoundPos = Player.transform.position;

                // Mientras una puerta cerrada bloquea la ruta, los temporizadores
                // de perdida quedan congelados: ni avanzan con el silencio ni se
                // reinician porque el jugador siga haciendo ruido.
                if (!routeBlocked)
                {
                    _soundSilenceTimer = 0f;
                    _loseTimer = 0f;

                    if (_state == BlindState.Wander)
                    {
                        if (isScreaming)
                            _state = _warned ? BlindState.Chase : BlindState.Stalk;
                        else
                            _state = BlindState.Chase;
                    }
                }
            }
            else if (!routeBlocked)
            {
                // El silencio solo avanza cuando la ruta no esta bloqueada por
                // una puerta. El tiempo de espera de Stalk queda congelado igual
                // que el timer de perdida de Chase.
                _soundSilenceTimer += Time.deltaTime;

                if (_state == BlindState.Chase)
                {
                    _loseTimer += Time.deltaTime;
                    if (_loseTimer >= loseSoundTime)
                    {
                        _state = BlindState.Wander;
                        PickWanderTarget();
                    }
                }
                else if (_state == BlindState.Stalk && _soundSilenceTimer >= noiseMemorySeconds)
                {
                    _state = BlindState.Wander;
                    PickWanderTarget();
                }
            }

            switch (_state)
            {
                case BlindState.Wander: UpdateWander(); break;
                case BlindState.Stalk: UpdateStalk(); break;
                case BlindState.Chase: UpdateChase(); break;
            }

            UpdateAnimation();
        }

        private void UpdateAnimation()
        {
            if (_animator == null) return;

            bool isMoving = Agent != null
                && Agent.isOnNavMesh
                && !Agent.isStopped
                && Agent.velocity.sqrMagnitude > 0.05f
                && !IsAttacking;

            _animator.SetFloat(SpeedHash, isMoving ? 1f : 0f, 0.1f, Time.deltaTime);
        }

        private void UpdateWander()
        {
            if (Time.time >= _nextWanderTime || Reached(_wanderTarget))
                PickWanderTarget();

            TryMoveTowardsPosition(_wanderTarget, wanderSpeed, false);
        }

        private void PickWanderTarget()
        {
            _nextWanderTime = Time.time + Random.Range(wanderDelayMin, wanderDelayMax);
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            _wanderTarget = transform.position + new Vector3(offset.x, 0f, offset.y);
        }

        private bool Reached(Vector3 point)
        {
            point.y = 0f;
            return (point - transform.position).sqrMagnitude < 1f;
        }

        private void UpdateStalk()
        {
            if (IsPursuitBlockedByClosedDoor())
            {
                StopMoving();
                return;
            }

            if (_lastSoundPos != Vector3.zero &&
                !TryMoveTowardsPosition(_lastSoundPos, stalkSpeed))
            {
                return;
            }

            if (!PlayerIsLookingAt(transform.position, viewEscapeAngle))
            {
                // Si el teletransporte no puede hacerse sin cruzar una puerta,
                // el Blind conserva el estado Stalk y reintenta mas tarde.
                if (!TeleportBehindPlayer())
                    return;

                _warned = true;
                _state = BlindState.Wander;
                PickWanderTarget();
            }
        }

        private void UpdateChase()
        {
            if (Player == null)
            {
                StopMoving();
                return;
            }

            if (!TryMoveTowardsPlayer(chaseSpeed))
                return;

            TryAttack();
        }

        private bool TeleportBehindPlayer()
        {
            if (Player == null || Agent == null || !Agent.isOnNavMesh)
                return false;

            Vector3 behind = Player.transform.position - Player.transform.forward * teleportDistance;
            Vector3 jitter = Random.insideUnitSphere * 0.3f;
            behind.x += jitter.x;
            behind.z += jitter.z;
            behind.y = Player.transform.position.y;

            StopMoving();

            // El Warp es un salto recto. Se valida el punto exacto sobre el
            // NavMesh y se rechaza cualquier salto que atraviese una puerta,
            // tanto desde el enemigo como desde el lado del jugador.
            if (!TryGetValidatedNavMeshDestination(behind, 0.75f, out Vector3 validatedTarget))
                return false;

            if (IsDirectSegmentBlockedByClosedDoor(transform.position, validatedTarget))
                return false;

            if (IsDirectSegmentBlockedByClosedDoor(Player.transform.position, validatedTarget))
                return false;

            if (!Agent.Warp(validatedTarget))
                return false;

            Vector3 dir = Player.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir);

            if (hearYouClip != null && voiceSource != null)
                voiceSource.PlayOneShot(hearYouClip);

            Debug.Log("[BlindEnemy] I can hear you");
            return true;
        }

        private void PruneNoise()
        {
            for (int i = _noiseStamps.Count - 1; i >= 0; i--)
            {
                if (Time.time - _noiseStamps[i].time > noiseMemorySeconds)
                    _noiseStamps.RemoveAt(i);
            }
        }

        private float GetFootstepLevel()
        {
            float level = 0f;

            foreach (NoiseStamp stamp in _noiseStamps)
            {
                float dist = Vector3.Distance(transform.position, stamp.position);
                float falloff = Mathf.Clamp01(1f - dist / hearingRange);
                level = Mathf.Max(level, stamp.level * falloff);
            }

            return level;
        }

        private float GetMicLevel()
        {
            if (_mic == null || Player == null) return 0f;

            float micLevel = _mic.CurrentLevel;
            if (micLevel <= 0f) return 0f;

            float dist = Vector3.Distance(transform.position, Player.transform.position);
            float falloff = Mathf.Clamp01(1f - dist / hearingRange);
            return micLevel * falloff;
        }
    }
}