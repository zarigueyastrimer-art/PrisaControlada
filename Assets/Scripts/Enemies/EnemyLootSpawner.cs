using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PrisaControlada.Map;

namespace PrisaControlada.Enemies
{
    // Spawner de enemigos con tabla ponderada (igual que LootSpawner pero para
    // enemigos). Cada entrada tiene un tipo de enemigo y un peso; ademas hay un
    // peso de "no sale nada" que compite en el mismo sorteo. Los enemigos se
    // instancian desde los prefabs que elijas y se anclan al NavMesh para caminar.
    //
    // Uso:
    //  - spawnOnStart = true  -> suelta al cargar la escena.
    //  - o llama EnemyLootSpawner.TrySpawn() desde un trigger/zona/script.
    //
    // Los enemigos instanciados se parentean a la raíz de la sala (RoomDefinition)
    // para que se destruyan automáticamente cuando la sala se despawn.
    public class EnemyLootSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class EnemyEntry
        {
            [Tooltip("Prefab del enemigo. Dejar vacio = cuenta como nada.")]
            public GameObject prefab;

            [Tooltip("Peso de aparicion. Mas peso = mas probable.")]
            [Min(0f)] public float weight = 1f;

            [Tooltip("Costo de este enemigo para el presupuesto de la sala.")]
            [Min(0)] public int cost = 1;

            [Tooltip("Cantidad minima si este tipo gana.")]
            [Min(1)] public int minCount = 1;

            [Tooltip("Cantidad maxima si este tipo gana.")]
            [Min(1)] public int maxCount = 1;
        }

        [Header("Tabla de Enemigos")]
        [SerializeField] private EnemyEntry[] enemyTable;
        [Tooltip("Peso del resultado 'no sale nada'. Compite contra los enemigos.")]
        [SerializeField, Min(0f)] private float nothingWeight = 1f;

        [Header("Spawn")]
        [Tooltip("Radio maximo de separacion al instanciar (0 = posicion exacta).")]
        [SerializeField, Min(0f)] private float spawnRadius = 3f;
        [Tooltip("Distancia maxima para anclar al NavMesh.")]
        [SerializeField, Min(0.1f)] private float navMeshSnapRange = 5f;
        [Tooltip("Soltar automaticamente al Start().")]
        [SerializeField] private bool spawnOnStart = false;
        [Tooltip("Si solo puede soltar una vez (ignora llamadas posteriores).")]
        [SerializeField] private bool spawnOnlyOnce = true;

        private bool _hasSpawned;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private RoomDirector _director;
        private Transform _roomRoot;

        // Enemigos creados por este spawner.
        public IReadOnlyList<GameObject> Spawned => _spawned;
        public bool HasSpawned => _hasSpawned;

        private void Awake()
        {
            // Cachear la raíz de la sala (el objeto con RoomDefinition)
            _roomRoot = GetComponentInParent<RoomDefinition>()?.transform ?? transform.root;
        }

        private void Start()
        {
            // El director vive en un ancestro (la sala). Si no hay, spawnea libre.
            _director = GetComponentInParent<RoomDirector>();

            if (spawnOnStart) TrySpawn();
        }

        // Sortea y crea los enemigos. Devuelve el ultimo creado, o null si salio
        // 'nada' o la sala no tiene presupuesto suficiente.
        public GameObject TrySpawn()
        {
            if (spawnOnlyOnce && _hasSpawned) return null;
            _hasSpawned = true;

            if (_director == null)
                _director = GetComponentInParent<RoomDirector>();

            EnemyEntry chosen = RollEntry();
            if (chosen == null) return null;

            int count = Random.Range(Mathf.Max(1, chosen.minCount), Mathf.Max(1, chosen.maxCount) + 1);
            GameObject last = null;

            for (int i = 0; i < count; i++)
            {
                // Si hay director y no queda presupuesto para este costo, no spawnea.
                if (_director != null && !_director.CanSpawn(chosen.cost))
                    continue;

                Vector3 pos = transform.position;
                if (spawnRadius > 0f)
                {
                    Vector2 circle = Random.insideUnitCircle * spawnRadius;
                    pos += new Vector3(circle.x, 0f, circle.y);
                }

                pos = SnapToNavMesh(pos);

                // Rotación aleatoria en Y
                Quaternion randomRot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                GameObject enemy = Instantiate(chosen.prefab, pos, randomRot);
                
                // Parentear el enemigo a la sala para que se destruya con ella
                if (_roomRoot != null && enemy != null)
                    enemy.transform.SetParent(_roomRoot, true);
                
                _spawned.Add(enemy);

                // Registra el costo en el director de la sala.
                if (_director != null)
                    _director.Register(enemy, chosen.cost);

                last = enemy;
            }

            return last;
        }

        // Reinicia el spawner para poder volver a soltar (util en debug).
        public void ResetSpawner()
        {
            _hasSpawned = false;
        }

        // Destroza todos los enemigos creados por este spawner.
        public void DespawnAll()
        {
            foreach (GameObject e in _spawned)
                if (e != null) Destroy(e);
            _spawned.Clear();
        }

        // Ancla la posicion al NavMesh para que el NavMeshAgent pueda caminar.
        private Vector3 SnapToNavMesh(Vector3 pos)
        {
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, navMeshSnapRange, NavMesh.AllAreas))
                return hit.position;
            return pos;
        }

        // Caja visual de editor: se ve en la Scene, desaparece en Play (enemigos = rojo).
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
            Gizmos.DrawCube(transform.position, Vector3.one * 0.125f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.125f);
        }

        // Devuelve la entrada ganadora segun pesos, o null si gano 'nada'.
        private EnemyEntry RollEntry()
        {
            float total = nothingWeight;
            if (enemyTable != null)
            {
                foreach (EnemyEntry e in enemyTable)
                    if (e != null && e.prefab != null) total += Mathf.Max(0f, e.weight);
            }

            if (total <= 0f) return null;

            float roll = Random.Range(0f, total);

            // Resta el peso de 'nada': si cae aca, no suelta nada.
            roll -= nothingWeight;
            if (roll < 0f) return null;

            if (enemyTable != null)
            {
                foreach (EnemyEntry e in enemyTable)
                {
                    if (e == null || e.prefab == null) continue;
                    float w = Mathf.Max(0f, e.weight);
                    if (roll < w) return e;
                    roll -= w;
                }
            }

            return null;
        }
    }
}
