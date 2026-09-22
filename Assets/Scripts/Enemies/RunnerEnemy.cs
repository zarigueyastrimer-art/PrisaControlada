using UnityEngine;

namespace PrisaControlada.Enemies
{
    public class RunnerEnemy : EnemyBase
    {
        [Header("Runner Behavior")]
        [SerializeField] protected float loseTargetTime = 5f;
        [SerializeField] private float watchDuration = 5f;

        private enum RunnerState { Idle, Watch, Chase }
        private RunnerState _state = RunnerState.Idle;
        private float _watchTimer;
        private float _loseTimer;

        protected override void Awake()
        {
            base.Awake();
        }

        public override void ApplyDefaultStats()
        {
            base.ApplyDefaultStats();
            maxHealth = 40;
            moveSpeed = 4.6f;
            contactDamage = 20;
            attackCooldown = 0.9f;
            viewRange = 22f;
        }

        protected override void Update()
        {
            if (IsDead || Player == null) return;

            base.Update();

            bool seen = CanSeePlayer() || PlayerCloseEnough();

            switch (_state)
            {
                case RunnerState.Idle:
                    if (seen)
                    {
                        _state = RunnerState.Watch;
                        _watchTimer = 0f;
                        StopMoving();
                    }
                    break;

                case RunnerState.Watch:
                    if (!seen)
                    {
                        _state = RunnerState.Idle;
                        break;
                    }

                    FacePlayer();
                    _watchTimer += Time.deltaTime;
                    if (_watchTimer >= watchDuration)
                        _state = RunnerState.Chase;
                    break;

                case RunnerState.Chase:
                    if (seen)
                        _loseTimer = 0f;
                    else
                    {
                        _loseTimer += Time.deltaTime;
                        if (_loseTimer >= loseTargetTime)
                        {
                            _state = RunnerState.Idle;
                            break;
                        }
                    }

                    if (PlayerWithinAttackRange())
                    {
                        FacePlayer();
                        TryAttack();
                    }
                    else
                    {
                        MoveTowards(Player.transform.position, moveSpeed);
                        TryAttack();
                    }
                    break;
            }
        }

        private bool PlayerCloseEnough()
        {
            if (Player == null) return false;
            return Vector3.Distance(transform.position, Player.transform.position) <= attackRange;
        }

        private bool PlayerWithinAttackRange()
        {
            return Player != null
                && Vector3.Distance(transform.position, Player.transform.position) <= attackRange;
        }

        private void FacePlayer()
        {
            if (Player == null) return;

            StopMoving();

            Vector3 flat = Player.transform.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;

            Quaternion look = Quaternion.LookRotation(flat.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }
    }
}