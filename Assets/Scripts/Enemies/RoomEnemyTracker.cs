using UnityEngine;

namespace PrisaControlada.Enemies
{
    //TP2 - Agustín Labarque
    // Se agrega automaticamente a cada enemigo spawneado por un EnemyLootSpawner.
    // Guarda su costo y, cuando el enemigo muere, avisa al RoomDirector para que
    // descuente ese costo del presupuesto de la sala y quede lugar para spawnear
    // otro. Esto puede ocurrir antes de que se destruya el GameObject: un zombie
    // muerto puede permanecer como cadaver hasta que cae fuera del mapa.
    //
    // No hace falta agregarlo a mano: lo pone el spawner al instanciar.
    public class RoomEnemyTracker : MonoBehaviour
    {
        private RoomDirector _director;
        private int _cost;
        private bool _unregistered;

        public int Cost => _cost;

        public void Init(RoomDirector director, int cost)
        {
            _director = director;
            _cost = Mathf.Max(0, cost);
        }

        internal void NotifyDeath()
        {
            if (_unregistered) return;
            _unregistered = true;

            // Si el director ya fue destruido (fin de escena), no hace nada.
            if (_director != null)
                _director.Unregister(this, _cost);
        }

        private void OnDestroy()
        {
            NotifyDeath();
        }
    }
}
