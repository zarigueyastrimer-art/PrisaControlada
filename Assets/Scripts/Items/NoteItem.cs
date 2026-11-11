using UnityEngine;

namespace PrisaControlada.Items
{
    // Nota interactiva. Va en un GameObject con collider (trigger o no).
    // Al interactuar (tecla E), muestra la imagen asignada en pantalla y pausa el juego.
    [DisallowMultipleComponent]
    public class NoteItem : MonoBehaviour
    {
        [Header("Imagen de la Nota")]
        [Tooltip("Imagen que se muestra al interactuar. Arrastra un Texture2D aqui.")]
        [SerializeField] private Texture2D noteImage;

        [Header("Opcional")]
        [Tooltip("Mensaje que se muestra al mirar la nota (si esta vacio no muestra nada).")]
        [SerializeField] private string promptMessage = "Leer nota";

        [SerializeField] private AudioClip pickupSound;

        private bool _alreadyRead;

        // Llamado por InventoryController.TryGrab() cuando el raycast impacta este objeto.
        public bool TryRead()
        {
            if (noteImage == null)
            {
                Debug.LogWarning($"[NoteItem] '{name}' no tiene imagen asignada.", this);
                return false;
            }

            NoteViewer.Show(noteImage);
            _alreadyRead = true;

            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }

            return true;
        }

        public string GetPromptMessage()
        {
            return promptMessage;
        }
    }
}
