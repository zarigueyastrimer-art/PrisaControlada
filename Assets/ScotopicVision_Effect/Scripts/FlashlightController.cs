using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de linterna.
///
/// La linterna va como hijo de la cámara para apuntar donde mira el jugador.
/// Se prende y apaga con F, y la intensidad sube/baja de forma suave para que
/// no sea un flash seco. Cuando está apagada, el jugador queda a oscuras y
/// entra en juego <see cref="DarkAdaptation"/>.
/// </summary>
[RequireComponent(typeof(Light))]
public class FlashlightController : MonoBehaviour
{
    [Header("Tecla")]
    [Tooltip("Tecla para prender/apagar. Requiere Input System.")]
    public Key toggleKey = Key.F;

    [Header("Linterna")]
    [Tooltip("Intensidad cuando está prendida al máximo.")]
    public float onIntensity = 12f;

    [Tooltip("Segundos para prenderse/apagarse del todo.")]
    public float rampTime = 0.12f;

    Light _light;
    float _target;      // 0 = apagada, 1 = prendida
    float _current;

    /// <summary>True si la linterna está encendida.</summary>
    public bool IsOn => _target > 0.5f;

    void Awake()
    {
        _light = GetComponent<Light>();

        // Arranca apagada: el jugador empieza a oscuras.
        _target = 0f;
        _current = 0f;
        _light.intensity = 0f;
    }

    void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            _target = _target > 0.5f ? 0f : 1f;
        }

        // Avance suave hacia el objetivo. Sin deltaTime negativo ni saltos.
        float step = Time.deltaTime / Mathf.Max(0.001f, rampTime);
        _current = Mathf.MoveTowards(_current, _target, step);
        _light.intensity = onIntensity * _current;
    }
}
