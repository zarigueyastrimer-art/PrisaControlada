using System.Collections;
using System.Collections.Generic;
using PrisaControlada.Core;
using UnityEngine;

namespace PrisaControlada.Map
{
    // Generador del mapa infinito de salas.
    //
    // Arranca desde el HUB que el jugador coloca a mano en la escena y encadena
    // salas del pool uniendo el Anchor_Out de la sala actual con el Anchor_In de
    // la siguiente. La puerta PuertaFuncional se instancia en un contenedor
    // aparte (nunca como hija de la sala).
    //
    // VENTANA DE STREAMING (defaults 5 / 3 / 2):
    //   - arranca con initialRoomsAlive salas vivas (el hub cuenta como la 1a),
    //   - al entrar a la sala i se crea hasta la sala i + roomsAhead,
    //   - se destruyen las salas que quedaron a mas de roomsBehind por detras.
    // Con esos valores: entrar a la 3a crea la 6a; entrar a la 4a crea la 7a y
    // destruye la 1a. El mapa es infinito: el juego termina solo al morir.
    //
    // NAVMesh GLOBAL (reemplaza al bake por sala): tras cada cambio de ventana
    // se rehornea RuntimeNavMesh (collectObjects = All) con BuildNavMesh
    // sincrono, de modo que salas + losas de umbral de las puertas quedan en
    // UNA sola malla sin juntas (el bake por sala dejaba una franja negra en
    // la boca de cada puerta y los enemigos no cruzaban). Corre en el mismo
    // frame del Instantiate y ANTES de que Unity ejecute los Start() de los
    // spawners, asi los enemigos de la sala nacen ya sobre superficie
    // caminable. La capa DOOR (3) no esta en la mascara del surface, asi que
    // los paneles de puerta nunca entran al bake, y la puerta no corta el
    // NavMesh (ver DoorController.ApplyNavMeshBlocker).
    public class ProceduralMapGenerator : MonoBehaviour
    {
        // Una sala viva: el objeto de escena, su RoomDefinition (instancia y
        // prefab de origen) y la puerta que ENTRA a esa sala (null en el hub).
        private sealed class SpawnedRoom
        {
            public GameObject Go;
            public RoomDefinition InstanceDef;
            public RoomDefinition SourceDef;
            public GameObject Door;
        }

        // Fila de la tabla de probabilidades por tipo (editable en el inspector).
        [System.Serializable]
        public class TypeWeightEntry
        {
            public RoomType type = RoomType.Pasillo;

            [Tooltip("Probabilidad de este tipo en el sorteo, en porcentaje (los 5 suman 100).")]
            [Min(0f)] public float weight = 20f;
        }

        [Header("Sala inicial")]
        [Tooltip("Hub colocado a mano en la escena. Si lo dejas vacio se busca una sala con tipo Hub.")]
        [SerializeField] private RoomDefinition hub;

        [Header("Pool de salas")]
        [Tooltip("Prefabs de sala con RoomDefinition (sin el hub). El tipo sale del RoomDefinition.")]
        [SerializeField] private RoomDefinition[] roomPool = new RoomDefinition[0];

        [Header("Puertas")]
        [Tooltip("Prefab PuertaFuncional. Se instancia en un contenedor aparte, nunca dentro de la sala.")]
        [SerializeField] private GameObject doorPrefab;

        [Tooltip("Prefab de puerta cerrada con llave (LockedDoor). Se usa en vez de doorPrefab " +
                 "cuando la sala de la que sale tiene RoomDefinition.lockExit marcado.")]
        [SerializeField] private GameObject lockedDoorPrefab;

        [Header("Ventana de streaming")]
        [Tooltip("Salas vivas al arrancar, contando el hub como la primera.")]
        [SerializeField, Min(1)] private int initialRoomsAlive = 5;

        [Tooltip("Salas que se mantienen creadas por delante de la sala del jugador.")]
        [SerializeField, Min(0)] private int roomsAhead = 3;

        [Tooltip("Salas que se mantienen vivas por detras de la sala del jugador.")]
        [SerializeField, Min(0)] private int roomsBehind = 2;

        [Header("Reglas de eleccion")]
        [Tooltip("Peso del combate cuando la sala con dos posiciones de distancia fue combate " +
                 "(o sea: una sala despues de un combate). 0.01 = casi nunca; 0.3 = poco frecuente. " +
                 "Justo despues de un combate el combate queda excluido (peso 0) siempre.")]
        [SerializeField, Min(0f)] private float combatCooldownMultiplier = 0.01f;

