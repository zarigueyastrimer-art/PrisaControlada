using System;
using System.Collections.Generic;
using PrisaControlada.Player;
using UnityEngine;
using UnityEngine.AI;

namespace PrisaControlada.Items
{
    //TP2 - Francisco Larrondo
    public class DoorController : MonoBehaviour, IInteractable
    {
        private static readonly List<DoorController> s_ActiveDoors = new List<DoorController>();

        // Las puertas activas se consultan desde EnemyBase para comprobar la ruta
        // sin meter los paneles en el NavMesh. El evento permite recalcular esa
        // ruta en el mismo frame en que una puerta se abre o se cierra.
        public static IReadOnlyList<DoorController> ActiveDoors => s_ActiveDoors;
        public static event Action<DoorController> NavigationStateChanged;

        [Header("References")]
        [SerializeField] private Transform doorPanel;
        [SerializeField] private Transform hinge;
        [SerializeField] private Transform player;

        [Header("Settings")]
        [Tooltip("Angulo de apertura de la bisagra en grados.")]
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 5f;
        [Tooltip("Extremo del panel donde esta la bisagra: +1 = borde +X, -1 = borde -X. Fija, no cambia con el jugador.")]
        [SerializeField] private float hingeSide = 1f;
        [Tooltip("Margen alrededor de la hoja que se considera boca ocupada. Si el jugador esta dentro, la puerta no se cierra: cerrarle encima lo expulsaria a traves del muro.")]
        [SerializeField, Min(0f)] private float doorwayPlayerClearance = 0.5f;

        [Header("NavMesh")]
        [Tooltip("Obstaculo local de la puerta. SIN carve: la boca del NavMesh queda horneada siempre. Cerrado evita localmente; EnemyBase impide perseguir y atacar a traves de el. Abierto deshabilitado. Si esta vacio se busca en los hijos de doorPanel.")]
        [SerializeField] private NavMeshObstacle doorObstacle;

        private Quaternion _targetRotation = Quaternion.identity;
        // Pose de apertura elegida en el ultimo Toggle. Se guarda para poder
        // reabrir si el jugador entra en la trayectoria mientras la hoja baja.
        private Quaternion _openRotation = Quaternion.identity;
        private bool _isOpen;
        private bool _targetOpen;

        // Huella de la hoja cuando esta cerrada. Se guarda respecto a la
        // bisagra para que el rectángulo no gire mientras la puerta se anima.
        // Asi, al cerrar una puerta que estaba abierta, la barrera logica
        // aparece inmediatamente en la boca de la puerta.
        private Vector3 _closedBlockerCenterInHinge;
        private Vector3 _closedBlockerRightInHinge;
        private Vector3 _closedBlockerForwardInHinge;
        private float _closedBlockerHalfWidth;
        private float _closedBlockerHalfDepth;
        private bool _hasClosedBlocker;
        private bool _configurationErrorLogged;
        private bool _lastNavigationGeometryValid = true;

        // Estado logico de transito. La puerta recién abierta sigue bloqueando
        // hasta que la hoja se aparto lo suficiente de la boca.
        public bool IsOpen => _isOpen;
        public bool IsOpening => _targetOpen && !_isOpen;
        public bool TargetOpen => _targetOpen;
        public bool HasValidClosedBlocker => _hasClosedBlocker;
        public bool HasValidNavigationGeometry => doorPanel != null && hinge != null
            && doorObstacle != null && _hasClosedBlocker;
        public bool CanToggle => HasValidNavigationGeometry && Mathf.Abs(openAngle) > 1f;

        // Estado fail-closed usado por los enemigos. Una puerta cerrada, sin
        // referencias validas o con el controlador desactivado bloquea el paso.
        public bool BlocksNavigation => !isActiveAndEnabled
            || !HasValidNavigationGeometry
            || !_isOpen;

