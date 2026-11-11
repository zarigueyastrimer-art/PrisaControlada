using UnityEngine;

namespace PrisaControlada.Enemies
{
    public class RunnerEnemy : EnemyBase
    {
        [Header("Runner Behavior")]
        [SerializeField] protected float loseTargetTime = 5f;
        [SerializeField] private float watchDuration = 5f;

        [Header("Audio (3D loop)")]
        [SerializeField] private AudioClip runnerClip;
        [SerializeField, Range(0f, 1f)] private float runnerVolume = 1f;
        [SerializeField] private float runnerMinDistance = 2f;
        [SerializeField] private float runnerMaxDistance = 20f;

        private AudioSource _runnerSource;

        private enum RunnerState { Idle, Watch, Chase }
        private RunnerState _state = RunnerState.Idle;
        private float _watchTimer;
        private float _loseTimer;
        private Animator _animator;
        private Transform _headBone;
        private Transform _neckBone;

        // Ángulo máximo que puede girar el cuello/cabeza (evita el efecto "palito" o 360°)
        [SerializeField] private float maxHeadTurnAngle = 90f;

        // Tiempo que se queda quieto después de atacar (recuperación)
        [SerializeField] private float attackRecoveryTime = 1f;

        // Indica si en este frame se debe girar la cabeza hacia el jugador
        private bool _wantHeadLook;

        // Temporizador de recuperación post-ataque (quedarse quieto 1 seg)
        private float _attackRecoveryTimer;
        private bool _wasAttacking;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        protected override void Awake()
        {
            base.Awake();
            _animator = GetComponentInChildren<Animator>();
            _headBone = FindHeadBone();

            // Buscar el cuello (padre de la cabeza) para rotar ambos juntos
            if (_headBone != null && _headBone.parent != null)
                _neckBone = _headBone.parent;

            Debug.Log($"[Runner] {name}: Awake - head={(_headBone != null ? _headBone.name : "null")}, neck={(_neckBone != null ? _neckBone.name : "null")}");

            _runnerSource = gameObject.AddComponent<AudioSource>();
            _runnerSource.playOnAwake = false;
            _runnerSource.loop = true;
            _runnerSource.spatialBlend = 1f;
            _runnerSource.rolloffMode = AudioRolloffMode.Linear;
            _runnerSource.minDistance = runnerMinDistance;
            _runnerSource.maxDistance = runnerMaxDistance;
            _runnerSource.dopplerLevel = 0f;
            _runnerSource.volume = runnerVolume;
            _runnerSource.clip = runnerClip;
            if (runnerClip != null)
                _runnerSource.Play();
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

            // Detecta al jugador: lo ve, está cerca o le pegó (aggro).
            bool seen = PlayerDetected();

            _wantHeadLook = false;

            // Detectar cuando termina el ataque para iniciar la recuperación (1 seg quieto)
            if (_wasAttacking && !_isAttacking)
            {
                _attackRecoveryTimer = attackRecoveryTime;
                Debug.Log($"[Runner] {name}: Ataque terminó -> recuperación {attackRecoveryTime:F1}s quieto");
            }
            _wasAttacking = _isAttacking;

            // Durante la recuperación post-ataque: quedarse quieto, no moverse ni atacar
            if (_attackRecoveryTimer > 0f)
            {
                _attackRecoveryTimer -= Time.deltaTime;
                StopMoving();
                UpdateAnimation();

                // Cuando termina la recuperación, cancelar la animación de ataque y volver a Idle/Run
                if (_attackRecoveryTimer <= 0f)
                {
                    Debug.Log($"[Runner] {name}: Recuperación terminada, cancelando ataque y volviendo a Idle");
                    if (_animator != null)
                        _animator.Play("Idle"); // Fuerza salida del estado Attack
                }
                return;
            }

            switch (_state)
            {
                case RunnerState.Idle:
                    if (IsAggroed)
                    {
                        // Le pegaron: persigue sin esperar a mirarlo.
                        _state = RunnerState.Chase;
                        _loseTimer = 0f;
                    }
                    else if (seen)
                    {
                        _state = RunnerState.Watch;
                        _watchTimer = 0f;
                        StopMoving();
                    }
                    break;

                case RunnerState.Watch:
                    if (IsAggroed)
                    {
                        // Le pegaron mientras observaba: persigue de inmediato.
                        _state = RunnerState.Chase;
                        break;
                    }

                    if (!seen)
                    {
                        _state = RunnerState.Idle;
                        break;
                    }

                    _wantHeadLook = true; // Solo gira la cabeza/cuello, el cuerpo queda quieto
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
                        if (_isAttacking)
                            _wantHeadLook = true; // Durante el ataque: solo cabeza, cuerpo quieto
                        else
                            FacePlayer(); // Antes de atacar: gira el cuerpo para enfrentar
                        TryAttack();
                    }
                    else
                    {
                        MoveTowards(Player.transform.position, moveSpeed);
                        TryAttack();
                    }
                    break;
            }

            UpdateAnimation();
        }

        // LateUpdate se ejecuta después del Animator para no pelear contra la animación del rig.
        private void LateUpdate()
        {
            if (!_wantHeadLook || Player == null || (_headBone == null && _neckBone == null))
                return;

            // Dirección desde el cuerpo hacia el jugador (plano XZ, sin componente Y para no romper el cuello)
            Vector3 toPlayer = Player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f) return;

            // Ángulo del enemigo y del jugador en Y (mundo)
            float enemyYaw = transform.eulerAngles.y;
            float targetYaw = Quaternion.LookRotation(toPlayer.normalized).eulerAngles.y;

            // Diferencia normalizada a [-180, 180]
            float deltaYaw = Mathf.DeltaAngle(enemyYaw, targetYaw);

            // Limitar al ángulo máximo permitido
            float clampedYaw = Mathf.Clamp(deltaYaw, -maxHeadTurnAngle, maxHeadTurnAngle);

            Debug.Log($"[Runner] {name}: LateUpdate headLook - enemyYaw={enemyYaw:F1}, targetYaw={targetYaw:F1}, deltaYaw={deltaYaw:F1}, clamped={clampedYaw:F1}");

            // --- APLICAR AL CUELLO (rotación mundial, directo) ---
            if (_neckBone != null)
            {
                // El cuello mira hacia el jugador con la mitad del ángulo
                float neckYaw = enemyYaw + clampedYaw * 0.5f;
                Quaternion neckTarget = Quaternion.Euler(0f, neckYaw, 0f);
                _neckBone.rotation = neckTarget;
            }

            // --- APLICAR A LA CABEZA (rotación mundial, directo) ---
            if (_headBone != null)
            {
                // La cabeza mira directamente hacia el jugador
                Quaternion headTarget = Quaternion.Euler(0f, targetYaw, 0f);
                _headBone.rotation = headTarget;
            }
        }

        private void UpdateAnimation()
        {
            if (_animator == null) return;

            bool isMoving = Agent != null
                && Agent.isOnNavMesh
                && !Agent.isStopped
                && Agent.velocity.sqrMagnitude > 0.05f
                && !IsAttacking;

            _animator.SetFloat(SpeedHash, isMoving ? 1f : 0f, 0.1f, Time.deltaTime);
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