        [Tooltip("Rango del multiplicador aleatorio de peso para las salas de historia " +
                 "(peso bajo que varia en cada sorteo). (1,1) = peso fijo sin azar.")]
        [SerializeField] private Vector2 historyWeightRange = new Vector2(0.2f, 0.7f);

        [Header("Probabilidad por tipo de sala")]
        [Tooltip("Probabilidad BASE de cada tipo en el sorteo. La suma de los 5 es siempre 100% " +
                 "(el inspector tiene sliders que reajustan el resto solos). Si un tipo no esta " +
                 "en la tabla se usa 0.5 por defecto. La repartimos entre los prefabs de ese tipo: " +
                 "Pasillo 45% = 45% de las salas son pasillo (repartidas entre sus 4 prefabs).")]
        [SerializeField] private TypeWeightEntry[] typeWeights = new TypeWeightEntry[]
        {
            new TypeWeightEntry { type = RoomType.Pasillo,  weight = 45f },
            new TypeWeightEntry { type = RoomType.Combate,  weight = 15f },
            new TypeWeightEntry { type = RoomType.Loot,     weight = 15f },
            new TypeWeightEntry { type = RoomType.Puzzle,   weight = 15f },
            new TypeWeightEntry { type = RoomType.Historia, weight = 10f },
        };

        [Header("Trigger de sala")]
        [Tooltip("Margen que se achica la caja del trigger para que no se pisen las salas vecinas.")]
        [SerializeField, Min(0f)] private float triggerMargin = 0.5f;

        // ---- estado ----
        private readonly List<SpawnedRoom> _rooms = new List<SpawnedRoom>();
        private readonly HashSet<RoomDefinition> _warned = new HashSet<RoomDefinition>();
        private Transform _roomsContainer;
        private Transform _doorsContainer;
        private RuntimeNavMesh _globalNavMesh; // surface global (RuntimeNavMesh)
        private bool _rebakeScheduled;         // ya hay un rebake pedido para fin de frame
        private int _firstIndex;   // indice absoluto de _rooms[0]
        private int _current;      // indice absoluto de la sala en la que esta el jugador
        private bool _running;

        // Cantidad de salas vivas ahora mismo.
        public int AliveRooms => _rooms.Count;

        private void Start()
        {
            _roomsContainer = new GameObject("Salas").transform;
            _roomsContainer.SetParent(transform, false);
            _doorsContainer = new GameObject("Puertas").transform;
            _doorsContainer.SetParent(transform, false);

            if (hub == null)
            {
                foreach (RoomDefinition rd in Object.FindObjectsByType<RoomDefinition>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (rd.type == RoomType.Hub)
                    {
                        hub = rd;
                        break;
                    }
                }
            }

            if (hub == null)
            {
                Debug.LogError("[MapGen] No hay hub en la escena. Coloca la sala hub (RoomDefinition tipo Hub) y vuelve a jugar.", this);
                enabled = false;
                return;
            }

            _firstIndex = 0;
            _current = 0;
            _rooms.Add(new SpawnedRoom
            {
                Go = hub.gameObject,
                InstanceDef = hub,
                SourceDef = hub,
                Door = null
            });
            SetupTrigger(hub, 0);

            _running = true;

            // Ventana inicial: el hub ya cuenta como sala viva numero 1.
            int safety = 0;
            while (_rooms.Count < initialRoomsAlive && safety++ < 256)
                if (!SpawnNext())
                    break;

            // Bake global unico: hornea hub + salas recien creadas + losas de
            // umbral de sus puertas de una sola pieza (sincrono, antes de los
            // Start de los spawners).
            RebakeGlobal();

            Debug.Log($"[MapGen] Arranque: hub + {_rooms.Count - 1} salas (vivas={_rooms.Count}), " +
                      $"adelante={roomsAhead}, detras={roomsBehind}.");
        }

        // Lo llama RoomTrigger cuando el jugador entra en la sala de ese indice.
        public void PlayerEnteredRoom(int absoluteIndex)
        {
            if (!_running)
                return;

            if (absoluteIndex < _firstIndex || absoluteIndex >= _firstIndex + _rooms.Count)
                return; // sala vieja ya destruida

            if (absoluteIndex == _current)
                return;

            _current = absoluteIndex;
            RefreshWindow();
        }

