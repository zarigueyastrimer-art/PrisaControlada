using UnityEngine;

namespace PrisaControlada.Enemies
{
    //TP2 - Agustín Labarque
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
        private Vector3 _lastPlayerTarget;
        private float _waitUntil;
        private bool _isPursuingPlayer;
        private bool _waitingForDoor;

        [Header("Audio (3D loop)")]
        [SerializeField] private AudioClip nemesisClip;
        [SerializeField, Range(0f, 1f)] private float nemesisVolume = 1f;
        [SerializeField] private float nemesisMinDistance = 2f;
        [SerializeField] private float nemesisMaxDistance = 25f;

        private AudioSource _nemesisSource;

        public float Progress
        {
            get => _progress;
            set => _progress = Mathf.Clamp01(value);
        }

        public bool IsPursuingPlayer => _isPursuingPlayer;
        public bool IsWaitingForDoor => _waitingForDoor;
        public Vector3 LastPlayerTarget => _lastPlayerTarget;

        protected override void Awake()
        {
            base.Awake();
            _nemesisSource = gameObject.AddComponent<AudioSource>();
            _nemesisSource.playOnAwake = false;
            _nemesisSource.loop = true;
            _nemesisSource.spatialBlend = 1f;
            _nemesisSource.rolloffMode = AudioRolloffMode.Linear;
            _nemesisSource.minDistance = nemesisMinDistance;
            _nemesisSource.maxDistance = nemesisMaxDistance;
            _nemesisSource.dopplerLevel = 0f;
            _nemesisSource.volume = nemesisVolume;
            _nemesisSource.clip = nemesisClip;
            if (nemesisClip != null)
                _nemesisSource.Play();
        }

        protected override void Start()
        {
            base.Start();
            PickTarget();
        }

        public override void ApplyDefaultStats()
        {
            maxHealth = int.MaxValue;
            moveSpeed = patrolSpeed;
            contactDamage = 9999;
            attackRange = 1.6f;
            attackCooldown = 0.5f;
            ResetHealthToMax();
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

            bool seesPlayer = Player != null && CanSeePlayer();
            if (seesPlayer)
            {
                _lastPlayerTarget = Player.transform.position;
                _isPursuingPlayer = true;
                _waitingForDoor = false;
                _target = _lastPlayerTarget;
            }

            // La ruta se evalua contra el ultimo objetivo visto, no contra la
            // linea de vista actual. Asi el Nemesis conserva la intencion de
            // perseguir y puede reanudar el mismo destino al abrir la puerta.
            PursuitRouteStatus routeStatus = PursuitRouteStatus.InvalidOrUnavailable;
            Vector3 validatedTarget = Vector3.zero;
            if (_isPursuingPlayer && _lastPlayerTarget != Vector3.zero)
            {
                routeStatus = EvaluatePursuitRouteTo(_lastPlayerTarget, out validatedTarget);
            }

            bool blockedByClosedDoor = routeStatus == PursuitRouteStatus.BlockedByClosedDoor;
            if (blockedByClosedDoor)
            {
                _isPursuingPlayer = true;
                _waitingForDoor = true;
                StopMoving();
                return;
            }

            // Si ya no ve al jugador, sigue intentando llegar al ultimo objetivo
            // hasta alcanzarlo. Perder la linea de vista no borra la intencion.
            if (_isPursuingPlayer && !seesPlayer && Reached(_lastPlayerTarget))
            {
                _isPursuingPlayer = false;
                _waitingForDoor = false;
                PickTarget();
            }
            else if (!_isPursuingPlayer && Time.time >= _waitUntil && Reached(_target))
            {
                PickTarget();
            }

            float dist = Player != null
                ? Vector3.Distance(transform.position, Player.transform.position)
                : proximityDistance;

            float proximity = Mathf.Clamp01(1f - dist / proximityDistance);
            float speed = patrolSpeed * (1f + proximity * proximityBoost) * (1f + _progress * progressSpeedFactor);

            if (_isPursuingPlayer)
            {
                if (routeStatus == PursuitRouteStatus.Ready)
                {
                    _waitingForDoor = false;
                    if (!TryMoveTowardsPosition(validatedTarget, speed))
                        StopMoving();
                }
                else
                {
                    // Una ruta invalida falla cerrada, pero no borra la intencion
                    // ni el ultimo objetivo: podria volver a estar disponible.
                    StopMoving();
                }
            }
            else
            {
                TryMoveTowardsPosition(_target, speed);
            }

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