        protected virtual void Awake()
        {
            if (player == null)
            {
                GameObject playerGo = GameObject.Find("Player");
                if (playerGo != null)
                    player = playerGo.transform;
            }

            ResolveDoorObstacle();
            CacheClosedBlocker();
            ApplyNavMeshBlocker();
            ValidateConfiguration();
            _lastNavigationGeometryValid = HasValidNavigationGeometry;
        }

        protected virtual void OnEnable()
        {
            if (!s_ActiveDoors.Contains(this))
                s_ActiveDoors.Add(this);

            // Una puerta cerrada que acaba de activarse tambien invalida las
            // rutas cacheadas de los enemigos.
            NavigationStateChanged?.Invoke(this);
        }

        protected virtual void OnDisable()
        {
            // Si solo se desactiva el componente, la hoja sigue fisicamente en
            // la escena: se conserva en la lista para fallar cerrado. Si se
            // desactiva el GameObject completo, la puerta ya no existe.
            if (!gameObject.activeInHierarchy)
                s_ActiveDoors.Remove(this);

            NavigationStateChanged?.Invoke(this);
        }

        protected virtual void OnDestroy()
        {
            s_ActiveDoors.Remove(this);
        }

        public bool IsDoorHit(Collider hitCollider)
        {
            if (doorPanel == null) return false;

            Transform current = hitCollider.transform;
            while (current != null)
            {
                if (current == doorPanel) return true;
                current = current.parent;
            }

            return false;
        }

        // Punto de entrada de interaccion. LockedDoor / CodeDoor lo sobreescriben.
        public virtual bool TryInteract(InventoryController inventory)
        {
            return Toggle();
        }

        // ---- IInteractable ----

        // Una puerta no es agarrable: no lleva flecha de proximidad.
        public bool IsGrabbable => false;

        // La puerta solo es interactiva si el rayo pego en la hoja: el marco y
        // el dintel no abren nada (asi la mira no se cierra al apuntarles).
        public bool IsValidTarget(Collider hitCollider)
        {
            return IsDoorHit(hitCollider);
        }

        // Entrada unica que llama InventoryController. Valida el impacto y
        // despues delega en el TryInteract virtual, asi LockedDoor y CodeDoor
        // mantienen su logica de llave/codigo sin reescribir este metodo.
        public bool TryInteract(InventoryController inventory, Collider hitCollider)
        {
            if (!IsDoorHit(hitCollider))
                return false;

            return TryInteract(inventory);
        }

        // Inicia el movimiento de la hoja. Al cerrar, la barrera logica aparece
        // inmediatamente. Al abrir, la puerta solo pasa a IsOpen cuando la hoja
        // ya se aparto lo suficiente de la boca.
        public bool Toggle()
        {
            if (!CanToggle)
            {
                LogInvalidNavigationGeometry("no se puede abrir");
                return false;
            }

            if (_targetOpen)
            {
                // Estado actual: abierta o abriendose. Se solicita el cierre.
                // Si el jugador esta en la boca no se cierra: la hoja lo
                // expulsaria a traves del muro o hacia el techo.
                if (IsPlayerInClosingPath())
                    return false;

                _targetOpen = false;
                _targetRotation = Quaternion.identity;
                SetLogicalOpen(false);
                return true;
            }

            // Estado actual: cerrada o cerrandose. Se solicita la apertura.
            _targetOpen = true;
            float swingSide = ComputeSwingSide();
            _targetRotation = Quaternion.Euler(0f, -swingSide * openAngle, 0f);
            _openRotation = _targetRotation;
            return true;
        }

        public bool RequestOpen()
        {
            if (!CanToggle)
            {
                LogInvalidNavigationGeometry("no se puede abrir");
                return false;
            }

            if (_targetOpen)
                return true;

            return Toggle();
        }

        public bool RequestClose()
        {
            if (!CanToggle)
            {
                LogInvalidNavigationGeometry("no se puede cerrar");
                return false;
            }

            if (!_targetOpen)
                return true;

            return Toggle();
        }

