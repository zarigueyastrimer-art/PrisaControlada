using PrisaControlada.Core;
using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    //TP2 - Matías Morilla
    // Nota de lore recogible. Va en el objeto físico de la escena
    // ("NotaLore": el quad con BoxCollider). Al interactuar (tecla E):
    //  1) resuelve qué nota del catálogo toca (id fijo o al azar entre las
    //     que todavía no coleccionaste);
    //  2) la abre en NotaLoreViewer (pausa + cualquier tecla para cerrar);
    //  3) guarda su id en el archivo de partida para que el menú la muestre.
    //
    // El objeto NO se destruye al leer: se puede volver a leer (y re-leer
    // no duplica la nota en la colección).
    [DisallowMultipleComponent]
    public class NotaLore : MonoBehaviour, IInteractable
    {
        [Header("Catálogo")]
        [Tooltip("Si se deja vacío se carga Resources/NotasCatalogo (el que crea el setup).")]
        [SerializeField] private NotasCatalogo catalogo;

        [Header("Nota")]
        [Tooltip("Id fijo del catálogo para ESTA nota. Vacío = al azar entre las que faltan.")]
        [SerializeField] private string notaId = "";

        [Tooltip("Mensaje que se muestra al mirar la nota (si esta vacio no muestra nada).")]
        [SerializeField] private string promptMessage = "Leer nota";

        [SerializeField] private AudioClip pickupSound;

        // Id resuelto en la primera lectura: la segunda vez muestra la misma.
        private string _notaElegida;

        // Llamado por InventoryController.TryGrab() cuando el raycast impacta este objeto.
        public bool TryRead()
        {
            NotasCatalogo cat = Catalogo();
            if (cat == null)
            {
                Debug.LogWarning("[NotaLore] No hay catálogo de notas: " +
                                 "corré Tools > PrisaControlada > Notas: Setup.", this);
                return false;
            }

            if (NotaLoreViewer.Instance == null)
            {
                Debug.LogWarning("[NotaLore] No hay NotaLoreViewer en la escena: " +
                                 "corré Tools > PrisaControlada > Notas: Setup.", this);
                return false;
            }

            if (string.IsNullOrEmpty(_notaElegida))
                _notaElegida = ResolverId(cat);

            if (string.IsNullOrEmpty(_notaElegida))
            {
                // Sin id fijo y ya coleccionó todas las del catálogo.
                InteractionPrompt.Show("Ya encontraste todas las notas.");
                return false;
            }

            NotasCatalogo.Nota nota = cat.Buscar(_notaElegida);
            if (nota == null)
            {
                Debug.LogWarning($"[NotaLore] El id '{_notaElegida}' no está en el catálogo.", this);
                return false;
            }

            if (!NotaLoreViewer.Mostrar(nota))
                return false;

            // Persistir la colección (idempotente: re-leer no duplica).
            Guardado.AgregarNota(nota.id);

            if (pickupSound != null)
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);

            return true;
        }

        public string GetPromptMessage()
        {
            return promptMessage;
        }

        // El catálogo se busca solo la primera vez y queda cacheado.
        private NotasCatalogo Catalogo()
        {
            if (catalogo == null)
                catalogo = Resources.Load<NotasCatalogo>("NotasCatalogo");

            return catalogo;
        }

        private string ResolverId(NotasCatalogo cat)
        {
            // 1) Id fijo puesto en el inspector.
            if (!string.IsNullOrEmpty(notaId))
            {
                if (cat.Buscar(notaId) == null)
                    Debug.LogWarning($"[NotaLore] El id '{notaId}' no está en el catálogo.", this);

                return notaId;
            }

            // 2) Al azar entre las que todavía no están coleccionadas.
            NotasCatalogo.Nota elegida = cat.ElegirNoColeccionada(Guardado.CargarNotas());
            return elegida != null ? elegida.id : null;
        }

        // ---- IInteractable ----

        // Leer no levanta nada del suelo: no lleva flecha de proximidad.
        public bool IsGrabbable => false;

        // La nota es interactiva desde cualquier parte de su collider.
        public bool IsValidTarget(Collider hitCollider)
        {
            return true;
        }

        // Leer no toca el inventario: solo muestra el texto y pausa el juego.
        public bool TryInteract(InventoryController inventory, Collider hitCollider)
        {
            return TryRead();
        }
    }
}