        // Crea las salas que falten por delante y borra las que quedaron detras.
        private void RefreshWindow()
        {
            int limiteAdelante = _current + roomsAhead;
            int safety = 0;
            bool spawned = false;
            while (_firstIndex + _rooms.Count - 1 < limiteAdelante && safety++ < 256)
            {
                if (!SpawnNext())
                    break;
                spawned = true;
            }

            int minimoVivo = Mathf.Max(0, _current - roomsBehind);
            bool despawned = false;
            while (_firstIndex < minimoVivo && _rooms.Count > 1)
            {
                DespawnOldest();
                despawned = true;
            }

            // Rebake global tras el cambio de ventana (ver encabezado de la
            // clase). Si hubo spawn es sincrono: los spawners de la sala nueva
            // todavia no corrieron Start(). Los Destroy() de DespawnOldest se
            // aplican al terminar el frame, asi que si tambien hubo despawn se
            // pide otro rebake para el frame siguiente (sin duplicar).
            if (spawned)
                RebakeGlobal();
            if (despawned)
                ScheduleRebakeAfterDestroy();
        }

        // Salas que faltan por delante segun la ventana.
        private bool SpawnNext()
        {
            if (_rooms.Count == 0)
                return false;

            SpawnedRoom prev = _rooms[_rooms.Count - 1];
            RoomDefinition def = PickNext(prev);
            if (def == null)
                return false;

            int antes = _rooms.Count;
            SpawnRoom(prev, def);
            return _rooms.Count > antes;
        }

        // Instancia 'def' enganchada a la salida de 'prev' y arma su trigger.
        // El rebake global lo hace RefreshWindow/Start despues del spawn.
        private void SpawnRoom(SpawnedRoom prev, RoomDefinition def)
        {
            RoomDefinition prevDef = prev.InstanceDef != null ? prev.InstanceDef : prev.SourceDef;
            Transform outAnchor = prevDef != null ? prevDef.PrimaryOut : null;
            if (outAnchor == null)
            {
                Debug.LogError($"[MapGen] La sala '{prev.Go.name}' no tiene Anchor_Out; no se puede encadenar.", prev.Go);
                return;
            }

            GameObject go = Instantiate(def.gameObject);
            go.transform.SetParent(_roomsContainer, true);
            go.name = def.name;

            RoomDefinition inst = go.GetComponent<RoomDefinition>();
            if (inst == null || inst.anchorIn == null)
            {
                Debug.LogError($"[MapGen] El prefab '{def.name}' no tiene RoomDefinition con Anchor_In; sala descartada.", go);
                Destroy(go);
                return;
            }

            // Pose local del ancla de entrada dentro de la sala recien instanciada.
            Transform inAnchor = inst.anchorIn;
            Vector3 inLocalPos = go.transform.InverseTransformPoint(inAnchor.position);
            Quaternion inLocalRot = Quaternion.Inverse(go.transform.rotation) * inAnchor.rotation;

            // Queremos que el Anchor_In quede en el mismo punto que la salida de
            // la sala anterior, con los forward enfrentados (180 grados en Y).
            Quaternion wantedAnchorRot = FlatForward(outAnchor) * Quaternion.Euler(0f, 180f, 0f);
            Quaternion roomRot = wantedAnchorRot * Quaternion.Inverse(inLocalRot);
            Vector3 roomPos = outAnchor.position - roomRot * inLocalPos;
            go.transform.SetPositionAndRotation(roomPos, roomRot);

            // Puerta en el punto de union (contenedor aparte, fuera de la sala).
            // Si la sala de la que sale tiene lockExit, la puerta se instancia
            // cerrada con llave: el jugador necesita el KeyPickup para abrirla.
            GameObject doorPrefabToUse = doorPrefab;
            if (prevDef != null && prevDef.lockExit)
            {
                if (lockedDoorPrefab != null)
                {
                    doorPrefabToUse = lockedDoorPrefab;
                }
                else
                {
                    Debug.LogWarning($"[MapGen] '{prevDef.name}' tiene lockExit marcado pero no hay " +
                                     "lockedDoorPrefab asignado en el generador; se crea la puerta normal.", prev.Go);
                }
            }

            GameObject door = null;
            if (doorPrefabToUse != null)
                door = Instantiate(doorPrefabToUse, outAnchor.position, FlatForward(outAnchor), _doorsContainer);

            SpawnedRoom room = new SpawnedRoom
            {
                Go = go,
                InstanceDef = inst,
                SourceDef = def,
                Door = door
            };
            _rooms.Add(room);
            SetupTrigger(inst, _firstIndex + _rooms.Count - 1);
        }