        // Comprueba si un tramo del camino del enemigo atraviesa el panel cerrado.
        // No modifica el NavMesh: es solo una barrera logica para la persecucion.
        // Usa la huella de la hoja en su pose cerrada, no su transform actual,
        // para que cerrar una puerta abierta active la barrera en el mismo frame.
        public bool BlocksPathSegment(Vector3 worldStart, Vector3 worldEnd, float padding = 0.05f)
        {
            // Fail-closed: sin referencias y geometria validas no se puede
            // demostrar que el segmento sea transitable, incluso si la hoja
            // figura como abierta.
            if (!isActiveAndEnabled || !HasValidNavigationGeometry)
            {
                LogInvalidNavigationGeometry("se bloqueara todo el trafico");
                return true;
            }

            if (_isOpen)
                return false;

            if (!TryGetClosedBlockerWorldData(out Vector3 center, out Vector3 right,
                    out Vector3 forward))
                return true;

            float halfWidth = _closedBlockerHalfWidth + Mathf.Max(0f, padding);
            float halfDepth = _closedBlockerHalfDepth + Mathf.Max(0f, padding);
            if (halfWidth <= 0.0001f || halfDepth <= 0.0001f)
                return false;

            Vector3 startOffset = worldStart - center;
            Vector3 endOffset = worldEnd - center;

            Vector2 start = new Vector2(
                Vector3.Dot(startOffset, right),
                Vector3.Dot(startOffset, forward));
            Vector2 end = new Vector2(
                Vector3.Dot(endOffset, right),
                Vector3.Dot(endOffset, forward));

            return SegmentIntersectsRect(start, end, new Vector2(halfWidth, halfDepth));
        }

        // Busca el obstacle local y, si falta, lo crea a partir del collider del
        // panel. El obstacle nunca carvea: solo evita que el agente atraviese la
        // hoja mientras esta se mueve.
        private void ResolveDoorObstacle()
        {
            if (doorObstacle == null && doorPanel != null)
                doorObstacle = doorPanel.GetComponentInChildren<NavMeshObstacle>(true);

            if (doorObstacle != null || doorPanel == null)
                return;

            BoxCollider box = doorPanel.GetComponent<BoxCollider>();
            doorObstacle = doorPanel.gameObject.AddComponent<NavMeshObstacle>();
            doorObstacle.shape = NavMeshObstacleShape.Box;

            if (box != null)
            {
                doorObstacle.center = box.center;
                doorObstacle.size = box.size;
            }
            else
            {
                Renderer renderer = doorPanel.GetComponent<Renderer>();
                if (renderer != null)
                {
                    doorObstacle.center = renderer.localBounds.center;
                    doorObstacle.size = renderer.localBounds.size;
                }
                else
                {
                    doorObstacle.center = Vector3.zero;
                    doorObstacle.size = new Vector3(1f, 2f, 0.1f);
                }
            }
        }

        private void ValidateConfiguration()
        {
            bool validGeometry = HasValidNavigationGeometry;
            bool validAngle = Mathf.Abs(openAngle) > 1f;

            if (doorPanel == null)
                Debug.LogError($"[DoorController] La puerta '{name}' no tiene doorPanel asignado; " +
                               "se bloqueara todo el trafico hasta repararla.", this);

            if (hinge == null)
                Debug.LogError($"[DoorController] La puerta '{name}' no tiene hinge asignado; " +
                               "se bloqueara todo el trafico hasta repararla.", this);

            if (!validAngle)
                Debug.LogError($"[DoorController] La puerta '{name}' tiene openAngle invalido " +
                               $"({openAngle}); no se puede abrir.", this);

            if (!_hasClosedBlocker)
                Debug.LogError($"[DoorController] La puerta '{name}' no tiene un panel/obstacle con " +
                               "geometria valida; se bloqueara todo el trafico hasta repararla.", this);
            else if (doorObstacle == null)
                Debug.LogError($"[DoorController] La puerta '{name}' no tiene doorObstacle; " +
                               "la barrera logica seguira activa, pero falta la evasion local.", this);

            _configurationErrorLogged = !validGeometry || !validAngle;
        }

