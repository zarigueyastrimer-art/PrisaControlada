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

        [Header("Vision")]
        [SerializeField] protected float viewRange = 16f;
        [SerializeField, Range(0f, 360f)] protected float viewAngle = 100f;
        [SerializeField] protected LayerMask visionMask = ~0;

        protected NavMeshAgent Agent;
        protected PlayerController Player;
        protected PlayerHealth PlayerHealth;
        protected Transform PlayerHead;

        protected int CurrentHealth;
        protected float NextAttackTime;
        protected bool IsDead;

        private bool _isAttacking;
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
        }

        protected virtual void OnDestroy() { }

        public virtual void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (CurrentHealth <= 0)
                Die();
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
                Agent.isStopped = true;
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
            if (Agent == null) return;
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
            }
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