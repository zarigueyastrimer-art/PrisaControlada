using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Enemies
{
    public class ZombieEnemy : EnemyBase
    {
        private enum ZombieState { Idle, Chase, Search }

        [Header("Zombie Behavior")]
        [SerializeField] protected float loseTargetTime = 5f;

        [Header("Hearing (pasos + micrófono)")]
        [Tooltip("Distancia máxima a la que se oye el ruido más fuerte (correr). Caminar se oye a menos distancia y agachado no se oye")]
        [SerializeField] private float hearingRange = 16f;
        [Tooltip("Nivel mínimo de ruido para oírlo. Por debajo (ej. agachado) es inaudible")]
        [SerializeField] private float minHeardLevel = 0.18f;
        [Tooltip("Segundos que recuerda un ruido escuchado")]
        [SerializeField] private float noiseMemorySeconds = 2f;

        [Header("Audio (3D loop)")]
        [SerializeField] private AudioClip breathingClip;
        [SerializeField, Range(0f, 1f)] private float breathingVolume = 1f;
        [SerializeField] private float breathingMinDistance = 2f;
        [SerializeField] private float breathingMaxDistance = 20f;

        private AudioSource _breathingSource;

        [Header("Search")]
        [SerializeField] private float searchRadius = 6f;
        [SerializeField] private float searchWaitMin = 1f;
        [SerializeField] private float searchWaitMax = 3f;

        [Header("Stagger")]
        [SerializeField] private float staggerAmplitude = 1.8f;
        [SerializeField] private float staggerFrequency = 1.5f;

        [Header("Attack Animation")]
        [SerializeField] private float attackAnimDuration = 1.5f;

        [Header("Ragdoll")]
        [SerializeField] private float ragdollDestroyTime = 5f;
        [SerializeField] private float ragdollImpulse = 8f;

        [Header("Hit Reaction")]
        [SerializeField] private float hitReactForce = 0.5f;
        [SerializeField] private float hitReactDecaySpeed = 10f;

        private ZombieState _state = ZombieState.Idle;
        private float _loseTimer;
        private float _staggerSeed;
        private float _attackAnimTimer;
        private bool _wasAttacking;
        private bool _isSearchWaiting;
        private Vector3 _searchOrigin;
        private Vector3 _searchPoint;
        private float _searchWaitUntil;
        private Animator _animator;

        private MicNoiseMeter _mic;
        private readonly System.Collections.Generic.List<NoiseStamp> _noiseStamps = new System.Collections.Generic.List<NoiseStamp>();

        private struct NoiseStamp
        {
            public float time;
            public float level;
            public Vector3 position;
        }

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int CanExitAttackHash = Animator.StringToHash("CanExitAttack");

        protected override void Awake()
        {
            base.Awake();
            _staggerSeed = Random.Range(0f, 100f);
            _animator = GetComponentInChildren<Animator>();

            _breathingSource = gameObject.AddComponent<AudioSource>();
            _breathingSource.playOnAwake = false;
            _breathingSource.loop = true;
            _breathingSource.spatialBlend = 1f;
            _breathingSource.rolloffMode = AudioRolloffMode.Linear;
            _breathingSource.minDistance = breathingMinDistance;
            _breathingSource.maxDistance = breathingMaxDistance;
            _breathingSource.dopplerLevel = 0f;
            _breathingSource.volume = breathingVolume;
            _breathingSource.clip = breathingClip;
            if (breathingClip != null)
                _breathingSource.Play();
        }

        protected override void Start()
        {
            base.Start();

            // Escucha: pasos del jugador (OnNoiseMade) + micrófono (mismo sistema que el ciego).
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

        private System.Collections.Generic.Dictionary<Transform, Vector3> _boneOffsets = new System.Collections.Generic.Dictionary<Transform, Vector3>();
        private System.Collections.Generic.Dictionary<Transform, Vector3> _lastFramePositions = new System.Collections.Generic.Dictionary<Transform, Vector3>();
        private System.Collections.Generic.Dictionary<Transform, Vector3> _lastAppliedOffset = new System.Collections.Generic.Dictionary<Transform, Vector3>();
        private bool _lateUpdateRunning;

        private void LateUpdate()
        {
            if (_boneOffsets.Count == 0) return;
            _lateUpdateRunning = true;

            var snapshot = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<Transform, Vector3>>(_boneOffsets);
            _boneOffsets.Clear();

            foreach (var kv in snapshot)
            {
                Transform bone = kv.Key;
                Vector3 offset = kv.Value;

                if (bone == null) continue;

                bool hadLastFrame = _lastFramePositions.ContainsKey(bone);
                bool animatorChangedIt = hadLastFrame && bone.localPosition != _lastFramePositions[bone];

                Vector3 cleanBase;
                if (!hadLastFrame)
                {
                    cleanBase = bone.localPosition;
                }
                else if (animatorChangedIt)
                {
                    cleanBase = bone.localPosition;
                }
                else
                {
                    Vector3 oldApplied = _lastAppliedOffset.ContainsKey(bone) ? _lastAppliedOffset[bone] : Vector3.zero;
                    cleanBase = _lastFramePositions[bone] - oldApplied;
                }

                bone.localPosition = cleanBase + offset;
                _lastFramePositions[bone] = bone.localPosition;
                _lastAppliedOffset[bone] = offset;

                Vector3 newOffset = Vector3.Lerp(offset, Vector3.zero, Time.deltaTime * hitReactDecaySpeed);
                if (newOffset.sqrMagnitude < 0.0001f)
                {
                    _lastFramePositions.Remove(bone);
                    _lastAppliedOffset.Remove(bone);
                }
                else
                {
                    _boneOffsets[bone] = newOffset;
                }
            }

            _lateUpdateRunning = false;
        }

        private void ApplyHitReaction(Vector3 hitPoint, Vector3 hitDirection)
        {
            Transform body = transform.Find("Body");
            if (body == null) return;

            Transform closestBone = null;
            float closestDist = float.MaxValue;

            foreach (Transform bone in body.GetComponentsInChildren<Transform>())
            {
                if (bone == body) continue;
                float dist = Vector3.Distance(bone.position, hitPoint);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestBone = bone;
                }
            }

            if (closestBone != null && closestBone.parent != null)
            {
                Vector3 localDir = closestBone.parent.InverseTransformDirection(hitDirection.normalized);
                _boneOffsets[closestBone] = localDir * hitReactForce;
            }
        }

        public override void ApplyDefaultStats()
        {
            base.ApplyDefaultStats();
            maxHealth = 80;
            moveSpeed = 2.2f;
            contactDamage = 25;
            attackCooldown = 0.7f;
            attackRange = 2f;
            attackWindup = 0.25f;
            attackActiveTime = 0.35f;
            damageOffset = 0.95f;
            damageRadius = 0.7f;
            viewRange = 18f;
        }

        protected override void Update()
        {
            if (IsDead || Player == null) return;

            base.Update();

            if (_attackAnimTimer > 0f)
            {
                _attackAnimTimer -= Time.deltaTime;
                if (_attackAnimTimer <= 0f)
                {
                    _attackAnimTimer = 0f;
                    if (_animator != null)
                        _animator.SetBool(CanExitAttackHash, true);
                }
            }

            // Detecta al jugador: lo ve, está cerca, le pegó (aggro) o ESCUCHA su ruido.
            // Escucha depende del ruido: correr (1.0) se oye lejos, caminar (0.35) cerca,
            // agachado (0.12) nada; el micrófono aplica igual con caída por distancia.
            PruneNoise();
            bool seen = PlayerDetected() || HeardPlayer();

            if (seen)
            {
                _state = ZombieState.Chase;
                _loseTimer = 0f;
            }
            else if (_state == ZombieState.Chase)
            {
                _loseTimer += Time.deltaTime;
                if (_loseTimer >= loseTargetTime)
                    StartSearch();
            }

            UpdateAnimation();

            if (_attackAnimTimer > 0f)
            {
                StopMoving();
                return;
            }

            switch (_state)
            {
                case ZombieState.Chase:
                    MoveTowards(GetStaggerTarget(Player.transform.position), moveSpeed);
                    TryAttack();
                    break;
                case ZombieState.Search:
                    UpdateSearch();
                    break;
            }
        }

        public override void TakeDamage(int amount, Vector3 hitDirection = default, Collider hitBone = null, Vector3 hitPoint = default)
        {
            base.TakeDamage(amount, hitDirection, hitBone, hitPoint);
            if (!IsDead)
            {
                _attackAnimTimer = 0f;
                if (_animator != null)
                {
                    _animator.ResetTrigger(AttackHash);
                    _animator.SetBool(CanExitAttackHash, true);
                }

                if (hitBone != null)
                    ApplyHitReaction(hitPoint, hitDirection);
            }
        }

        protected override void Die()
        {
            if (IsDead) return;
            IsDead = true;

            Agent.enabled = false;
            enabled = false;

            if (_animator != null)
                _animator.enabled = false;

            foreach (var c in GetComponentsInChildren<CharacterJoint>()) Destroy(c);
            foreach (var c in GetComponentsInChildren<Rigidbody>()) Destroy(c);
            foreach (var c in GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var c in GetComponentsInChildren<RagdollComponent>()) Destroy(c);
            foreach (var c in GetComponentsInChildren<Ragdoll>()) Destroy(c);

            Vector3 pushDir = (transform.position - Player.transform.position).normalized;
            pushDir.y = 0f;

            StartCoroutine(BuildRagdollNextFrame(pushDir));
            Destroy(gameObject, ragdollDestroyTime);
        }

        private System.Collections.IEnumerator BuildRagdollNextFrame(Vector3 pushDir)
        {
            yield return new WaitForEndOfFrame();

            string[] boneNames = {
                "mixamorig:Hips",
                "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2",
                "mixamorig:Neck", "mixamorig:Head",
                "mixamorig:LeftShoulder", "mixamorig:LeftArm", "mixamorig:LeftForeArm", "mixamorig:LeftHand",
                "mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand",
                "mixamorig:LeftUpLeg", "mixamorig:LeftLeg", "mixamorig:LeftFoot",
                "mixamorig:RightUpLeg", "mixamorig:RightLeg", "mixamorig:RightFoot"
            };

            Transform body = transform.Find("Body");
            if (body == null) yield break;

            var bones = new System.Collections.Generic.Dictionary<string, Rigidbody>();
            var transforms = new System.Collections.Generic.Dictionary<string, Transform>();

            foreach (string boneName in boneNames)
            {
                Transform bone = FindDeep(body, boneName);
                if (bone == null) continue;
                transforms[boneName] = bone;

                float mass;
                if (boneName.Contains("Hips")) mass = 4f;
                else if (boneName.Contains("Head")) mass = 1.5f;
                else if (boneName.Contains("Spine")) mass = 2f;
                else if (boneName.Contains("UpLeg") || boneName.Contains("Shoulder")) mass = 1.2f;
                else if (boneName.Contains("Hand")) mass = 0.3f;
                else mass = 0.8f;

                Rigidbody rb = bone.gameObject.AddComponent<Rigidbody>();
                rb.mass = mass;
                rb.linearDamping = 0.1f;
                rb.angularDamping = 0.3f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                bones[boneName] = rb;
            }

            foreach (var kv in bones)
            {
                Collider col = kv.Value.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Transform t = transforms[kv.Key];
                string name = kv.Key;

                CapsuleCollider capsule = t.gameObject.AddComponent<CapsuleCollider>();
                capsule.radius = GetBoneRadius(name);

                if (transforms.ContainsKey(GetParentBone(name)))
                {
                    Transform parentT = transforms[GetParentBone(name)];
                    float dist = Vector3.Distance(t.position, parentT.position);
                    capsule.height = Mathf.Max(dist, 0.01f);
                    Vector3 localDir = t.InverseTransformDirection((parentT.position - t.position).normalized);
                    int axis = 0;
                    if (Mathf.Abs(localDir.y) > Mathf.Abs(localDir.x) && Mathf.Abs(localDir.y) > Mathf.Abs(localDir.z)) axis = 1;
                    else if (Mathf.Abs(localDir.z) > Mathf.Abs(localDir.x)) axis = 2;
                    capsule.direction = axis;
                    capsule.center = Vector3.zero;
                }
                else
                {
                    capsule.height = capsule.radius * 2f;
                    capsule.direction = 1;
                }
            }

            string[][] connections = {
                new[] { "mixamorig:Hips", "mixamorig:Spine" },
                new[] { "mixamorig:Spine", "mixamorig:Spine1" },
                new[] { "mixamorig:Spine1", "mixamorig:Spine2" },
                new[] { "mixamorig:Spine2", "mixamorig:Neck" },
                new[] { "mixamorig:Neck", "mixamorig:Head" },
                new[] { "mixamorig:Spine2", "mixamorig:LeftShoulder" },
                new[] { "mixamorig:LeftShoulder", "mixamorig:LeftArm" },
                new[] { "mixamorig:LeftArm", "mixamorig:LeftForeArm" },
                new[] { "mixamorig:LeftForeArm", "mixamorig:LeftHand" },
                new[] { "mixamorig:Spine2", "mixamorig:RightShoulder" },
                new[] { "mixamorig:RightShoulder", "mixamorig:RightArm" },
                new[] { "mixamorig:RightArm", "mixamorig:RightForeArm" },
                new[] { "mixamorig:RightForeArm", "mixamorig:RightHand" },
                new[] { "mixamorig:Hips", "mixamorig:LeftUpLeg" },
                new[] { "mixamorig:LeftUpLeg", "mixamorig:LeftLeg" },
                new[] { "mixamorig:LeftLeg", "mixamorig:LeftFoot" },
                new[] { "mixamorig:Hips", "mixamorig:RightUpLeg" },
                new[] { "mixamorig:RightUpLeg", "mixamorig:RightLeg" },
                new[] { "mixamorig:RightLeg", "mixamorig:RightFoot" }
            };

            foreach (var conn in connections)
            {
                if (!bones.ContainsKey(conn[0]) || !bones.ContainsKey(conn[1])) continue;
                Rigidbody childRb = bones[conn[1]];
                Rigidbody parentRb = bones[conn[0]];

                CharacterJoint joint = childRb.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = parentRb;
                joint.enablePreprocessing = false;
                joint.autoConfigureConnectedAnchor = true;
                joint.lowTwistLimit = new SoftJointLimit { limit = -30f };
                joint.highTwistLimit = new SoftJointLimit { limit = 30f };
                joint.swing1Limit = new SoftJointLimit { limit = 60f };
                joint.swing2Limit = new SoftJointLimit { limit = 30f };
                joint.breakForce = float.MaxValue;
                joint.breakTorque = float.MaxValue;

                Physics.IgnoreCollision(childRb.GetComponent<Collider>(), parentRb.GetComponent<Collider>(), true);
            }

            foreach (var kv in bones)
                kv.Value.AddForce(pushDir * ragdollImpulse, ForceMode.Impulse);
        }

        private static float GetBoneRadius(string boneName)
        {
            if (boneName.Contains("Hips")) return 0.08f;
            if (boneName.Contains("Head")) return 0.06f;
            if (boneName.Contains("Spine") || boneName.Contains("Neck")) return 0.05f;
            if (boneName.Contains("UpLeg")) return 0.05f;
            if (boneName.Contains("Leg") || boneName.Contains("Arm")) return 0.03f;
            if (boneName.Contains("Foot")) return 0.04f;
            if (boneName.Contains("Hand")) return 0.02f;
            return 0.03f;
        }

        private static string GetParentBone(string boneName)
        {
            switch (boneName)
            {
                case "mixamorig:Spine": return "mixamorig:Hips";
                case "mixamorig:Spine1": return "mixamorig:Spine";
                case "mixamorig:Spine2": return "mixamorig:Spine1";
                case "mixamorig:Neck": return "mixamorig:Spine2";
                case "mixamorig:Head": return "mixamorig:Neck";
                case "mixamorig:LeftShoulder": return "mixamorig:Spine2";
                case "mixamorig:LeftArm": return "mixamorig:LeftShoulder";
                case "mixamorig:LeftForeArm": return "mixamorig:LeftArm";
                case "mixamorig:LeftHand": return "mixamorig:LeftForeArm";
                case "mixamorig:RightShoulder": return "mixamorig:Spine2";
                case "mixamorig:RightArm": return "mixamorig:RightShoulder";
                case "mixamorig:RightForeArm": return "mixamorig:RightArm";
                case "mixamorig:RightHand": return "mixamorig:RightForeArm";
                case "mixamorig:LeftUpLeg": return "mixamorig:Hips";
                case "mixamorig:LeftLeg": return "mixamorig:LeftUpLeg";
                case "mixamorig:LeftFoot": return "mixamorig:LeftLeg";
                case "mixamorig:RightUpLeg": return "mixamorig:Hips";
                case "mixamorig:RightLeg": return "mixamorig:RightUpLeg";
                case "mixamorig:RightFoot": return "mixamorig:RightLeg";
                default: return "";
            }
        }

        private void UpdateAnimation()
        {
            if (_animator == null) return;

            if (IsAttacking && !_wasAttacking && _attackAnimTimer <= 0f)
            {
                _animator.SetBool(CanExitAttackHash, false);
                _animator.SetTrigger(AttackHash);
                _attackAnimTimer = attackAnimDuration;
            }
            _wasAttacking = IsAttacking;

            bool isMoving = _state == ZombieState.Chase
                || (_state == ZombieState.Search && !_isSearchWaiting);

            if (_attackAnimTimer > 0f)
                isMoving = false;

            _animator.SetFloat(SpeedHash, isMoving ? 1f : 0f, 0.1f, Time.deltaTime);
        }

        private void StartSearch()
        {
            _state = ZombieState.Search;
            _searchOrigin = transform.position;
            _searchWaitUntil = Time.time + Random.Range(searchWaitMin, searchWaitMax);
            _isSearchWaiting = true;
            StopMoving();
        }

        private void UpdateSearch()
        {
            if (_isSearchWaiting)
            {
                if (Time.time >= _searchWaitUntil)
                {
                    _searchPoint = _searchOrigin
                        + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * searchRadius;
                    _isSearchWaiting = false;
                }
            }
            else
            {
                MoveTowards(_searchPoint, moveSpeed);
                if (Reached(_searchPoint))
                {
                    _searchWaitUntil = Time.time + Random.Range(searchWaitMin, searchWaitMax);
                    _isSearchWaiting = true;
                }
            }
        }

        private bool Reached(Vector3 point)
        {
            point.y = 0f;
            return (point - transform.position).sqrMagnitude < 1f;
        }

        // ---- Escucha (mismo sistema que BlindEnemy) ----

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

        private void PruneNoise()
        {
            for (int i = _noiseStamps.Count - 1; i >= 0; i--)
            {
                if (Time.time - _noiseStamps[i].time > noiseMemorySeconds)
                    _noiseStamps.RemoveAt(i);
            }
        }

        // Nivel de ruido de los pasos: depende del tipo de paso y la distancia al origen.
        private float GetFootstepLevel()
        {
            float level = 0f;
            Vector3 myPos = transform.position;

            foreach (NoiseStamp stamp in _noiseStamps)
            {
                float dist = Vector3.Distance(myPos, stamp.position);
                float falloff = Mathf.Clamp01(1f - dist / hearingRange);
                level = Mathf.Max(level, stamp.level * falloff);
            }

            return level;
        }

        // Nivel del micrófono del jugador con caída por distancia.
        private float GetMicLevel()
        {
            if (_mic == null || Player == null) return 0f;

            float micLevel = _mic.CurrentLevel;
            if (micLevel <= 0f) return 0f;

            float dist = Vector3.Distance(transform.position, Player.transform.position);
            float falloff = Mathf.Clamp01(1f - dist / hearingRange);
            return micLevel * falloff;
        }

        private bool HeardPlayer()
        {
            if (IsDead || Player == null) return false;
            return GetFootstepLevel() >= minHeardLevel || GetMicLevel() >= minHeardLevel;
        }

        private Vector3 GetStaggerTarget(Vector3 target)
        {
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < 0.05f) return target;

            Vector3 perp = Vector3.Cross(toTarget / distance, Vector3.up).normalized;
            float wobble = staggerAmplitude * (Mathf.PerlinNoise(_staggerSeed, Time.time * staggerFrequency) * 2f - 1f);
            float fade = Mathf.Clamp01(distance / (attackRange * 3f));
            return target + perp * wobble * fade;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