        private void LogInvalidNavigationGeometry(string reason)
        {
            if (_configurationErrorLogged)
                return;

            _configurationErrorLogged = true;
            Debug.LogError($"[DoorController] La puerta '{name}' tiene referencias o geometria " +
                           $"de navegacion invalidas; {reason} y se bloqueara todo el trafico " +
                           "hasta repararla.", this);
        }

        // Cachea el rectangol de la hoja usando su relacion con la bisagra.
        // La pose cerrada de la bisagra es la identidad, por lo que estos datos
        // siguen siendo validos aunque Awake se ejecute con la puerta abierta.
        private void CacheClosedBlocker()
        {
            _hasClosedBlocker = false;

            if (hinge == null)
                return;

            if (doorObstacle != null && doorObstacle.shape == NavMeshObstacleShape.Box)
            {
                CacheClosedBlocker(
                    doorObstacle.transform,
                    doorObstacle.center,
                    doorObstacle.size.x * 0.5f,
                    doorObstacle.size.z * 0.5f);
                return;
            }

            if (doorPanel != null)
            {
                BoxCollider box = doorPanel.GetComponent<BoxCollider>();
                if (box != null)
                    CacheClosedBlocker(
                        box.transform,
                        box.center,
                        box.size.x * 0.5f,
                        box.size.z * 0.5f);
            }
        }

        private void CacheClosedBlocker(Transform blockerTransform, Vector3 center,
            float halfWidth, float halfDepth)
        {
            Vector3 worldCenter = blockerTransform.TransformPoint(center);

            _closedBlockerCenterInHinge = hinge.InverseTransformPoint(worldCenter);
            _closedBlockerRightInHinge = hinge.InverseTransformDirection(blockerTransform.right).normalized;
            _closedBlockerForwardInHinge = hinge.InverseTransformDirection(blockerTransform.forward).normalized;

            Vector3 scale = blockerTransform.lossyScale;
            _closedBlockerHalfWidth = halfWidth * Mathf.Abs(scale.x);
            _closedBlockerHalfDepth = halfDepth * Mathf.Abs(scale.z);

            _hasClosedBlocker = _closedBlockerHalfWidth > 0.0001f
                && _closedBlockerHalfDepth > 0.0001f;
        }

        // Reconstruye la huella de la hoja en el mundo. La bisagra puede seguir
        // girando, pero su pose logica cerrada siempre es localRotation identity.
        private bool TryGetClosedBlockerWorldData(out Vector3 center, out Vector3 right,
            out Vector3 forward)
        {
            return TryGetBlockerWorldData(Quaternion.identity, out center, out right,
                out forward);
        }

        // Igual que lo anterior, pero para cualquier rotacion local de la
        // bisagra. Permite muestrear la barrida de la hoja entre su pose
        // actual y la cerrada.
        private bool TryGetBlockerWorldData(Quaternion hingeLocalRotation,
            out Vector3 center, out Vector3 right, out Vector3 forward)
        {
            center = Vector3.zero;
            right = Vector3.right;
            forward = Vector3.forward;

            if (!_hasClosedBlocker || hinge == null)
                return false;

            Transform hingeParent = hinge.parent;
            if (hingeParent != null)
            {
                Vector3 centerInClosedHinge = hinge.localPosition
                    + hingeLocalRotation * Vector3.Scale(hinge.localScale,
                        _closedBlockerCenterInHinge);

                center = hingeParent.TransformPoint(centerInClosedHinge);
                right = hingeParent.TransformDirection(
                    hingeLocalRotation * _closedBlockerRightInHinge).normalized;
                forward = hingeParent.TransformDirection(
                    hingeLocalRotation * _closedBlockerForwardInHinge).normalized;
                return true;
            }

            // Caso excepcional: la bisagra es la raiz de la puerta.
            center = hinge.position + hinge.rotation * hingeLocalRotation
                * Vector3.Scale(hinge.localScale, _closedBlockerCenterInHinge);
            right = (hinge.rotation * hingeLocalRotation
                * _closedBlockerRightInHinge).normalized;
            forward = (hinge.rotation * hingeLocalRotation
                * _closedBlockerForwardInHinge).normalized;
            return true;
        }

