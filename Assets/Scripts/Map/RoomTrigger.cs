using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Map
{
    // Trigger de sala: cuando el JUGADOR entra en la sala le avisa al generador
    // para que corra la ventana de streaming (crea salas por delante y borra las
    // que quedaron detras). No hace nada mas: la seleccion de salas, las puertas
    // y el NavMesh los maneja ProceduralMapGenerator.
    //
    // El collider trigger lo arma el generador en runtime (un hijo 'SalaTrigger'
    // con un BoxCollider del tamano de la sala). El jugador usa CharacterController
    // (hereda de Collider y genera eventos de trigger), asi que no hace falta
    // Rigidbody en ninguna de las dos partes.
    [RequireComponent(typeof(BoxCollider))]
    public class RoomTrigger : MonoBehaviour
    {
        private ProceduralMapGenerator _generator;
        private int _index;
        private bool _ready;

        // Se llama desde el generador cuando spawnea la sala.
        public void Init(ProceduralMapGenerator generator, int index)
        {
            _generator = generator;
            _index = index;
            _ready = true;
        }

        // En editor, al agregar el componente ya deja el collider listo como trigger.
        private void Reset()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
                box.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_ready || _generator == null)
                return;

            // Solo el jugador mueve la ventana: enemigos e items no cuentan.
            if (other.GetComponentInParent<PlayerController>() == null)
                return;

            _generator.PlayerEnteredRoom(_index);
        }
    }
}
