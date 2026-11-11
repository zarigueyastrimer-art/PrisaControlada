using UnityEngine;

namespace PrisaControlada.Items
{
    // Colision invisible que al interactuar reproduce un audio.
    // Asigna el clip desde el Inspector cuando lo tengas.
    [DisallowMultipleComponent]
    public class AudioTrigger : MonoBehaviour
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
    }
}
