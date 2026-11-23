using System.Collections.Generic;
using PrisaControlada.Items;
using PrisaControlada.Player;
using UnityEngine;
using UnityEngine.AI;

namespace PrisaControlada.Enemies
{
    //TP2 - Agustín Labarque
    // Los enemigos (runner, zombie, blind, nemesis) heredan de aca: vida,
    // danio, muerte y la ruta de persecucion viven en la base y cada uno pone
    // su forma de moverse. Es abstracta porque "un enemigo" sin tipo no tiene
    // sentido instanciar: es el molde, no un objeto.
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class EnemyBase : MonoBehaviour
    {
        protected enum PursuitRouteStatus
        {
            Ready,
            BlockedByClosedDoor,
            InvalidOrUnavailable
        }

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

        [Header("Persecución / puertas cerradas")]
        [Tooltip("Segundos entre cada comprobacion de la ruta hacia el jugador.")]
        [SerializeField, Min(0.05f)] private float pursuitPathRefreshInterval = 0.2f;
        [Tooltip("Radio maximo para proyectar al jugador sobre el NavMesh. Se mantiene corto para no elegir el lado equivocado de una puerta.")]
        [SerializeField, Min(0.05f)] private float pursuitTargetSampleRadius = 0.75f;
        [Tooltip("Radio maximo para proyectar destinos secundarios (patrulla, busqueda o merodeo) sobre el NavMesh.")]
        [SerializeField, Min(0.05f)] private float movementTargetSampleRadius = 1.5f;
        [Tooltip("Radio maximo para recolocar un agente fuera de la malla. Nunca usa AllAreas ni salta una puerta.")]
        [SerializeField, Min(0.05f)] private float navMeshSnapRadius = 0.75f;
        [Tooltip("Margen extra alrededor del panel para no perder el cruce por error.")]
        [SerializeField, Min(0f)] private float closedDoorPathPadding = 0.05f;
        [Tooltip("Distancia minima que el agente mantiene con el jugador al perseguir. Debe superar la suma de los dos radios: el NavMeshAgent no ve al CharacterController y, si termina metido dentro, al expulsarlo el jugador sube por la pared o sale al otro lado del muro.")]
        [SerializeField, Min(0f)] private float minPursuitSeparation = 0.95f;
        [Tooltip("Aceleracion al frenar en seco (parar para atacar o por ruta bloqueada). Con la aceleracion normal el agente patina tras recibir la orden de parar y termina montado dentro del CharacterController, que lo expulsa contra el muro.")]
        [SerializeField, Min(0f)] private float hardStopAcceleration = 1000f;

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

        private NavMeshPath _routeEvaluationPath;
        private NavMeshPath _movementPath;
        private NavMeshPath _submittedPath;
        private Vector3 _validatedPursuitTarget;
        private bool _hasValidatedPursuitTarget;
        private float _nextPursuitPathRefreshTime;
        private PursuitRouteStatus _pursuitRouteStatus = PursuitRouteStatus.InvalidOrUnavailable;
        private bool _pursuitPathDirty = true;
        // Aceleracion original del agente. Se restaura al reanudar la marcha
        // despues de una frenada en seco.
        private float _defaultAgentAcceleration;

        public int MaxHealth => maxHealth;
        public bool Dead => IsDead;
        public bool IsAttacking => _isAttacking;

        // Cada tipo de enemigo debe definir sus propias estadísticas.
        // Al ser abstracto, el compilador impide crear una variante sin contrato.
        public abstract void ApplyDefaultStats();

        // EnemyFactory aplica los valores después de que Unity haya ejecutado
        // Awake. Sincronizar la vida evita que nazca con la salud del prefab base.
        protected void ResetHealthToMax()
        {
            CurrentHealth = maxHealth;
        }

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Agent.updateRotation = false;
            _defaultAgentAcceleration = Agent.acceleration;
            CurrentHealth = maxHealth;
            _routeEvaluationPath = new NavMeshPath();
            _movementPath = new NavMeshPath();
            _submittedPath = new NavMeshPath();

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
            Player = FindFirstObjectByType<PlayerController>();
            if (Player != null)
            {
                PlayerHealth = Player.GetComponent<PlayerHealth>();
                Camera cam = Player.GetComponentInChildren<Camera>();
                if (cam != null) PlayerHead = cam.transform;
            }

            EnsureOnNavMesh();
            _pursuitPathDirty = true;
        }

