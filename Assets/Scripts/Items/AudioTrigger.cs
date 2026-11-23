using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    //TP2 - Francisco Larrondo
    // Colision invisible que al interactuar reproduce un audio.
    // Asigna el clip desde el Inspector cuando lo tengas.
    [DisallowMultipleComponent]
    public class AudioTrigger : MonoBehaviour, IInteractable
    {
        [Header("Audio")]
        [Tooltip("Clip de audio que se reproduce al interactuar. Asignalo desde el Inspector.")]
        [SerializeField] private AudioClip audioClip;

        [Header("Opcional")]
        [SerializeField] private float volume = 1f;
        [Tooltip("Repetir si se interactua de nuevo.")]
        [SerializeField] private bool allowRepeat = true;
        [SerializeField] private string promptMessage = "Escuchar";

        private bool _alreadyPlayed;

        public bool TryPlay()
        {
            if (audioClip == null)
            {
                Debug.LogWarning($"[AudioTrigger] '{name}' no tiene audio asignado.", this);
                return false;
            }

            if (_alreadyPlayed && !allowRepeat)
                return true;

            AudioSource.PlayClipAtPoint(audioClip, transform.position, volume);
            _alreadyPlayed = true;

            return true;
        }

        public string GetPromptMessage()
        {
            return promptMessage;
        }

        // ---- IInteractable ----

        // Escuchar no levanta nada del suelo: no lleva flecha de proximidad.
        public bool IsGrabbable => false;

        // El clip es interactiva desde cualquier parte de su collider.
        public bool IsValidTarget(Collider hitCollider)
        {
            return true;
        }

        // Reproducir el audio no usa el inventario.
        public bool TryInteract(InventoryController inventory, Collider hitCollider)
        {
            return TryPlay();
        }
    }
}