        // Rehornea el NavMesh GLOBAL (RuntimeNavMesh, collectObjects = All) con
        // la geometria actual: salas + losas de umbral de las puertas de una
        // sola pieza, sin juntas entre superficies. La capa DOOR (3) no entra
        // en la mascara del surface (1335), asi que los paneles de puerta no
        // se hornean. BuildNavMesh es sincrono.
        private void RebakeGlobal()
        {
            if (_globalNavMesh == null)
                _globalNavMesh = FindFirstObjectByType<RuntimeNavMesh>();

            if (_globalNavMesh != null)
                _globalNavMesh.Rebake();
            else
                Debug.LogWarning("[MapGen] No hay RuntimeNavMesh en la escena; " +
                                 "no se pudo rehornear el NavMesh.", this);
        }

        // Pide un rebake para el frame siguiente (los Destroy() se aplican al
        // terminar el frame; si se hornea antes, la sala borrada seguiria en
        // la malla). Si ya hay uno pedido, no se duplica.
        private void ScheduleRebakeAfterDestroy()
        {
            if (!_rebakeScheduled)
                StartCoroutine(RebakeAfterDestroy());
        }

        private IEnumerator RebakeAfterDestroy()
        {
            _rebakeScheduled = true;
            yield return null;
            _rebakeScheduled = false;
            RebakeGlobal();
        }

        // Arma el hijo 'SalaTrigger' con un BoxCollider trigger del tamano de la
        // sala y lo conecta al generador.
        private void SetupTrigger(RoomDefinition def, int index)
        {
            Transform existing = def.transform.Find("SalaTrigger");
            GameObject triggerGo = existing != null ? existing.gameObject : new GameObject("SalaTrigger");
            if (existing == null)
                triggerGo.transform.SetParent(def.transform, false);

            triggerGo.layer = 0;

            BoxCollider box = triggerGo.GetComponent<BoxCollider>();
            if (box == null)
                box = triggerGo.AddComponent<BoxCollider>();

            Bounds local = GetLocalBounds(def.transform);
            Vector3 size = local.size;
            size.x = Mathf.Max(0.5f, size.x - triggerMargin);
            size.z = Mathf.Max(0.5f, size.z - triggerMargin);
            size.y = Mathf.Max(0.5f, size.y);

            box.isTrigger = true;
            box.center = local.center;
            box.size = size;

            RoomTrigger rt = triggerGo.GetComponent<RoomTrigger>();
            if (rt == null)
                rt = triggerGo.AddComponent<RoomTrigger>();
            rt.Init(this, index);
        }

        // Bounds de TODA la sala en el espacio local de su raiz (aguanta rotacion).
        private static Bounds GetLocalBounds(Transform root)
        {
            bool set = false;
            Bounds b = new Bounds(Vector3.zero, Vector3.one * 4f);
            Matrix4x4 toLocal = root.worldToLocalMatrix;
            Vector3 sign = Vector3.one;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer)
                    continue;