        protected virtual void OnEnable()
        {
            _pursuitPathDirty = true;
            DoorController.NavigationStateChanged += HandleDoorNavigationStateChanged;
        }

        protected virtual void OnDisable()
        {
            DoorController.NavigationStateChanged -= HandleDoorNavigationStateChanged;
            _pursuitPathDirty = true;

            // Desactivar solo el script no debe dejar al NavMeshAgent ejecutando
            // una ruta cacheada ni permitir que un ataque activo siga damaging.
            StopMoving();
            CancelAttack();
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

        // Las puertas se dejan fuera del NavMesh para que una puerta abierta no
        // corte el paso. Antes de perseguir se calcula una ruta completa y se
        // cancela si atraviesa una puerta cerrada. El punto validado es el
        // mismo que despues recibe Agent.SetDestination.
        protected bool IsPursuitBlockedByClosedDoor()
        {
            return GetPursuitRouteStatus() == PursuitRouteStatus.BlockedByClosedDoor;
        }

        protected PursuitRouteStatus GetPursuitRouteStatus()
        {
            if (!_pursuitPathDirty && Time.time < _nextPursuitPathRefreshTime)
                return _pursuitRouteStatus;

            _pursuitPathDirty = false;
            _nextPursuitPathRefreshTime = Time.time + Mathf.Max(0.05f, pursuitPathRefreshInterval);
            RefreshPursuitRoute();
            return _pursuitRouteStatus;
        }

        private void RefreshPursuitRoute()
        {
            _hasValidatedPursuitTarget = false;
            _pursuitRouteStatus = PursuitRouteStatus.InvalidOrUnavailable;

            if (Player == null || Agent == null)
                return;

            _pursuitRouteStatus = EvaluatePursuitRouteTo(
                Player.transform.position, out Vector3 validatedTarget);
            if (_pursuitRouteStatus != PursuitRouteStatus.Ready)
                return;

            _validatedPursuitTarget = validatedTarget;
            _hasValidatedPursuitTarget = true;
        }

        // Evalua una ruta de persecucion hacia cualquier objetivo. Nemesis usa
        // esta misma comprobacion para su ultimo objetivo visto, incluso cuando
        // ya no tiene linea de vista al jugador.
        protected PursuitRouteStatus EvaluatePursuitRouteTo(Vector3 requestedTarget,
            out Vector3 validatedTarget)
        {
            validatedTarget = Vector3.zero;
            if (Agent == null)
                return PursuitRouteStatus.InvalidOrUnavailable;

            if (!EnsureOnNavMesh())
            {
                // Un Warp fallido a traves de una hoja cerrada sigue siendo un
                // bloqueo de puerta, no un fallo temporal de la malla.
                if (IsDirectSegmentBlockedByClosedDoor(transform.position, requestedTarget))
                    return PursuitRouteStatus.BlockedByClosedDoor;
                return PursuitRouteStatus.InvalidOrUnavailable;
            }

            if (!TrySampleNavMeshTarget(requestedTarget, pursuitTargetSampleRadius,
                    out validatedTarget))
            {
                // Si el objetivo no se puede samplear y el segmento recto atraviesa
                // una puerta, el destino es ambiguo y se conserva el bloqueo.
                if (IsDirectSegmentBlockedByClosedDoor(transform.position, requestedTarget))
                    return PursuitRouteStatus.BlockedByClosedDoor;
                validatedTarget = Vector3.zero;
                return PursuitRouteStatus.InvalidOrUnavailable;
            }

            // Si el sample cae al otro lado de una puerta, el segmento objetivo ->
            // destino lo revela. No se ejecuta un destino ambiguo.
            if (IsDirectSegmentBlockedByClosedDoor(requestedTarget, validatedTarget))
            {
                validatedTarget = Vector3.zero;
                return PursuitRouteStatus.BlockedByClosedDoor;
            }

            if (_routeEvaluationPath == null)
                _routeEvaluationPath = new NavMeshPath();

            if (!TryCalculateCompletePath(validatedTarget, _routeEvaluationPath))
            {
                // Una ruta parcial puede ser una union desconectada por una
                // puerta. Solo se clasifica como puerta cuando el segmento
                // demuestra que la separacion atraviesa una hoja cerrada.
                if (IsDirectSegmentBlockedByClosedDoor(transform.position, validatedTarget))
                    return PursuitRouteStatus.BlockedByClosedDoor;
                validatedTarget = Vector3.zero;
                return PursuitRouteStatus.InvalidOrUnavailable;
            }

            if (TryDetectPathCrossingClosedDoor(_routeEvaluationPath,
                    out bool crossesClosedDoor)
                && crossesClosedDoor)
            {
                validatedTarget = Vector3.zero;
                return PursuitRouteStatus.BlockedByClosedDoor;
            }

            return PursuitRouteStatus.Ready;
        }

        protected bool TryGetValidatedPursuitTarget(out Vector3 target)
        {
            target = Vector3.zero;
            if (GetPursuitRouteStatus() != PursuitRouteStatus.Ready || !_hasValidatedPursuitTarget)
                return false;

            target = _validatedPursuitTarget;
            return true;
        }

        private void HandleDoorNavigationStateChanged(DoorController changedDoor)
        {
            _pursuitPathDirty = true;

            // El cierre es una transicion sincronica: no se espera al proximo
            // Update del enemigo para detener el agente o cancelar un ataque.
            // Una puerta invalida o desactivada tambien falla cerrada.
            if (changedDoor != null && changedDoor.BlocksNavigation)
            {
                StopMoving();
                CancelAttack();
            }
        }

        // Movimiento hacia el jugador respetando las puertas cerradas. Devuelve
        // false si la ruta esta bloqueada; en ese caso el agente se detiene.
        protected bool TryMoveTowardsPlayer(float speed, bool faceTarget = true)
        {
            if (Player == null)
            {
                StopMoving();
                return false;
            }

            return TryMoveTowardsPlayer(Player.transform.position, speed, faceTarget);
        }

        protected bool TryMoveTowardsPlayer(Vector3 target, float speed, bool faceTarget = true)
        {
            if (!TryGetValidatedPursuitTarget(out Vector3 validatedPlayerTarget))
            {
                StopMoving();
                return false;
            }

            Vector3 executionTarget = validatedPlayerTarget;
            bool requestsPlayerTarget = (target - Player.transform.position).sqrMagnitude < 0.01f;

            // Un destino lateral (por ejemplo el stagger del Zombie) se valida
            // por separado. Si no es alcanzable, se usa el destino del jugador
            // que ya fue validado, nunca el punto original sin comprobar.
            if (!requestsPlayerTarget)
            {
                Vector3 validatedTarget;
                if (TryGetValidatedNavMeshDestination(target, movementTargetSampleRadius,
                        out validatedTarget))
                {
                    executionTarget = validatedTarget;
                }
            }

            // Separacion minima con el jugador. El agente no ve al
            // CharacterController: si se mete dentro, el jugador lo expulsa por
            // el eje de menor resistencia y termina subiendo por la pared o
            // saliendo al otro lado de un muro delgado. La ruta no esta
            // bloqueada, por eso se devuelve true y el ataque sigue permitido
            // (attackRange es mayor que esta separacion).
            if (WouldInvadePlayerSeparation(executionTarget))
            {
                StopMoving();
                if (faceTarget)
                    FaceTowards(executionTarget);
                return true;
            }

            return SetMovementDestination(executionTarget, speed, faceTarget);
        }

        // Separacion efectiva: nunca se detiene mas alla del rango de ataque,
        // para que un enemigo no se quede mirando sin poder golpear.
        private float EffectivePursuitSeparation =>
            Mathf.Min(minPursuitSeparation, Mathf.Max(0f, attackRange));

        // Verdadero si el agente ya esta dentro de la zona del jugador y su
        // destino tambien cae dentro: avanzar lo meteria dentro del
        // CharacterController. Si el destino esta fuera, puede salir de la zona.
        private bool WouldInvadePlayerSeparation(Vector3 destination)
        {
            return IsPlayerWithinPursuitSeparation()
                && IsInsidePlayerSeparationZone(destination);
        }

        // Verdadero si el jugador ya esta dentro de la zona que el agente no
        // debe invadir. Se mide en el plano horizontal: la altura no debe
        // impedir que el agente se acerque.
        private bool IsPlayerWithinPursuitSeparation()
        {
            if (Player == null)
                return false;

            Vector3 toPlayer = Player.transform.position - transform.position;
            toPlayer.y = 0f;

            float separation = EffectivePursuitSeparation;
            return toPlayer.sqrMagnitude <= separation * separation;
        }

        // Verdadero si un punto de destino cae dentro de esa misma zona.
        private bool IsInsidePlayerSeparationZone(Vector3 point)
        {
            if (Player == null)
                return false;

            Vector3 toPlayer = point - Player.transform.position;
            toPlayer.y = 0f;

            float separation = EffectivePursuitSeparation;
            return toPlayer.sqrMagnitude < separation * separation;
        }

        // Gira el cuerpo hacia un punto sin pedirle nada al NavMeshAgent.
        private void FaceTowards(Vector3 target)
        {
            Vector3 flat = target - transform.position;
            flat.y = 0f;

            if (flat.sqrMagnitude <= 0.0001f)
                return;

            Quaternion look = Quaternion.LookRotation(flat.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        // Movimiento secundario seguro (patrulla, busqueda o merodeo). El punto
        // se proyecta sobre el NavMesh y su ruta completa se comprueba contra
        // todas las puertas cerradas antes de ejecutar el movimiento.
        protected bool TryMoveTowardsPosition(Vector3 target, float speed, bool faceTarget = true)
        {
            // La persecucion del Nemesis pasa por aqui, asi que tambien se
            // respeta la separacion minima con el jugador.
            if (WouldInvadePlayerSeparation(target))
            {
                StopMoving();
                if (faceTarget)
                    FaceTowards(target);
                return true;
            }

            if (!TryGetValidatedNavMeshDestination(target, movementTargetSampleRadius,
                    out Vector3 validatedTarget))
            {
                StopMoving();
                return false;
            }

            return SetMovementDestination(validatedTarget, speed, faceTarget);
        }

        protected bool MoveTowards(Vector3 target, float speed, bool faceTarget = true)
        {
            return TryMoveTowardsPosition(target, speed, faceTarget);
        }

        // Valida un destino exacto y lo devuelve ya proyectado sobre la malla.
        // La ruta debe ser completa y ningun segmento puede cruzar una puerta
        // cerrada.
        protected bool TryGetValidatedNavMeshDestination(Vector3 requestedTarget, float sampleRadius,
            out Vector3 validatedTarget)
        {
            validatedTarget = Vector3.zero;
            if (Agent == null || !EnsureOnNavMesh())
                return false;

            if (!TrySampleNavMeshTarget(requestedTarget, sampleRadius, out validatedTarget))
                return false;

            if (_movementPath == null)
                _movementPath = new NavMeshPath();

            if (!TryCalculateCompletePath(validatedTarget, _movementPath))
                return false;

            if (PathCrossesClosedDoor(_movementPath))
                return false;

            return true;
        }

        private bool TrySampleNavMeshTarget(Vector3 worldPosition, float sampleRadius,
            out Vector3 sampledPosition)
        {
            sampledPosition = Vector3.zero;
            if (Agent == null || Agent.areaMask == 0)
                return false;

            if (!NavMesh.SamplePosition(worldPosition, out NavMeshHit hit,
                    Mathf.Max(0.05f, sampleRadius), Agent.areaMask))
                return false;

            sampledPosition = hit.position;
            return true;
        }

        private bool TryCalculateCompletePath(Vector3 target, NavMeshPath path)
        {
            if (Agent == null || path == null)
                return false;

            if (!Agent.CalculatePath(target, path))
                return false;

            // Un camino parcial/invalido no es seguro: podría hacer que el agente
            // avance hacia un borde de la malla y termine atravesando geometria.
            return path.status == NavMeshPathStatus.PathComplete;
        }

        private bool PathCrossesClosedDoor(NavMeshPath path)
        {
            return !TryDetectPathCrossingClosedDoor(path, out bool crossesClosedDoor)
                || crossesClosedDoor;
        }

        // Devuelve false si la ruta no tiene informacion utilizable. El segundo
        // valor solo significa que una puerta cerrada fue detectada.
        private bool TryDetectPathCrossingClosedDoor(NavMeshPath path, out bool crossesClosedDoor)
        {
            crossesClosedDoor = false;
            if (path == null)
                return false;

            Vector3[] corners = path.corners;
            if (corners.Length == 0)
                return false;

            for (int segment = 0; segment < corners.Length - 1; segment++)
            {
                if (!IsDirectSegmentBlockedByClosedDoor(corners[segment], corners[segment + 1]))
                    continue;

                crossesClosedDoor = true;
                return true;
            }

            return true;
        }

        // Comprueba el segmento directo (no una ruta alternativa). Se usa para
        // impedir ataques y teletransportes que atraviesen una hoja cerrada.
        protected bool IsDirectSegmentBlockedByClosedDoor(Vector3 worldStart, Vector3 worldEnd)
        {
            IReadOnlyList<DoorController> doors = DoorController.ActiveDoors;
            for (int i = 0; i < doors.Count; i++)
            {
                DoorController door = doors[i];
                if (door == null)
                    return true;

                // Una referencia rota o un controlador desactivado no se puede
                // demostrar transitable: falla cerrado aunque la hoja este
                // marcada como abierta.
                if (!door.isActiveAndEnabled || !door.HasValidNavigationGeometry)
                    return true;

                if (door.IsOpen)
                    continue;

                if (door.BlocksPathSegment(worldStart, worldEnd, closedDoorPathPadding))
                    return true;
            }

            return false;
        }

        protected bool EnsureOnNavMesh()
        {
            if (Agent == null || Agent.areaMask == 0) return false;
            if (Agent.isOnNavMesh) return true;

            // No se busca en AllAreas ni a diez metros: un sample lejano puede
            // dejar al agente al otro lado de una puerta cerrada.
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit,
                    Mathf.Max(0.05f, navMeshSnapRadius), Agent.areaMask))
                return false;

            // El Warp es un salto recto. Si ese segmento atraviesa una puerta,
            // no se realiza.
            if (IsDirectSegmentBlockedByClosedDoor(transform.position, hit.position))
                return false;

            if (!Agent.Warp(hit.position))
                return false;

            return Agent.isOnNavMesh;
        }

        private bool SetMovementDestination(Vector3 target, float speed, bool faceTarget)
        {
            if (Agent == null)
                return false;

            if (_isAttacking)
            {
                // Durante el ataque el cuerpo queda clavado en el suelo.
                if (Agent.isOnNavMesh) HaltAgent();
                return false;
            }

            if (!EnsureOnNavMesh())
            {
                StopMoving();
                return false;
            }

            Agent.speed = speed;

            if (faceTarget)
                FaceTowards(target);

            // Se vuelve a calcular la ruta que se va a enviar al agente. Esto
            // impide aceptar una ruta parcial o un segmento que atraviese una
            // puerta cerrada entre la validacion y la ejecucion.
            if (_submittedPath == null)
                _submittedPath = new NavMeshPath();

            if (!TryCalculateCompletePath(target, _submittedPath)
                || PathCrossesClosedDoor(_submittedPath))
            {
                HaltAgent();
                return false;
            }

            // Primero se solicita el destino. Solo se reanuda el agente si Unity
            // acepto la solicitud; un false descarta la ruta anterior.
            if (!Agent.SetDestination(target))
            {
                HaltAgent();
                return false;
            }

            // Si Unity ya publico la ruta, tambien se comprueba la ruta realmente
            // comprometida. Un camino parcial o bloqueado se descarta.
            if (Agent.hasPath)
            {
                NavMeshPath committedPath = Agent.path;
                if (committedPath.status != NavMeshPathStatus.PathComplete
                    || PathCrossesClosedDoor(committedPath))
                {
                    HaltAgent();
                    return false;
                }
            }

            // Se reanuda la marcha con la aceleracion normal: la frenada en
            // seco solo debe durar mientras el agente este parado.
            Agent.acceleration = _defaultAgentAcceleration;
            Agent.isStopped = false;
            return true;
        }

        // Frena el agente en seco. ResetPath puede restaurar el estado interno
        // del agente, por eso isStopped se aplica al final para garantizar que
        // quede quieto. La aceleracion se sube antes para que la velocidad
        // muera en el mismo frame: con la normal el agente patina hasta un
        // metro y termina dentro del CharacterController del jugador.
        private void HaltAgent()
        {
            Agent.acceleration = hardStopAcceleration;
            Agent.ResetPath();
            Agent.isStopped = true;
        }

        protected void StopMoving()
        {
            if (Agent == null) return;

            if (!Agent.isOnNavMesh)
                return;

            HaltAgent();
        }

        protected void TryAttack()
        {
            if (Player == null || PlayerHealth == null || _isAttacking) return;

            // No atacar a través de una puerta cerrada, aunque el jugador esté
            // dentro del rango y el enemigo lo haya detectado. Se exige tanto
            // una ruta completa como un segmento directo libre atacante->jugador.
            if (!TryGetValidatedPursuitTarget(out _)) return;
            if (IsDirectSegmentBlockedByClosedDoor(transform.position, Player.transform.position)) return;

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

            if (Player == null)
            {
                CancelAttack();
                return;
            }

            // Si la puerta se cerró durante el windup o el ataque ya activo,
            // cancelarlo antes de que pueda generar o dejar daño. La comprobacion
            // directa cubre el caso en que exista una ruta alternativa alrededor
            // de la puerta, pero el segmento atacante->jugador la atraviesa.
            if (GetPursuitRouteStatus() != PursuitRouteStatus.Ready ||
                IsDirectSegmentBlockedByClosedDoor(transform.position, Player.transform.position))
            {
                CancelAttack();
                return;
            }

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

        protected virtual void CancelAttack()
        {
            if (_activeDamageCollider != null)
            {
                Collider damageCollider = _activeDamageCollider.GetComponent<Collider>();
                if (damageCollider != null)
                    damageCollider.enabled = false;

                Destroy(_activeDamageCollider);
                _activeDamageCollider = null;
            }

            _attackTimer = 0f;
            _isAttacking = false;
            NextAttackTime = Time.time + attackCooldown;
            OnAttackCancelled();
        }

        // Permite que cada enemigo cancele tambien el estado visual de su
        // animador de ataque, no solo el collider de daño.
        protected virtual void OnAttackCancelled()
        {
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