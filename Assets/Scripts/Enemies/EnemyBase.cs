using PrisaControlada.Player;
using UnityEngine;
using UnityEngine.AI;

namespace PrisaControlada.Enemies
{
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class EnemyBase : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] protected int maxHealth = 100;
        [SerializeField] protected float moveSpeed = 2.2f;
        [SerializeField] protected float turnSpeed = 8f;
        [SerializeField] protected int contactDamage = 25;
        [SerializeField] protected float attackCooldown = 1.2f;
        [SerializeField] protected float attackRange = 1.5f;

        [Header("Attack Collider")]
        [SerializeField] protected float attackWindup = 0.5f;
        [SerializeField] protected float attackActiveTime = 0.2f;
        [SerializeField] protected float damageRadius = 0.6f;
        [SerializeField] protected float damageOffset = 0.7f;
        [SerializeField] protected float damageHeight = 1.1f;
        [SerializeField] private bool showAttackDebug = true;

        [Header("Headshot")]
        [SerializeField] protected float headshotRadius = 0.2f;
        [SerializeField] protected string headBoneName = "mixamorig:Head";

        [Header("Vision")]
        [SerializeField] protected float viewRange = 16f;
        [SerializeField, Range(0f, 360f)] protected float viewAngle = 100f;
        [SerializeField] protected LayerMask visionMask = ~0;

        [Header("Aggro (reacciona al recibir daño)")]
        [Tooltip("Si está activo, al recibir daño el enemigo persigue al jugador automáticamente")]
        [SerializeField] protected bool aggroOnDamage = true;
        [Tooltip("Segundos en modo aggro después de recibir un golpe (cada golpe renueva el tiempo)")]
        [SerializeField] protected float aggroDuration = 8f;

        [Header("Audio (3D loop)")]
        [SerializeField] protected AudioClip heartbeatClip;
        [SerializeField, Range(0f, 1f)] protected float heartbeatVolume = 1f;
        [SerializeField] protected float heartbeatMinDistance = 2f;
        [SerializeField] protected float heartbeatMaxDistance = 25f;
        [SerializeField] protected float heartbeatRolloff = 2f;

        [Header("Footsteps (3D)")]
        [SerializeField] protected AudioClip[] footstepClips;
        [SerializeField, Range(0f, 1f)] protected float footstepVolume = 1f;
        [SerializeField] protected float footstepInterval = 0.5f;
        [SerializeField] protected float footstepMinDistance = 2f;
        [SerializeField] protected float footstepMaxDistance = 15f;

        protected AudioSource HeartbeatSource;
        protected AudioSource FootstepSource;
        protected Transform PlayerTransform;
        protected float _nextFootstepTime;

        protected NavMeshAgent Agent;
        protected PlayerController Player;
        protected PlayerHealth PlayerHealth;
        protected Transform PlayerHead;

        protected int CurrentHealth;
        protected float NextAttackTime;
        protected bool IsDead;

        // Hasta cuándo permanece en modo aggro (recibió daño del jugador).
        protected float _aggroUntilTime = float.NegativeInfinity;

        protected bool _isAttacking;
        private float _attackTimer;
        private GameObject _activeDamageCollider;

        public int MaxHealth => maxHealth;
        public bool Dead => IsDead;
        public bool IsAttacking => _isAttacking;

        public virtual void ApplyDefaultStats() { }

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Agent.updateRotation = false;
            CurrentHealth = maxHealth;

            HeartbeatSource = GetComponent<AudioSource>();
            if (HeartbeatSource == null)
                HeartbeatSource = gameObject.AddComponent<AudioSource>();
            HeartbeatSource.playOnAwake = false;
            HeartbeatSource.loop = true;
            HeartbeatSource.spatialBlend = 1f;
            HeartbeatSource.rolloffMode = AudioRolloffMode.Linear;
            HeartbeatSource.minDistance = heartbeatMinDistance;
            HeartbeatSource.maxDistance = heartbeatMaxDistance;
            HeartbeatSource.dopplerLevel = 0f;
            HeartbeatSource.volume = heartbeatVolume;
            HeartbeatSource.clip = heartbeatClip;
            if (heartbeatClip != null)
                HeartbeatSource.Play();

            FootstepSource = gameObject.AddComponent<AudioSource>();
            FootstepSource.playOnAwake = false;
            FootstepSource.loop = false;
            FootstepSource.spatialBlend = 1f;
            FootstepSource.rolloffMode = AudioRolloffMode.Linear;
            FootstepSource.minDistance = footstepMinDistance;
            FootstepSource.maxDistance = footstepMaxDistance;
            FootstepSource.dopplerLevel = 0f;
            FootstepSource.volume = footstepVolume;
        }

        protected virtual void Start()
        {
            Player = FindObjectOfType<PlayerController>();
            if (Player != null)
            {
                PlayerHealth = Player.GetComponent<PlayerHealth>();
                Camera cam = Player.GetComponentInChildren<Camera>();
                if (cam != null) PlayerHead = cam.transform;
            }

            EnsureOnNavMesh();
        }

        protected virtual void Update()
        {
            UpdateAttack();
            UpdateFootsteps();
        }

        protected void UpdateFootsteps()
        {
            if (IsDead || Agent == null || !Agent.isOnNavMesh) return;

            float speed = Agent.velocity.magnitude;
            if (speed < 0.1f) return;

            if (Time.time >= _nextFootstepTime)
            {
                PlayFootstep();
                _nextFootstepTime = Time.time + footstepInterval;
            }
        }

        protected void PlayFootstep()
        {
            if (FootstepSource == null || footstepClips == null || footstepClips.Length == 0) return;

            var clip = footstepClips[Random.Range(0, footstepClips.Length)];
            FootstepSource.PlayOneShot(clip, footstepVolume);
        }

        protected virtual void OnDestroy() { }

        public virtual void TakeDamage(int amount, Vector3 hitDirection = default, Collider hitBone = null, Vector3 hitPoint = default)
        {
            if (IsDead || amount <= 0) return;

            // Al recibir daño el enemigo sabe dónde está el jugador y lo persigue,
            // aunque esté por detrás o fuera de su campo de visión.
            if (aggroOnDamage)
                _aggroUntilTime = Time.time + aggroDuration;

            // Headshot: si el hit point esta cerca del hueso de la cabeza, duplica el daño.
            if (IsHeadshot(hitPoint))
                amount *= 2;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (CurrentHealth <= 0)
                Die();
        }

        protected bool IsHeadshot(Vector3 hitPoint)
        {
            Transform head = FindHeadBone();
            if (head == null) return false;
            return Vector3.Distance(hitPoint, head.position) <= headshotRadius;
        }

        protected Transform FindHeadBone()
        {
            Transform body = transform.Find("Body");
            if (body == null)
            {
                Debug.LogWarning($"[Runner] FindHeadBone: no se encontró hijo 'Body' en {transform.name}");
                return null;
            }
            foreach (Transform t in body.GetComponentsInChildren<Transform>())
            {
                if (t.name == headBoneName) return t;
            }
            // Fallback: buscar cualquier hueso llamado "Head"
            foreach (Transform t in body.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Head") return t;
            }
            // Fallback: cualquier transform que contenga "Head"
            foreach (Transform t in body.GetComponentsInChildren<Transform>())
            {
                if (t.name.Contains("Head")) return t;
            }
            Debug.LogWarning($"[Runner] FindHeadBone: no se encontró hueso '{headBoneName}' ni 'Head' dentro de Body");
            return null;
        }

        protected virtual void Die()
        {
            if (IsDead) return;
            IsDead = true;
            Agent.enabled = false;
            enabled = false;
            Destroy(gameObject);
        }

        protected bool CanSeePlayer()
        {
            if (Player == null || PlayerHead == null || IsDead) return false;

            Vector3 toHead = PlayerHead.position - transform.position;
            float dist = toHead.magnitude;
            if (dist > viewRange) return false;

            if (Vector3.Angle(transform.forward, toHead) > viewAngle * 0.5f) return false;

            if (Physics.Raycast(transform.position + Vector3.up * 0.6f, toHead.normalized,
                    out RaycastHit hit, dist, visionMask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.transform == Player.transform
                    || hit.collider.transform.IsChildOf(Player.transform);
            }

            return true;
        }

        // Verdadero mientras el enemigo esté en modo aggro (recibió daño recientemente).
        protected bool IsAggroed => Time.time < _aggroUntilTime;

        protected bool PlayerCloseEnough()
        {
            if (Player == null) return false;
            return Vector3.Distance(transform.position, Player.transform.position) <= attackRange;
        }

        // Detección completa: lo ve, está a distancia de ataque o está en modo aggro.
        protected bool PlayerDetected()
        {
            return IsAggroed || CanSeePlayer() || PlayerCloseEnough();
        }

        protected bool EnsureOnNavMesh()
        {
            if (Agent == null) return false;
            if (Agent.isOnNavMesh) return true;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                Agent.Warp(hit.position);
                return Agent.isOnNavMesh;
            }

            return false;
        }

        protected void MoveTowards(Vector3 target, float speed, bool faceTarget = true)
        {
            if (Agent == null) return;

            if (_isAttacking)
            {
                if (Agent.isOnNavMesh) Agent.isStopped = true;
                return;
            }

            if (!EnsureOnNavMesh()) return;

            Agent.speed = speed;
            Agent.isStopped = false;

            if (faceTarget)
            {
                Vector3 flat = target - transform.position;
                flat.y = 0f;

                if (flat.sqrMagnitude > 0.0001f)
                {
                    Quaternion look = Quaternion.LookRotation(flat.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
                }
            }

            Agent.SetDestination(target);
        }

        protected void StopMoving()
        {
            if (Agent == null || !Agent.isOnNavMesh) return;
            Agent.isStopped = true;
            Agent.ResetPath();
        }

        protected void TryAttack()
        {
            if (Player == null || PlayerHealth == null || _isAttacking) return;

            float dist = Vector3.Distance(transform.position, Player.transform.position);
            if (dist <= attackRange && Time.time >= NextAttackTime)
            {
                _isAttacking = true;
                _attackTimer = attackWindup;
                TriggerAttackAnimation();
            }
        }

        // Dispara el trigger de ataque en el Animator del modelo (child "Body").
        protected void TriggerAttackAnimation()
        {
            var body = transform.Find("Body");
            if (body == null) return;
            var animator = body.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger("Attack");
        }

        private void UpdateAttack()
        {
            if (!_isAttacking) return;

            _attackTimer -= Time.deltaTime;

            if (_activeDamageCollider == null && _attackTimer <= 0f)
                SpawnDamageCollider();
            else if (_activeDamageCollider != null && _attackTimer <= -attackActiveTime)
            {
                Destroy(_activeDamageCollider);
                _activeDamageCollider = null;
                _isAttacking = false;
                NextAttackTime = Time.time + attackCooldown;
            }
        }

        private void SpawnDamageCollider()
        {
            GameObject go = new GameObject("AttackDamage");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, damageHeight, damageOffset);

            SphereCollider sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = damageRadius;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            DamageCollider dmg = go.AddComponent<DamageCollider>();
            dmg.Setup(PlayerHealth, contactDamage);
            _activeDamageCollider = go;

            if (showAttackDebug)
                AddAttackDebugVisual(go, sc);

            dmg.CheckOverlap();
        }

        private void AddAttackDebugVisual(GameObject parent, SphereCollider collider)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "AttackRangeDebug";
            visual.transform.SetParent(parent.transform, false);

            // Match the collider's effective world-space radius (the enemy scales x/z).
            Vector3 lossy = parent.transform.lossyScale;
            float worldRadius = collider.radius * Mathf.Max(lossy.x, lossy.z);
            float d = worldRadius * 2f;
            visual.transform.localScale = new Vector3(d / lossy.x, d / lossy.y, d / lossy.z);

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null) visualCollider.enabled = false;

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateDebugMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static Material _debugMat;

        private static Material CreateDebugMaterial()
        {
            if (_debugMat != null) return _debugMat;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            _debugMat = new Material(shader) { color = new Color(1f, 0.35f, 0f, 0.35f) };
            return _debugMat;
        }

        protected bool PlayerIsLookingAt(Vector3 worldPos, float maxAngle)
        {
            if (PlayerHead == null) return false;

            Vector3 to = worldPos - PlayerHead.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return true;

            return Vector3.Angle(PlayerHead.forward, to) <= maxAngle;
        }
    }
}