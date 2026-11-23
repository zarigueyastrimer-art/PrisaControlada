using UnityEngine;
using PrisaControlada.Player;

namespace PrisaControlada.Items
{
    //TP2 - Francisco Larrondo
    // Contrato de todo objeto con el que el jugador puede interactuar.
    //
    // Antes, InventoryController.TryGrab() tenia una cadena de 9 if (uno por
    // tipo: cajon, nota, audio, puerta, 4 tipos de municion y pickup). Al meter
    // esta interfaz, cada objeto decide el mismo si es tocable y que pasa cuando
    // se lo toca, y el jugador solo hace UN GetComponentInParent<IInteractable>.
    //
    // Esto es lo que permite tratar igual a clases que NO comparten herencia:
    // DoorController, NoteItem y AmmoPickup no tienen ninguna superclase en
    // comun entre si, pero al implementar IInteractable se vuelven polimorficas.
    public interface IInteractable
    {
        // Valida si el collider impactado corresponde a una parte interactiva.
        // Devuelve false cuando el objeto existe en la jerarquia pero se pego
        // en una parte que no reacciona: el marco de la puerta (no la hoja) o
        // el mueble (no el cajon). Sirve para que la mira no se cierre.
        bool IsValidTarget(Collider hitCollider);

        // Si el objeto se puede levantar del suelo (pickup y municion).
        // ProximityHighlighter usa esto para poner la flecha de proximidad:
        // nunca la pone sobre puertas, cajones ni notas, pero tampoco hace
        // una lista de tipos: le pregunta a cada objeto.
        bool IsGrabbable { get; }

        // Ejecuta la interaccion. Devuelve true si la consumio.
        // El inventario se pasa por parametro porque hay objetos que necesitan
        // leerlo (llave en mano) o avisarle que cambio (la municion suma balas
        // y el pickup entra a la mano).
        bool TryInteract(InventoryController inventory, Collider hitCollider);
    }
}
