using UnityEngine;

namespace PrisaControlada.Enemies
{
    public class ZombieEnemy : EnemyBase
    {
        private enum ZombieState { Idle, Chase, Search }

        [Header("Zombie Behavior")]
        [SerializeField] protected float loseTargetTime = 3f;

        [Header("Search")]
        [SerializeField] private float searchRadius = 6f;
        [SerializeField] private float searchWaitMin = 1f;
        [SerializeField] private float searchWaitMax = 3f;

        private ZombieState _state = ZombieState.Idle;
        private float _loseTimer;
        private Vector3 _lastKnownPos;

        private Vector3 _searchOrigin;
        private Vector3 _searchPoint;
        private float _searchWaitUntil;
        private bool _isSearchWaiting;

        protected override void Awake()
        {
            base.Awake();
            maxHealth = 80;
            moveSpeed = 2.2f;
            contactDamage = 25;
            attackCooldown = 1.2f;
            viewRange = 18f;
        }

        protected override void Update()
        {
            if (IsDead || Player == null) return;

            base.Update();

            bool seen = CanSeePlayer();

            if (seen)
            {
                _state = ZombieState.Chase;
                _loseTimer = 0f;
                _lastKnownPos = Player.transform.position;
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
                    MoveTowards(_lastKnownPos, moveSpeed);
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
    }
}