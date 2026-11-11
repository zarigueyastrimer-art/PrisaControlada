using UnityEngine;

/// <summary>Estilo del ruido neural que se empuja al shader.</summary>
public enum NeuralNoiseMode
{
    /// <summary>Scroll direccional diagonal (el original: sensacion de viento).</summary>
    DirectionalScroll = 0,
    /// <summary>Estatica de TV / grano: parpadeo aleatorio por celda, sin direccion.</summary>
    StaticGrain = 1,
    /// <summary>Particulas discretas: celdas sueltas que destellan en instantes aleatorios.</summary>
    CellParticles = 2
}

/// <summary>
/// Empuja el look escotopico (Eigengrau, ruido neural, vision periferica, blur
/// y ganancia retiniana) al shader Hidden/PCHZ/ScotopicVision, usando la
/// adaptacion que calcula <see cref="DarkAdaptation"/>.
///
/// No mide la luz: esa logica ya vive en DarkAdaptation (oclusion, carried
/// light, caches de luces...). Este componente es solo la traduccion de esa
/// adaptacion 0-1 a los parametros del shader del video de referencia.
///
/// Todos los parametros clave son PUBLICOS para poder ir ajustandolos en vivo
/// desde otro script (o el inspector) sin tener que recompilar.
///
/// Debe correr en el MISMO player que tiene DarkAdaptation.
/// </summary>
[RequireComponent(typeof(DarkAdaptation))]
public class ScotopicVisionController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Se resuelve solo si se deja vacio. Debe ser el DarkAdaptation del player.")]
    [SerializeField] private DarkAdaptation adaptation;

    [Tooltip("Se resuelve solo si se deja vacio. Debe ser el FlashlightController " +
             "(normalmente en la camara hija del player).")]
    [SerializeField] private FlashlightController flashlight;

    [Header("Linterna")]
    [Tooltip("Si esta ON, el filtro escotopico se APAGA por completo cuando la linterna " +
             "esta encendida (la escena se ve cruda, con color y sin ruido). Biologicamente " +
             "correcto: con luz intensa los bastones se saturan y manda la vision de conos " +
             "(fotopica). OFF = el efecto sigue aplicandose aunque la linterna este prendida.")]
    public bool turnOffWithFlashlight = true;

    [Header("Fondo negro con la linterna")]
    [Tooltip("Si esta ON, al prender la linterna se oscurecen las luces generales (ambiental " +
             "y directional) para que SOLO ilumine la linterna: fuera del cono queda NEGRO " +
             "absoluto. Se restauran solas al apagar la linterna.")]
    public bool darkenAmbientWithFlashlight = true;

    [Tooltip("Color de luz ambiental cuando la linterna esta apagada (la escena oscura base).")]
    public Color darkAmbientColor = new Color(0.032f, 0.032f, 0.04f, 1f);

    [Tooltip("Intensidad de la luz Directional cuando la linterna esta apagada.")]
    public float darkDirectionalIntensity = 0.18f;

    [Tooltip("Direccional de la escena. Se resuelve sola si se deja vacia.")]
    [SerializeField] private Light directionalLight;

    [Tooltip("Segundos en fundir las luces generales entre su valor normal y negro " +
             "al prender/apagar la linterna.")]
    public float flashlightFadeTime = 0.35f;

    [Header("=== Controles maestros del filtro ===")]
    [Tooltip("Prende/Apaga todo el filtro escotopico. OFF = ves la escena cruda, sin ningún efecto.")]
    public bool filterEnabled = true;

    [Tooltip("Fuerza global del efecto (0 = escena cruda, 1 = efecto completo). " +
             "Permite fundir el filtro suavemente o dosificar su intensidad.")]
    [Range(0f, 1f)] public float filterStrength = 1f;

    [Tooltip("Gama (curva tonal) del look escotópico. >1 = más oscuro/contraste, " +
             "<1 = más claro/plano. 1 = sin cambio. No toca el cono de la linterna.")]
    [Range(0.25f, 4f)] public float filterGamma = 1f;

    [Tooltip("Piso mínimo de oscuridad: evita el negro puro en la zona no iluminada " +
             "(los bastones siempre ven algo). 0.35 = valor original del video.")]
    [Range(0f, 1f)] public float minDarknessFloor = 0.35f;

    [Header("Cual mide la linterna")]
    [Tooltip("Umbral de luminancia bajo el cual un pixel iluminado por la linterna aun cuenta como 'oscuro'." +
             "Mas alto = la linterna 'limpia' el efecto antes (cono mas nitido).")]
    [Range(0.5f, 50f)] public float localLitSensitivity = 6f;

    [Header("Eigengrau (piso de la retina)")]
    [Tooltip("Gris intrinseco que la retina ve en negro total. Nunca se deja negro puro.")]
    public Color eigengrauColor = new Color(0.030f, 0.030f, 0.038f, 1f);
    [Tooltip("Altura del piso. 1 = gris intrinseco completo; menos = un poco mas oscuro.")]
    [Range(0f, 1f)] public float eigengrauAmount = 0.6f;

    [Header("Ganancia retiniana (revela formas tenues)")]
    [Tooltip("Amplifica la escena oscura al adaptarse, para que emerjan las formas " +
             "sin hacer un 'boost' de exposicion a lo bruto.")]
    [Range(0f, 16f)] public float scotopicGain = 3f;

    [Header("Monocromatismo (bastones)")]
    [Range(0f, 1f)] public float monoAmount = 0.95f;

    [Header("Ruido neural")]
    [Tooltip("Estilo del ruido: 0 = scroll direccional, 1 = estatica de TV/grano " +
             "(parpadeo random, recomendado), 2 = particulas discretas. Cambiar en vivo.")]
    public NeuralNoiseMode noiseMode = NeuralNoiseMode.StaticGrain;
    [Tooltip("Cuantas veces por segundo el ruido se re-randomiza (modos estatica y particulas). " +
             "Mas alto = parpadeo mas rapido.")]
    [Range(1f, 60f)] public float noiseFlickerRate = 12f;
    [Range(0f, 0.3f)] public float noiseIntensity = 0.035f;
    [Tooltip("Grano fino.")]
    public float noise1Scale = 900f;
    [Tooltip("Grano mas grueso, desfasado para que no se repita el patron.")]
    public float noise2Scale = 240f;

    [Header("Vision periferica (bastones)")]
    [Tooltip("Crecimiento del efecto hacia los bordes de pantalla.")]
    [Range(0f, 1f)] public float peripheralPower = 0.85f;
    [Tooltip("Peso del efecto justo en el centro (fovea). 0 = bastones/mono/ruido llegan a todo el campo: los conos solo ven claro DENTRO del cono de la linterna, en la oscuridad todo es baston.")]
    [Range(0f, 1f)] public float centerWeight = 0f;

    [Header("Blur por convergencia de bastones")]
    [Range(0f, 6f)] public float blurRadius = 1.5f;

    [Header("Luz ambiente tenue (jugable)")]
    [Tooltip("Si esta ON, el Update re-aplica una luz ambiente tenue cada frame para que la " +
             "vision escotopica siempre tenga algo que amplificar (entorno distinguible). " +
             "OFF = no toca RenderSettings (respeta lo que ya este en la escena).")]
    public bool applyAmbientLight = false;
    [Tooltip("Color de esa luz ambiente tenue. Lineal. ~0.15 deja el entorno 'se nota un poco'.")]
    public Color ambientLightColor = new Color(0.15f, 0.15f, 0.18f, 1f);

    private Material _material;

    /// <summary>Blend de las luces generales: 1 = normal (linterna off), 0 = negro (linterna on).</summary>
    private float _generalLightBlend = 1f;

    /// <summary>Adaptacion 0-1 que se esta empujando al shader (para depurar).</summary>
    public float CurrentDarkness { get; private set; }

    private void Awake()
    {
        if (adaptation == null)
            adaptation = GetComponent<DarkAdaptation>();
        if (adaptation == null)
            Debug.LogError("[ScotopicVision] Falta un DarkAdaptation en el mismo player.", this);

        // La linterna suele vivir en la camara hija del player.
        if (flashlight == null)
            flashlight = GetComponentInChildren<FlashlightController>();

        // La direccional es la unica de la escena (sol/luz de relleno).
        if (directionalLight == null)
        {
            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
                if (l.type == LightType.Directional) { directionalLight = l; break; }
        }
    }

    private void Update()
    {
        // El material lo crea el Render Feature; esperamos a que exista.
        if (_material == null)
        {
            _material = ScotopicVisionFeature.ActiveMaterial;
            if (_material == null) return;
        }

        if (adaptation == null) return;

        // Estado de la linterna (gobierna las luces generales aunque el filtro
        // no se apague con ella).
        bool flashlightLit = flashlight != null && flashlight.IsOn;

        // Fondo negro con la linterna: al prenderla se funden las luces generales
        // (ambiental + directional) a negro para que SOLO ilumine la linterna ->
        // fuera del cono queda NEGRO absoluto. Al apagarla se restauran solas.
        if (darkenAmbientWithFlashlight)
        {
            float targetBlend = flashlightLit ? 0f : 1f;
            _generalLightBlend = Mathf.MoveTowards(_generalLightBlend, targetBlend,
                Time.deltaTime / Mathf.Max(0.01f, flashlightFadeTime));

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(Color.black, darkAmbientColor, _generalLightBlend);
            if (directionalLight != null)
                directionalLight.intensity = darkDirectionalIntensity * _generalLightBlend;
        }
        else if (applyAmbientLight)
        {
            // Modo simple: luz ambiente tenue fija (sin el crossfade de linterna).
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientLightColor;
        }

        // El corazon: la adaptacion medida por DarkAdaptation gobierna cuanto
        // responde la retina escotopica.
        CurrentDarkness = adaptation.Adaptation01;

        // Fuerza efectiva: si el filtro esta apagado, se fuerza a 0 (escena cruda).
        // Con la linterna encendida tambien se anula (vision fotopica de conos),
        // si el jugador activo esa opcion.
        bool flashlightOn = turnOffWithFlashlight && flashlightLit;
        float effectiveStrength = (filterEnabled && !flashlightOn) ? filterStrength : 0f;

        _material.SetFloat("_DarknessAmount", CurrentDarkness);
        _material.SetFloat("_LocalLitSensitivity", localLitSensitivity);
        _material.SetColor("_EigengrauColor", eigengrauColor);
        _material.SetFloat("_EigengrauAmount", eigengrauAmount);
        _material.SetFloat("_ScotopicGain", scotopicGain);
        _material.SetFloat("_MonoAmount", monoAmount);
        _material.SetFloat("_NoiseIntensity", noiseIntensity);
        _material.SetFloat("_Noise1Scale", noise1Scale);
        _material.SetFloat("_Noise2Scale", noise2Scale);
        _material.SetFloat("_NoiseMode", (float)noiseMode);
        _material.SetFloat("_NoiseFlickerRate", noiseFlickerRate);
        _material.SetFloat("_PeripheralPower", peripheralPower);
        _material.SetFloat("_CenterWeight", centerWeight);
        _material.SetFloat("_BlurRadius", blurRadius);

        // Controles maestros.
        _material.SetFloat("_FilterStrength", effectiveStrength);
        _material.SetFloat("_FilterGamma", filterGamma);
        _material.SetFloat("_MinDarkness", minDarknessFloor);
    }
}
