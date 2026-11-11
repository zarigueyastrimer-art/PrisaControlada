using UnityEngine;
using PrisaControlada.Map;

namespace PrisaControlada.Items
{
    // Spawner de loot: un GameObject vacio que, al invocar TrySpawn(), elige un
    // item de la tabla segun sus pesos y lo instancia en el mundo. Tambien existe
    // un peso de "nada" que compite en el mismo sorteo, de modo que el spawner puede
    // no soltar absolutamente nada.
    //
    // Uso:
    //  - spawnOnStart = true  -> suelta al cargar la escena.
    //  - o llama LootSpawner.TrySpawn() desde un trigger/puerta/script.
    //
    // Los objetos instanciados se parentean a la raíz de la sala (RoomDefinition)
    // para que se destruyan automáticamente cuando la sala se despawn.
    public class LootSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class LootEntry
        {
            [Tooltip("Prefab a instanciar. Dejar vacio = cuenta como nada.")]
            public GameObject prefab;

            [Tooltip("Peso de aparicion. Mas peso = mas probable.")]
            [Min(0f)] public float weight = 1f;

            [Tooltip("Cantidad minima a soltar si este item gana.")]
            [Min(1)] public int minCount = 1;

            [Tooltip("Cantidad maxima a soltar si este item gana.")]
            [Min(1)] public int maxCount = 1;
        }

        [Header("Tabla de Loot")]
        [SerializeField] private LootEntry[] lootTable;
        [Tooltip("Peso del resultado 'no sale nada'. Compite contra los items.")]
        [SerializeField, Min(0f)] private float nothingWeight = 1f;

        [Header("Spawn")]
        [Tooltip("Radio maximo de separacion al instanciar (0 = posicion exacta).")]
        [SerializeField, Min(0f)] private float spawnRadius = 0f;
        [Tooltip("Rotacion aleatoria en Y al instanciar.")]
        [SerializeField] private bool randomRotation = true;
        [Tooltip("Soltar automaticamente al Start().")]
        [SerializeField] private bool spawnOnStart = false;
        [Tooltip("Si solo puede soltar una vez (ignora llamadas posteriores).")]
        [SerializeField] private bool spawnOnlyOnce = true;

        private bool _hasSpawned;
        private Transform _roomRoot;

        // Ultimo resultado instanciado (null si salio 'nada' o esta vacio).
        public GameObject LastSpawned { get; private set; }
        public bool HasSpawned => _hasSpawned;

        private void Awake()
        {
            // Cachear la raíz de la sala (el objeto con RoomDefinition)
            _roomRoot = GetComponentInParent<RoomDefinition>()?.transform ?? transform.root;
        }

        private void Start()
        {
            if (spawnOnStart) TrySpawn();
        }

        // Sortea y instancia. Devuelve el objeto instanciado, o null si salio 'nada'.
        public GameObject TrySpawn()
        {
            if (spawnOnlyOnce && _hasSpawned) return null;
            _hasSpawned = true;

            LootEntry chosen = RollEntry();
            if (chosen == null || chosen.prefab == null)
            {
                LastSpawned = null;
                return null;
            }

            int count = Random.Range(Mathf.Max(1, chosen.minCount), Mathf.Max(1, chosen.maxCount) + 1);
            GameObject last = null;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = transform.position;
                if (spawnRadius > 0f)
                {
                    Vector2 circle = Random.insideUnitCircle * spawnRadius;
                    pos += new Vector3(circle.x, 0f, circle.y);
                }

                Quaternion rot = transform.rotation;
                if (randomRotation)
                    rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                last = Instantiate(chosen.prefab, pos, rot);
                
                // Parentear el item a la sala para que se destruya con ella
                if (_roomRoot != null && last != null)
                    last.transform.SetParent(_roomRoot, true);
            }

            LastSpawned = last;
            return last;
        }

        // Reinicia el spawner para poder volver a soltar (util en debug).
        public void ResetSpawner()
        {
            _hasSpawned = false;
            LastSpawned = null;
        }

        // Caja visual de editor: se ve en la Scene, desaparece en Play (loot objects = azul).
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.7f);
            Gizmos.DrawCube(transform.position, Vector3.one * 0.125f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.125f);
        }

        // Devuelve la entrada ganadora segun pesos, o null si gano 'nada'.
        private LootEntry RollEntry()
        {
            float total = nothingWeight;
            if (lootTable != null)
            {
                foreach (LootEntry e in lootTable)
                    if (e != null && e.prefab != null)
                        total += Mathf.Max(0f, e.weight);
            }

            // Sin nada que soltar.
            if (total <= 0f) return null;

            float roll = Random.Range(0f, total);

            // Resta el peso de 'nada': si cae aca, no suelta nada.
            roll -= nothingWeight;
            if (roll < 0f) return null;

            if (lootTable != null)
            {
                foreach (LootEntry e in lootTable)
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
