using UnityEngine;

namespace PrisaControlada.Enemies
{
    public class RunnerEnemy : EnemyBase
    {
        [Header("Runner Behavior")]
        [SerializeField] protected float loseTargetTime = 2.5f;

        protected bool HasTarget;
        protected float LoseTimer;
        protected Vector3 LastKnownPos;

        protected override void Awake()
        {
            base.Awake();
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

            bool seen = CanSeePlayer();

            if (seen)
            {
                HasTarget = true;
                LoseTimer = 0f;
                LastKnownPos = Player.transform.position;
            }
            else if (HasTarget)
            {
                LoseTimer += Time.deltaTime;
                if (LoseTimer >= loseTargetTime)
                    HasTarget = false;
            }

            if (HasTarget)
            {
                MoveTowards(LastKnownPos, moveSpeed);
                TryAttack();
            }
        }
    }
}