using System.Collections.Generic;
using UnityEngine;

namespace PrisaControlada.Enemies
{
    // Director de sala: limita cuantos enemigos puede haber a la vez en una sala
    // usando un PRESUPUESTO DE COSTOS. Cada enemigo tiene un costo; la sala tiene
    // un costo maximo. Cuando la suma de los enemigos vivos alcanza el tope, no
    // deja spawnear mas hasta que alguno muera y se descuente su costo.
    //
    // Vive en el GameObject de la sala (padre de los spawners). Los spawners lo
    // buscan con GetComponentInParent<RoomDirector>() y le piden permiso antes de
    // spawnear. Sin director, los spawners spawnean libremente (modo prototipo).
    public class RoomDirector : MonoBehaviour
    {
        [Header("Presupuesto de la sala")]
        [Tooltip("Costo maximo de enemigos vivos al mismo tiempo en esta sala.")]
        [SerializeField] private int maxBudget = 3;

        private int _currentCost;
        private readonly List<RoomEnemyTracker> _alive = new List<RoomEnemyTracker>();

        public int MaxBudget => maxBudget;

        // Costo de los enemigos vivos ahora mismo.
        public int CurrentCost
        {
            get { Prune(); return _currentCost; }
        }

        // Cuanto presupuesto libre queda.
        public int Remaining
        {
            get { Prune(); return maxBudget - _currentCost; }
        }

        // True si todavia entra en el presupuesto un enemigo de ese costo.
        public bool CanSpawn(int cost)
        {
            Prune();
            return _currentCost + Mathf.Max(0, cost) <= maxBudget;
        }

        // Registra un enemigo ya instanciado y le suma su costo al presupuesto.
        // Llamar solo despues de haber confirmado CanSpawn(cost) == true.
        public void Register(GameObject enemy, int cost)
        {
            if (enemy == null) return;

            RoomEnemyTracker tracker = enemy.GetComponent<RoomEnemyTracker>();
            if (tracker == null) tracker = enemy.AddComponent<RoomEnemyTracker>();
            tracker.Init(this, cost);

            _alive.Add(tracker);
            _currentCost += Mathf.Max(0, cost);
        }

        // Lo llama el tracker cuando el enemigo muere (se destruye). Descuenta el
        // costo y lo saca de la lista de vivos.
        internal void Unregister(RoomEnemyTracker tracker, int cost)
        {
            _alive.Remove(tracker);
            _currentCost = Mathf.Max(0, _currentCost - Mathf.Max(0, cost));
        }

        // Destroza todos los enemigos vivos registrados en esta sala.
        public void DespawnAll()
        {
            Prune();
            foreach (RoomEnemyTracker t in _alive)
                if (t != null) Destroy(t.gameObject);

            _alive.Clear();
            _currentCost = 0;
        }

        // Defensa: saca de la lista cualquier tracker ya destruido (normalmente el
        // OnDestroy del tracker ya lo hizo, esto es solo red de seguridad).
        private void Prune()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null)
                    _alive.RemoveAt(i);
            }
        }
    }
}
