using UnityEngine;

namespace PrisaControlada.Enemies
{
    // Elige UN EnemyLootSpawner al azar de sus hijos y lo dispara.
    //
    // Sirve para salas con varios puntos de spawn posibles (por ejemplo la
    // llave de la sala puzzle) donde debe salir UNA sola vez pero en un punto
    // distinto en cada partida. Sin esto, todos los spawners con spawnOnStart
    // compiten por el presupuesto de la sala (RoomDirector) y siempre gana el
    // primero en el orden del arbol: la llave saldria siempre en el mismo lugar.
    //
    // Los spawners hijos tienen que tener spawnOnStart = false: este script es
    // el unico que los llama.
    public class RandomSpawnSelector : MonoBehaviour
    {
        [Tooltip("Dispara el sorteo en el Start().")]
        [SerializeField] private bool spawnOnStart = true;

        [Tooltip("Avisa en la consola si no hay spawners o si el sorteo no creo nada.")]
        [SerializeField] private bool warnIfNothing = true;

        private void Start()
        {
            if (spawnOnStart)
                Select();
        }

        // Elige un spawner al azar y lo hace spawnear. Devuelve lo que creo
        // (null si no hay spawners o si el spawner no solto nada).
        public GameObject Select()
        {
            // Solo spawners activos: un spawner inactivo nunca corrio su Awake
            // y la llave no se parentaria a la sala (quedaria en el mundo
            // aunque la sala se destruya).
            EnemyLootSpawner[] spawners = GetComponentsInChildren<EnemyLootSpawner>();
            if (spawners.Length == 0)
            {
                if (warnIfNothing)
                    Debug.LogWarning($"[RandomSpawnSelector] '{name}' no tiene EnemyLootSpawner hijos.", this);
                return null;
            }

            EnemyLootSpawner chosen = spawners[Random.Range(0, spawners.Length)];
            GameObject created = chosen.TrySpawn();

            if (created == null && warnIfNothing)
                Debug.LogWarning($"[RandomSpawnSelector] El spawner '{chosen.name}' no solto nada. " +
                                 "Revisa pesos, nothingWeight y el presupuesto de la sala (RoomDirector).", chosen);

            return created;
        }
    }
}
