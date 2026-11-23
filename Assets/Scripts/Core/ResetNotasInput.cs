using PrisaControlada.Items;
using PrisaControlada.Menu;
using PrisaControlada.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.Core
{
    //TP2 - Matías Morilla
    // Tecla rara para resetear el progreso de las notas: la P borra los ids
    // coleccionados del archivo de partida, para volver a buscarlas todas.
    // (P no se usa en ningún otro lado del juego.)
    //
    // Vive en las dos escenas: el setup crea el objeto "ResetNotas".
    // Si hay una nota/pantalla abierta, la tecla es de esa pantalla (cerrar):
    // por eso se trackea si había algo abierto el frame anterior y no se
    // resetea en ese frame, sin importar el orden de los Update.
    [DisallowMultipleComponent]
    public class ResetNotasInput : MonoBehaviour
    {
        private bool _uiAbierta;

        private void Update()
        {
            bool abiertaAhora = NoteViewer.IsOpen ||
                                NotaLoreViewer.IsOpen ||
                                CodeUI.IsOpen ||
                                NotasMenu.PopupAbierto;

            bool abiertaAntes = _uiAbierta;
            _uiAbierta = abiertaAhora;

            // La tecla de este frame la está usando una pantalla abierta.
            if (abiertaAhora || abiertaAntes) return;

            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.pKey.wasPressedThisFrame) return;

            Guardado.ResetearNotas();

            // Repintar el menú si está en pantalla (en la escena de juego no hay).
            if (NotasMenu.Instance != null)
                NotasMenu.Instance.Pintar();

            InteractionPrompt.Show("Progreso de notas reiniciado");
        }
    }
}
