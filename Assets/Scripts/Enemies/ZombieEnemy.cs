using UnityEngine;

namespace PrisaControlada.Enemies
{
    public class ZombieEnemy : EnemyBase
    {
        private enum ZombieState { Idle, Chase, Search }

        [Header("Zombie Behavior")]
        [SerializeField] protected float loseTargetTime = 5f;

        [Header("Search")]
        [SerializeField] private float searchRadius = 6f;
        [SerializeField] private float searchWaitMin = 1f;
        [SerializeField] private float searchWaitMax = 3f;

        [Header("Stagger")]
        [SerializeField] private float staggerAmplitude = 1.8f;
        [SerializeField] private float staggerFrequency = 1.5f;

        private ZombieState _state = ZombieState.Idle;
        private float _loseTimer;
        private float _staggerSeed;

        private Vector3 _searchOrigin;
        private Vector3 _searchPoint;
        private float _searchWaitUntil;
        private bool _isSearchWaiting;

        protected override void Awake()
        {
            base.Awake();
            _staggerSeed = Random.Range(0f, 100f);
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

            bool seen = CanSeePlayer() || PlayerCloseEnough();

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

        private bool PlayerCloseEnough()
        {
            if (Player == null) return false;
            return Vector3.Distance(transform.position, Player.transform.position) <= attackRange;
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
    }
}