using UnityEngine;

namespace PrisaControlada.Enemies
{
    // Se agrega automaticamente a cada enemigo spawneado por un EnemyLootSpawner.
    // Guarda su costo y, cuando el enemigo muere (EnemyBase llama Destroy), el
    // OnDestroy avisa al RoomDirector para que descuente ese costo del presupuesto
    // de la sala y quede lugar para spawnear otro.
    //
    // No hace falta agregarlo a mano: lo pone el spawner al instanciar.
    public class RoomEnemyTracker : MonoBehaviour
    {
        private RoomDirector _director;
        private int _cost;

        public int Cost => _cost;

        public void Init(RoomDirector director, int cost)
        {
            _director = director;
            _cost = Mathf.Max(0, cost);
        }

        private void OnDestroy()
        {
            // Si el director ya fue destruido (fin de escena), no hace nada.
            if (_director != null)
                _director.Unregister(this, _cost);
        }
    }
}