        // Verdadero si el jugador esta dentro de la trayectoria que barre la
        // hoja al cerrar. Se muestrean varias poses entre la actual y la
        // cerrada y el rectangulo se expande por el margen del jugador para
        // cubrir su capsule. Cerrar encima expulsaria al CharacterController
        // a traves del muro o hacia el techo.
        private bool IsPlayerInClosingPath()
        {
            if (player == null || hinge == null || !_hasClosedBlocker)
                return false;

            Vector3 playerPosition = player.position;
            float halfWidth = _closedBlockerHalfWidth + doorwayPlayerClearance;
            float halfDepth = _closedBlockerHalfDepth + doorwayPlayerClearance;

            Quaternion from = hinge.localRotation;
            const int samples = 5;
            for (int i = 0; i <= samples; i++)
            {
                Quaternion pose = Quaternion.Slerp(from, Quaternion.identity,
                    i / (float)samples);

                if (!TryGetBlockerWorldData(pose, out Vector3 center, out Vector3 right,
                        out Vector3 forward))
                    return false;

                Vector3 offset = playerPosition - center;
                offset.y = 0f;

                if (Mathf.Abs(Vector3.Dot(offset, right)) <= halfWidth
                    && Mathf.Abs(Vector3.Dot(offset, forward)) <= halfDepth)
                    return true;
            }

            return false;
        }

        // Intersección de un segmento 2D con un rectangulo centrado en el origen.
        private static bool SegmentIntersectsRect(Vector2 start, Vector2 end, Vector2 halfSize)
        {
            float tMin = 0f;
            float tMax = 1f;

            if (!ClipSegmentToAxis(start.x, end.x, -halfSize.x, halfSize.x, ref tMin, ref tMax))
                return false;

            if (!ClipSegmentToAxis(start.y, end.y, -halfSize.y, halfSize.y, ref tMin, ref tMax))
                return false;

            return true;
        }

        private static bool ClipSegmentToAxis(float start, float end, float min, float max,
            ref float tMin, ref float tMax)
        {
            float direction = end - start;
            if (Mathf.Abs(direction) < 0.000001f)
                return start >= min && start <= max;

            float t1 = (min - start) / direction;
            float t2 = (max - start) / direction;
            if (t1 > t2)
            {
                float temp = t1;
                t1 = t2;
                t2 = temp;
            }

            tMin = Mathf.Max(tMin, t1);
            tMax = Mathf.Min(tMax, t2);
            return tMin <= tMax;
        }

        // La puerta NUNCA carvea el NavMesh (la boca siempre queda horneada,
        // asi que no hace falta rehornear al abrir ni al cerrar). El obstacle
        // sin carve solo aporta evasion local; la barrera logica que impide
        // perseguir y atacar a traves de una puerta cerrada la calcula
        // EnemyBase sobre la ruta real. Al abrir se deshabilita y los enemigos
        // cruzan libre.
        private void ApplyNavMeshBlocker()
        {
            if (doorObstacle == null)
                return;

            doorObstacle.carving = false;
            doorObstacle.enabled = !_isOpen;
        }

        private void SetLogicalOpen(bool open)
        {
            if (_isOpen == open)
                return;

            _isOpen = open;
            ApplyNavMeshBlocker();
            NavigationStateChanged?.Invoke(this);
        }

        private float ComputeSwingSide()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = transform.right;
            forward.Normalize();

            if (player == null)
                return -1f;

            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;

            return Vector3.Dot(toPlayer, forward) >= 0f ? -1f : 1f;
        }

