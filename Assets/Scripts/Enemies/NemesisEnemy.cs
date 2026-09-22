using UnityEngine;

namespace PrisaControlada.Enemies
{
    public class NemesisEnemy : EnemyBase
    {
        [Header("Nemesis / Patrol")]
        [SerializeField] private float patrolSpeed = 1.4f;
        [SerializeField] private float patrolRadius = 25f;
        [SerializeField] private float proximityBoost = 3f;
        [SerializeField] private float proximityDistance = 10f;
        [SerializeField] private float progressSpeedFactor = 1.5f;
        [SerializeField] private float waypointRadius = 1.5f;
        [SerializeField] private float waitMin = 0.8f;
        [SerializeField] private float waitMax = 2.5f;

        private float _progress;
        private Vector3 _target;
        private float _waitUntil;

        public float Progress
        {
            get => _progress;
            set => _progress = Mathf.Clamp01(value);
        }

        protected override void Awake()
        {
            base.Awake();
        }

        public override void ApplyDefaultStats()
        {
            base.ApplyDefaultStats();
            maxHealth = int.MaxValue;
            moveSpeed = patrolSpeed;
            contactDamage = 9999;
            attackRange = 1.6f;
            attackCooldown = 0.5f;
        }

        public override void TakeDamage(int amount, Vector3 hitDirection = default, Collider hitBone = null, Vector3 hitPoint = default)
        {
        }

        protected override void Die()
        {
        }

        protected override void Update()
        {
            if (IsDead) return;

            base.Update();

            if (CanSeePlayer() && Player != null)
            {
                _target = Player.transform.position;
            }
            else if (Time.time >= _waitUntil && Reached(_target))
            {
                PickTarget();
            }

            float dist = Player != null
                ? Vector3.Distance(transform.position, Player.transform.position)
                : proximityDistance;

            float proximity = Mathf.Clamp01(1f - dist / proximityDistance);
            float speed = patrolSpeed * (1f + proximity * proximityBoost) * (1f + _progress * progressSpeedFactor);

            MoveTowards(_target, speed);
            TryAttack();
        }

        private void PickTarget()
        {
            Vector2 offset = Random.insideUnitCircle * patrolRadius;
            _target = transform.position + new Vector3(offset.x, 0f, offset.y);
            _waitUntil = Time.time + Random.Range(waitMin, waitMax);
        }

        private bool Reached(Vector3 point)
        {
            point.y = 0f;
            return (point - transform.position).sqrMagnitude < waypointRadius * waypointRadius;
        }
    }
}