                Bounds wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    sign.x = (i & 1) == 0 ? -1f : 1f;
                    sign.y = (i & 2) == 0 ? -1f : 1f;
                    sign.z = (i & 4) == 0 ? -1f : 1f;
                    Vector3 corner = wb.center + Vector3.Scale(wb.extents, sign);
                    Vector3 local = toLocal.MultiplyPoint3x4(corner);
                    if (!set)
                    {
                        b = new Bounds(local, Vector3.zero);
                        set = true;
                    }
                    else
                    {
                        b.Encapsulate(local);
                    }
                }
            }

            return b;
        }

        // Forward del ancla achatado al plano horizontal (los anclajes son 2D en Y).
        private static Quaternion FlatForward(Transform anchor)
        {
            Vector3 f = anchor.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-5f)
                f = Vector3.forward;
            return Quaternion.LookRotation(f.normalized, Vector3.up);
        }

        // Sorteo de la proxima sala aplicando las reglas:
        //  - nunca el mismo prefab dos veces seguidas,
        //  - nunca combate justo tras combate (excluido, peso 0),
        //  - con una sala de distancia tras un combate, combate x combatCooldownMultiplier,
        //  - historia con peso aleatorio bajo (historyWeightRange).
        // El peso base de cada sala es: peso del prefab x (probabilidad del tipo
        // / cantidad de prefabs de ese tipo). typeWeights suma siempre 100.
        private RoomDefinition PickNext(SpawnedRoom prev)
        {
            if (roomPool == null || roomPool.Length == 0)
            {
                Debug.LogError("[MapGen] El pool de salas esta vacio.", this);
                return null;
            }

            SpawnedRoom prevPrev = _rooms.Count >= 2 ? _rooms[_rooms.Count - 2] : null;

            bool prevIsCombat = prev.InstanceDef != null && prev.InstanceDef.type == RoomType.Combate;
            bool prevPrevIsCombat = prevPrev != null && prevPrev.InstanceDef != null &&
                                    prevPrev.InstanceDef.type == RoomType.Combate;
            bool penalizarCombate = prevPrevIsCombat && !prevIsCombat;
            RoomDefinition prevSource = prev.SourceDef;

            RoomDefinition elegida = Sortear(prevSource, prevIsCombat, penalizarCombate, true);

            // Si la regla de "no repetir" vacio todo el pool (pool de una sola
            // sala), se repite igual antes que frenar el mapa.
            if (elegida == null)
            {
                Debug.LogWarning("[MapGen] No hay salas validas sin repetir la ultima; se permite repetir.");
                elegida = Sortear(prevSource, prevIsCombat, penalizarCombate, false);
            }

            return elegida;
        }

        // Peso del tipo en la tabla typeWeights del inspector. Si el tipo no
        // esta en la tabla se usa 10% por defecto.
        private float PesoTipo(RoomType t)
        {
            if (typeWeights != null)
            {
                for (int i = 0; i < typeWeights.Length; i++)
                {
                    TypeWeightEntry e = typeWeights[i];
                    if (e != null && e.type == t)
                        return Mathf.Max(0f, e.weight);
                }
            }

            return 10f;
        }

        // Prefabs del pool que son de ese tipo. El peso del tipo se reparte entre
        // ellos: la fila del inspector es la probabilidad del TIPO ENTERO (pasillo
        // 45% = 45% de las salas son pasillo, repartidas entre sus 4 prefabs).
        private int CantidadDelTipo(RoomType t)
        {
            int n = 0;
            if (roomPool != null)
            {
                foreach (RoomDefinition def in roomPool)
                    if (def != null && def.type == t)
                        n++;
            }

            return Mathf.Max(1, n);
        }

        private RoomDefinition Sortear(RoomDefinition prevSource, bool prevIsCombat,
                                       bool penalizarCombate, bool sinRepetir)
        {
            float total = 0f;
            List<RoomDefinition> candidatas = new List<RoomDefinition>();
            List<float> pesos = new List<float>();

            foreach (RoomDefinition def in roomPool)
            {
                if (def == null)
                    continue;
                if (def.type == RoomType.Hub)
                    continue; // el hub no se genera: es solo la sala inicial
                if (sinRepetir && def == prevSource)
                    continue; // nunca el mismo prefab dos veces seguidas

                if (!def.CanBeChained)
                {
                    if (_warned.Add(def))
                        Debug.LogError($"[MapGen] '{def.name}' no tiene Anchor_In/Anchor_Out; se saltea. " +
                                       "Corre Tools > PrisaControlada > Mapa > Preparar Anclajes.", def);
                    continue;
                }

                // Peso final = peso del prefab x (probabilidad del tipo / cantidad
                // de prefabs de ese tipo), para que el slider del tipo sea la
                // probabilidad del tipo entero.
                float w = Mathf.Max(0f, def.weight) * PesoTipo(def.type) / CantidadDelTipo(def.type);
                if (def.type == RoomType.Combate)
                {
                    if (prevIsCombat)
                        continue; // justo despues de un combate: combate excluido (peso 0)
                    if (penalizarCombate)
                        w *= combatCooldownMultiplier; // una sala de distancia: muy poco probable
                }

                if (def.type == RoomType.Historia)
                    w *= Random.Range(Mathf.Min(historyWeightRange.x, historyWeightRange.y),
                                      Mathf.Max(historyWeightRange.x, historyWeightRange.y));

                if (w <= 0f)
                    continue;

                candidatas.Add(def);
                pesos.Add(w);
                total += w;
            }

            if (total <= 0f)
                return null;

            float roll = Random.Range(0f, total);
            for (int i = 0; i < candidatas.Count; i++)
            {
                roll -= pesos[i];
                if (roll <= 0f)
                    return candidatas[i];
            }

            return candidatas[candidatas.Count - 1];
        }

        // Destruye la sala mas vieja y la puerta que entraba a ella.
        private void DespawnOldest()
        {
            if (_rooms.Count == 0)
                return;

            SpawnedRoom oldest = _rooms[0];
            _rooms.RemoveAt(0);
            _firstIndex++;

            if (oldest.Door != null)
                Destroy(oldest.Door);
            if (oldest.Go != null)
                Destroy(oldest.Go);
        }
    }
}