        private void Update()
        {
            bool validGeometry = HasValidNavigationGeometry;
            if (validGeometry != _lastNavigationGeometryValid)
            {
                _lastNavigationGeometryValid = validGeometry;
                if (validGeometry)
                {
                    _configurationErrorLogged = false;
                }
                else
                {
                    LogInvalidNavigationGeometry("se bloqueara todo el trafico");
                }

                NavigationStateChanged?.Invoke(this);
            }

            if (hinge == null) return;

            hinge.localRotation = Quaternion.Slerp(hinge.localRotation, _targetRotation, openSpeed * Time.deltaTime);

            // Si el jugador entra en la trayectoria mientras la hoja baja, se
            // vuelve a abrir en lugar de cerrarle encima. Solo aplica con la
            // puerta realmente cerrandose: con la hoja ya en la pose cerrada
            // esto dejaria el cierre pedido y bloquearia la apertura.
            if (!_targetOpen
                && Quaternion.Angle(hinge.localRotation, Quaternion.identity) > 0.5f
                && IsPlayerInClosingPath())
            {
                _targetOpen = true;
                _targetRotation = _openRotation;
                return;
            }

            if (!_targetOpen || _isOpen)
                return;

            // No se habilita el paso hasta que la hoja ya se ha abierto el
            // angulo suficiente. Se mide el apertura real respecto de la pose
            // cerrada (identidad), no la distancia restante al objetivo.
            float passableAngle = Mathf.Min(Mathf.Abs(openAngle), 80f);
            float openedAngle = Quaternion.Angle(hinge.localRotation, Quaternion.identity);
            if (openedAngle >= passableAngle)
                SetLogicalOpen(true);
        }

        [ContextMenu("Build Example Door")]
        private void BuildExampleDoor()
        {
#if UNITY_EDITOR
            Transform root = transform;

            Transform panelT = root.Find("DoorPanel");
            if (panelT == null)
            {
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "DoorPanel";
                panel.transform.SetParent(root, false);
                panel.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                panel.transform.localScale = new Vector3(1.2f, 2.2f, 0.05f);
                panelT = panel.transform;
            }

            float halfWidth = Mathf.Max(panelT.localScale.x * 0.5f, 0.05f);
            Vector3 panelCenterLocal = root.InverseTransformPoint(panelT.position);

            Transform hingeT = root.Find("Hinge");
            if (hingeT == null)
            {
                GameObject hingeGo = new GameObject("Hinge");
                hingeT = hingeGo.transform;
                hingeT.SetParent(root, false);
                hingeT.localPosition = new Vector3(hingeSide * halfWidth, panelCenterLocal.y, panelCenterLocal.z);
                hingeT.localRotation = Quaternion.identity;
            }

            panelT.SetParent(hingeT, true);
            panelT.localRotation = Quaternion.identity;

            BuildFramePart("JambL", new Vector3(0.7f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 0.1f), root);
            BuildFramePart("JambR", new Vector3(-0.7f, 1.1f, 0f), new Vector3(0.1f, 2.2f, 0.1f), root);
            BuildFramePart("Lintel", new Vector3(0f, 2.25f, 0f), new Vector3(1.5f, 0.1f, 0.1f), root);

            if (root.position == Vector3.zero && root.rotation == Quaternion.identity)
                root.position = new Vector3(6f, 0f, 2f);

            if (doorPanel == null)
                doorPanel = panelT;
            if (hinge == null)
                hinge = hingeT;

            if (player == null)
            {
                GameObject playerGo = GameObject.Find("Player");
                if (playerGo != null)
                    player = playerGo.transform;
            }

            _targetRotation = Quaternion.identity;

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[DoorController] Puerta de bisagra de ejemplo construida. Guarda la escena.", this);
#endif
        }

#if UNITY_EDITOR
        private static void BuildFramePart(string name, Vector3 localPosition, Vector3 localScale, Transform parent)
        {
            if (parent.Find(name) != null)
                return;

            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
        }
#endif
    }
}