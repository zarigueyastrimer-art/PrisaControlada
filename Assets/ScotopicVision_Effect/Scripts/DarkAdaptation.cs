using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Adaptación a la oscuridad (visión escotópica).
///
/// Simula cómo el ojo humano pasa de "no ver nada" a adaptarse cuando entra
/// en un ambiente oscuro: al principio la pantalla queda casi negra y, con el
/// tiempo, el entorno emerge. Para eso mide la luz que rodea al jugador y
/// anima los post-procesados de URP (exposición, saturación, grano, viñeta
/// y balance de color).
///
/// Pensado para INTERIORES: usa raycasts de oclusión para que una luz que está
/// detrás de una pared no cuente como iluminación. Sin eso, una habitación
/// oscura vecina con la luz prendida impediría que el jugador se adaptara.
/// </summary>
public class DarkAdaptation : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Cámara del jugador. Si está vacío se busca automáticamente.")]
    public Camera targetCamera;

    [Tooltip("Volume global donde viven los post-procesados. Si está vacío se busca.")]
    public Volume volume;

    [Header("Detección de luz")]
    [Tooltip("Sensibilidad al convertir la luz de la escena en un nivel 0-1. Más bajo = se oscurece antes.")]
    public float lightSensitivity = 0.5f;

    [Tooltip("Nivel de luz por debajo del cual se considera 'a oscuras'.")]
    [Range(0f, 1f)] public float darknessThreshold = 0.12f;

    [Tooltip("Nivel de luz por encima del cual no hay adaptación (plena luz).")]
    [Range(0f, 1f)] public float fullLightThreshold = 0.55f;

    [Tooltip("Segundos entre refrescos de la lista de luces de la escena.")]
    public float lightCacheInterval = 0.5f;

    [Header("Oclusión (clave en interiores)")]
    [Tooltip("Si una luz está tapada por una pared, no cuenta. Desactivar solo para exteriores.")]
    public bool useOcclusion = true;

    [Tooltip("Segundos entre raycasts de oclusión. Subir si hay muchas luces.")]
    public float occlusionCheckInterval = 0.1f;

    [Tooltip("Qué geometría tapa la luz. Dejar en Everything salvo el jugador.")]
    public LayerMask occlusionMask = ~0;

    [Tooltip("Distancia del rayo para luces direccionales (sol/luna entrando por una ventana).")]
    public float directionalCheckDistance = 60f;

    [Tooltip("Separación del rayo desde los ojos hacia arriba, para evitar arrancar dentro del suelo.")]
    public float rayOriginLift = 0.15f;

    [Tooltip("Luces a menos de esta distancia del jugador se tratan como 'en su mano' " +
             "(la linterna) y cuentan completa, sin atenuación ni chequeo de cono.")]
    public float carriedLightDistance = 1.0f;

    [Header("Depuración")]
    [Tooltip("Loguea por qué cada luz se considera visible u ocluida.")]
    public bool debugOcclusion = false;

    [Header("Ritmo de la adaptación")]
    [Tooltip("Segundos en pasar de 'no veo nada' a adaptación completa a la oscuridad.")]
    public float darkAdaptTime = 7f;

    [Tooltip("Segundos en volver a la vista normal al recibir luz fuerte (los ojos se recuperan rápido).")]
    public float lightAdaptTime = 0.9f;

    [Header("Curva de la adaptación")]
    [Tooltip("x = adaptación normalizada, y = intensidad visual 0-1. " +
             "El tramo plano inicial es lo que da la sensación de 'no veo nada'.")]
    public AnimationCurve adaptCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.30f, 0.04f, 0.15f, 0.40f),
        new Keyframe(1f, 1f, 1.30f, 0f));

    [Header("Efecto visual (a adaptación completa)")]
    [Tooltip("Exposiciones (EV) que se suman al revelar la oscuridad.")]
    public float maxExposureBoost = 3.2f;

    [Tooltip("Cuánta saturación se pierde. En visión escotópica casi no hay color.")]
    public float maxSaturationDrop = -35f;

    [Tooltip("Aumento de contraste para recuperar forma en la penumbra.")]
    public float maxContrastBoost = 12f;

    [Tooltip("Grano de película. Los bastones son ruidosos: cuanto más oscuro, más ruido.")]
    [Range(0f, 1f)] public float maxGrain = 0.55f;

    [Tooltip("Viñeta. En la oscuridad se reduce la visión periférica (túnel).")]
    [Range(0f, 1f)] public float maxVignette = 0.42f;

    [Tooltip("Desplazamiento de temperatura. Negativo = frío/azulado (efecto Purkinje).")]
    [Range(-100f, 100f)] public float maxTemperatureShift = -18f;

    // ------------------------------------------------------------------ estado

    /// <summary>Adaptación actual, 0 = vista normal, 1 = plena adaptación oscura.</summary>
    public float Adaptation01 { get; private set; }

    /// <summary>Último nivel de luz medido alrededor del jugador, 0-1.</summary>
    public float SceneLight { get; private set; }

    /// <summary>Cuántas luces visibles (sin tapar) detectó la última medición.</summary>
    public int VisibleLightCount { get; private set; }

    float _refreshTimer;
    float _occlusionTimer;
    Light[] _lights = System.Array.Empty<Light>();
    bool[] _occluded = System.Array.Empty<bool>();

    // referencias de post-procesado
    ColorAdjustments _colorAdjust;
    FilmGrain _grain;
    Vignette _vignette;
    WhiteBalance _whiteBalance;

    // valores base capturados del profile, para no pisar el trabajo del artista
    float _baseExposure, _baseSaturation, _baseContrast;
    float _baseGrainIntensity, _baseVignetteIntensity, _baseTemperature;

    // ------------------------------------------------------------------ ciclo

    void Awake()
    {
        ResolveCamera();
        ResolveVolume();
        CacheBaselines();
        RefreshLights();
    }

    void Update()
    {
        // En modo edición no hacemos nada: queremos poder tunningear sin que
        // el script pelee con los valores del Inspector.
        if (!Application.isPlaying)
            return;

        if (_colorAdjust == null)
            return;

        if (targetCamera == null)
        {
            ResolveCamera();
            if (targetCamera == null)
                return;
        }

        Vector3 eye = targetCamera.transform.position + Vector3.up * rayOriginLift;

        // Refrescar la lista de luces cada tanto. Las referencias se guardan,
        // pero la intensidad se lee en vivo para que un fogonazo o un parpadeo
        // de antorcha se sientan de inmediato.
        _refreshTimer += Time.deltaTime;
        bool refreshList = _refreshTimer >= lightCacheInterval || _lights.Length == 0;
        if (refreshList)
        {
            _refreshTimer = 0f;
            RefreshLights();
        }

        // Los raycasts son más caros, así que van más despacio que la lista.
        _occlusionTimer += Time.deltaTime;
        if (refreshList || _occlusionTimer >= occlusionCheckInterval)
        {
            _occlusionTimer = 0f;
            UpdateOcclusion(eye);
        }

        SceneLight = MeasureSceneLight(eye);

        // Objetivo continuo: con mucha luz = 0 (sin adaptar), a oscuras = 1.
        float target = Mathf.InverseLerp(fullLightThreshold, darknessThreshold, SceneLight);

        // Ir hacia la oscuridad es lento; recibir luz fuerte es rápido.
        bool gettingDarker = target > Adaptation01;
        float seconds = gettingDarker ? darkAdaptTime : lightAdaptTime;
        float rate = 1f / Mathf.Max(0.01f, seconds);

        Adaptation01 = Mathf.MoveTowards(Adaptation01, target, rate * Time.deltaTime);

        ApplyVisual(adaptCurve.Evaluate(Adaptation01));
    }

    void OnDisable()
    {
        if (Application.isPlaying)
            ApplyVisual(0f);   // volver a los valores base
    }

    // ------------------------------------------------------------------ luz

    /// <summary>
    /// Estima cuánta luz hay alrededor del jugador (0-1).
    /// Suma el ambiental + cada luz cercana que NO esté tapada por una pared.
    /// </summary>
    float MeasureSceneLight(Vector3 eyePosition)
    {
        // En espacio de color Linear, ambientLight viene en sRGB pero lo que
        // Unity renderiza es el valor LINEAL (~9x menor). Si medimos el sRGB
        // crudo sobreestimamos la luz y la adaptación se frena de más. Medimos
        // el lineal: es lo que realmente llega al ojo. En interiores es casi
        // cero, así que la adaptación puede ir a full y la exposición revela.
        float sum = RenderSettings.ambientLight.linear.maxColorComponent;

        Vector3 playerPosition = eyePosition - Vector3.up * rayOriginLift;
        VisibleLightCount = 0;

        for (int i = 0; i < _lights.Length; i++)
        {
            Light l = _lights[i];
            if (l == null || !l.enabled || !l.gameObject.activeInHierarchy)
                continue;

            // Si el rayo de oclusión ya determinó que está tapada, ni la miramos.
            if (useOcclusion && i < _occluded.Length && _occluded[i])
                continue;

            // Las direccionales iluminan todo el escenario por igual.
            if (l.type == LightType.Directional)
            {
                sum += l.intensity * 0.6f * l.color.linear.maxColorComponent;
                VisibleLightCount++;
                continue;
            }

            float range = l.range;
            if (range <= 0f)
                continue;

            Vector3 toTarget = playerPosition - l.transform.position;
            float distance = toTarget.magnitude;
            if (distance >= range)
                continue;

            // La linterna va en la mano del jugador: está a centímetros de los
            // ojos. Ahí el cono y la atenuación no aplican (y el cálculo del
            // ángulo daría basura con un vector casi nulo), así que cuenta
            // completa. Esto además hace que prenderla resetee la adaptación.
            if (distance <= carriedLightDistance)
            {
                sum += l.intensity * l.color.linear.maxColorComponent;
                VisibleLightCount++;
                continue;
            }

            // Si el jugador está fuera del cono de una spotlight, no lo alumbra.
            // Esto hace que una linterna en mano impida la adaptación (correcto).
            if (l.type == LightType.Spot)
            {
                float halfAngle = l.spotAngle * 0.5f;
                if (Vector3.Angle(l.transform.forward, toTarget) > halfAngle)
                    continue;
            }

            float attenuation = 1f - (distance / range);
            attenuation *= attenuation;

            sum += l.intensity * attenuation * l.color.linear.maxColorComponent;
            VisibleLightCount++;
        }

        return Mathf.Clamp01(sum * lightSensitivity);
    }

    /// <summary>
    /// Marca qué luces están tapadas por geometría. Esto es lo que hace que el
    /// efecto funcione en interiores: una luz al otro lado de una pared no debe
    /// iluminar al jugador, ni contar para su adaptación.
    /// </summary>
    void UpdateOcclusion(Vector3 eyePosition)
    {
        if (_occluded.Length != _lights.Length)
            _occluded = new bool[_lights.Length];

        if (!useOcclusion)
            return;

        // Al entrar en Play (o al aparecer geometría) los colliders tardan unos
        // frames en registrarse en la escena de física, y mientras tanto los
        // raycasts no chocan: todas las luces parecen visibles y la adaptación
        // se frena. Forzar el sincronismo elimina ese transitorio.
        Physics.SyncTransforms();

        for (int i = 0; i < _lights.Length; i++)
        {
            Light l = _lights[i];

            if (l == null || !l.enabled || !l.gameObject.activeInHierarchy)
            {
                _occluded[i] = true;
                continue;
            }

            // Direccional: el "origen" es infinito, así que tiramos el rayo en
            // la dirección de la que viene la luz a ver si algo lo bloquea.
            if (l.type == LightType.Directional)
            {
                Vector3 towardLight = -l.transform.forward;
                _occluded[i] = Physics.Raycast(
                    eyePosition, towardLight,
                    directionalCheckDistance, occlusionMask,
                    QueryTriggerInteraction.Ignore);
                continue;
            }

            Vector3 toLight = l.transform.position - eyePosition;
            float distance = toLight.magnitude;

            // Muy cerca: no hay dónde meter una pared entre el jugador y la luz.
            if (distance < 0.25f)
            {
                _occluded[i] = false;
                continue;
            }

            // Un tapado parcial igual deja pasar algo de luz, así que solo
            // marcamos como tapada si el rayo choca antes de llegar a la fuente.
            bool hit = Physics.Raycast(
                eyePosition, toLight / distance,
                distance - 0.1f, occlusionMask,
                QueryTriggerInteraction.Ignore);
            _occluded[i] = hit;

            if (debugOcclusion)
            {
                RaycastHit h;
                bool anyHit = Physics.Raycast(eyePosition, toLight / distance, out h, 200f, occlusionMask, QueryTriggerInteraction.Ignore);
                Debug.Log($"[DA-occ] {l.name} dist={distance:F1} rayDist={distance - 0.1f:F1} occluded={hit} " +
                          $"| rayoLargo choca={anyHit} a {h.distance:F1}m con {h.collider?.name ?? "nada"} " +
                          $"| eye={eyePosition} dir={(toLight / distance)}", this);
            }
        }
    }

    void RefreshLights()
    {
        _lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        if (_occluded.Length != _lights.Length)
            _occluded = new bool[_lights.Length];
    }

    // ------------------------------------------------------- referencias

    void ResolveCamera()
    {
        if (targetCamera != null)
            return;

        targetCamera = Camera.main;

        if (targetCamera == null)
        {
            // Ojo: la cámara del jugador a veces está sin tag MainCamera,
            // así que no confiamos solo en Camera.main.
            var cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length > 0)
                targetCamera = cameras[0];
        }

        if (targetCamera == null)
            Debug.LogWarning("[DarkAdaptation] No se encontró ninguna cámara.", this);
    }

    void ResolveVolume()
    {
        if (volume != null)
        {
            BindProfile(volume);
            return;
        }

        var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        Volume fallback = null;

        foreach (var v in volumes)
        {
            if (v == null || !v.isGlobal || v.profile == null)
                continue;

            // Preferimos el volume que realmente tenga ColorAdjustments activo.
            if (v.profile.TryGet<ColorAdjustments>(out var ca) && ca != null && ca.active)
            {
                fallback = v;
                break;
            }

            if (fallback == null)
                fallback = v;
        }

        if (fallback == null && volumes.Length > 0)
            fallback = volumes[0];

        volume = fallback;

        if (volume == null)
        {
            Debug.LogWarning("[DarkAdaptation] No se encontró ningún Volume en la escena.", this);
            return;
        }

        BindProfile(volume);
    }

    void BindProfile(Volume v)
    {
        // v.profile devuelve una copia en memoria: así no ensuciamos el asset.
        VolumeProfile profile = v.profile;
        if (profile == null)
            return;

        if (!profile.TryGet(out _colorAdjust) || _colorAdjust == null)
            _colorAdjust = profile.Add<ColorAdjustments>(true);

        if (!profile.TryGet(out _grain))
            _grain = profile.Add<FilmGrain>(true);

        if (!profile.TryGet(out _vignette))
            _vignette = profile.Add<Vignette>(true);

        if (!profile.TryGet(out _whiteBalance))
            _whiteBalance = profile.Add<WhiteBalance>(true);

        if (_grain != null)
        {
            // Grano procedural, sin depender de una textura asignada a mano.
            _grain.type.value = FilmGrainLookup.Medium1;
            _grain.response.value = 0.8f;
        }
    }

    void CacheBaselines()
    {
        if (_colorAdjust != null)
        {
            _baseExposure = _colorAdjust.postExposure.value;
            _baseSaturation = _colorAdjust.saturation.value;
            _baseContrast = _colorAdjust.contrast.value;
        }

        if (_grain != null)
            _baseGrainIntensity = _grain.intensity.value;

        if (_vignette != null)
            _baseVignetteIntensity = _vignette.intensity.value;

        if (_whiteBalance != null)
            _baseTemperature = _whiteBalance.temperature.value;
    }

    // ------------------------------------------------------------- visual

    /// <summary>v = intensidad visual 0-1 (salida de la curva).</summary>
    void ApplyVisual(float v)
    {
        if (_colorAdjust != null)
        {
            _colorAdjust.postExposure.value = _baseExposure + maxExposureBoost * v;
            _colorAdjust.saturation.value = _baseSaturation + maxSaturationDrop * v;
            _colorAdjust.contrast.value = _baseContrast + maxContrastBoost * v;
        }

        if (_grain != null)
            _grain.intensity.value = Mathf.Lerp(_baseGrainIntensity, maxGrain, v);

        if (_vignette != null)
            _vignette.intensity.value = Mathf.Lerp(_baseVignetteIntensity, maxVignette, v);

        if (_whiteBalance != null)
            _whiteBalance.temperature.value = _baseTemperature + maxTemperatureShift * v;
    }

    // -------------------------------------------------------------- ayuda

    /// <summary>Fuerza un estado de adaptación. Útil para probar desde un botón.</summary>
    public void SetAdaptationImmediate(float value)
    {
        Adaptation01 = Mathf.Clamp01(value);
        ApplyVisual(adaptCurve.Evaluate(Adaptation01));
    }

    [ContextMenu("Forzar re-medición de luces")]
    void ForceRefreshLights() => RefreshLights();
}
