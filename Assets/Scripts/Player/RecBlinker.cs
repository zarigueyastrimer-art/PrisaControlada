using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    //TP2 - Ian Rosenberg
    // Parpadeo del punto rojo "REC" del overlay de camara (CameraHUD_Canvas).
    // Solo alterna la visibilidad del punto a un ritmo fijo; el texto "rec"
    // al lado queda estatico. Usa tiempo sin escala para que el parpadeo
    // siga aunque el juego este pausado (codigo, nota, muerte).
    //
    // No crea UI: el RawImage del punto debe existir en la jerarquia (lo crea
    // la tool de Editor "PrisaControlada/Configurar CameraHUD").
    [DisallowMultipleComponent]
    public class RecBlinker : MonoBehaviour
    {
        [Header("UI (el componente vive en el punto rojo)")]
        [Tooltip("RawImage del punto rojo. Si se deja vacio, se toma el del mismo GameObject.")]
        [SerializeField] private RawImage punto;

        [Header("Comportamiento")]
        [Tooltip("Segundos encendido y segundos apagado (ciclo completo = 2x este valor).")]
        [SerializeField, Min(0.05f)] private float blinkInterval = 0.5f;

        private void Awake()
        {
            if (blinkInterval < 0.05f)
                blinkInterval = 0.05f;

            if (punto == null)
                punto = GetComponent<RawImage>();

            if (punto == null)
            {
                Debug.LogError("[RecBlinker] Falta el RawImage del punto rojo. Componente deshabilitado.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            // Metade del ciclo encendida, metade apagada.
            bool on = Mathf.FloorToInt(Time.unscaledTime / blinkInterval) % 2 == 0;

            if (punto.enabled != on)
                punto.enabled = on;
        }
    }
}
