using System.Collections.Generic;
using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Enemies
{
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
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioClip hearYouClip;

        private MicNoiseMeter _mic;
        private BlindState _state = BlindState.Wander;
        private bool _warned;
        private float _lastHeardTime = -999f;
        private Vector3 _lastSoundPos;
        private float _loseTimer;
        private Vector3 _wanderTarget;
        private float _nextWanderTime;

        private readonly List<NoiseStamp> _noiseStamps = new List<NoiseStamp>();

        private struct NoiseStamp
        {
            public float time;
            public float level;
            public Vector3 position;
        }

        public bool Warned => _warned;

        protected override void Awake()
        {
            base.Awake();
        }

        public override void ApplyDefaultStats()
        {
            base.ApplyDefaultStats();
            maxHealth = 300;
            moveSpeed = 1.6f;
            contactDamage = 40;
            attackCooldown = 0.8f;
            viewRange = 0f;
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
            float heard = GetHeardLevel();
            bool isHearing = heard >= minHeardLevel;

            if (isHearing)
            {
                _lastHeardTime = Time.time;
                if (Player != null) _lastSoundPos = Player.transform.position;
                _loseTimer = 0f;

                if (_state == BlindState.Wander)
                {
                    _state = _warned ? BlindState.Chase : BlindState.Stalk;
                }
            }
            else
            {
                if (_state == BlindState.Chase)
                {
                    _loseTimer += Time.deltaTime;
                    if (_loseTimer >= loseSoundTime)
                    {
                        _state = BlindState.Wander;
                        PickWanderTarget();
                    }
                }
                else if (_state == BlindState.Stalk && Time.time - _lastHeardTime > noiseMemorySeconds)
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
        }

        private void UpdateWander()
        {
            if (Time.time >= _nextWanderTime || Reached(_wanderTarget))
                PickWanderTarget();

            MoveTowards(_wanderTarget, wanderSpeed, false);
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
            if (_lastSoundPos != Vector3.zero)
                MoveTowards(_lastSoundPos, stalkSpeed);

            if (!PlayerIsLookingAt(transform.position, viewEscapeAngle))
            {
                TeleportBehindPlayer();
                _warned = true;
                _state = BlindState.Wander;
                PickWanderTarget();
            }
        }

        private void UpdateChase()
        {
            if (Player == null) return;
            MoveTowards(Player.transform.position, chaseSpeed);
            TryAttack();
        }

        private void TeleportBehindPlayer()
        {
            if (Player == null) return;

            Vector3 behind = Player.transform.position - Player.transform.forward * teleportDistance;
            behind += Random.insideUnitSphere * 0.3f;

            StopMoving();

            if (Physics.Raycast(behind + Vector3.up * 10f, Vector3.down,
                    out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                behind.y = hit.point.y + 0.05f;

            Agent.Warp(behind);

            Vector3 dir = Player.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir);

            if (hearYouClip != null && voiceSource != null)
                voiceSource.PlayOneShot(hearYouClip);

            Debug.Log("[BlindEnemy] I can hear you");
        }

        private void PruneNoise()
        {
            for (int i = _noiseStamps.Count - 1; i >= 0; i--)
            {
                if (Time.time - _noiseStamps[i].time > noiseMemorySeconds)
                    _noiseStamps.RemoveAt(i);
            }
        }

        private float GetHeardLevel()
        {
            float level = 0f;

            foreach (NoiseStamp stamp in _noiseStamps)
            {
                float dist = Vector3.Distance(transform.position, stamp.position);
                float falloff = Mathf.Clamp01(1f - dist / hearingRange);
                level = Mathf.Max(level, stamp.level * falloff);
            }

            if (_mic != null && Player != null)
            {
                float micLevel = _mic.CurrentLevel;
                if (micLevel > 0f)
                {
                    float dist = Vector3.Distance(transform.position, Player.transform.position);
                    float falloff = Mathf.Clamp01(1f - dist / hearingRange);
                    level = Mathf.Max(level, micLevel * falloff);
                }
            }

            return level;
        }
    }
}