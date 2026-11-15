using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    // HUD de stamina (StaminaHUD_Canvas).
    //
    // La barra (arte "Stamina barra.png") NO se achica: se desliza de derecha
    // a izquierda dentro del contenedor (arte "Stamina contenedor.png").
    // El triangulito del final de la barra es el que marca el nivel: cuando
    // esta al 100% queda tapado por la pared derecha del contenedor y no se
    // ve; cuando esta a la mitad queda en el centro y la barra sobresale por
    // la izquierda.
    //
    // El recorte lo hace el shader PrisaControlada/StaminaBarClip: fuera de la
    // ventana interior del contenedor no se dibuja nada.
    //
    // OJO con los espacios: RectTransform.rect vive en el espacio local PROPIO
    // del rect (relativo a su pivot), NO en el del padre. Por eso todas las
    // cuentas se hacen llevando la ventana al espacio de la barra con
    // TransformPoint / InverseTransformPoint, que andan sin importar donde
    // este el pivot de cada uno ni como este escalada la jerarquia.
    //
    // Este script es solo logica: NO crea UI. La jerarquia (contenedor,
    // ventana y barra) la arma la tool de Editor
    // "PrisaControlada/Configurar StaminaHUD". En el Scene view solo se
    // refresca el recorte del material (para ver el corte mientras retocas);
    // las posiciones las manejas vos.
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class StaminaBarHUD : MonoBehaviour
    {
        [Header("UI (lo crea la tool de Editor)")]
        [Tooltip("Rect interior del contenedor: define la ventana por la que se ve la barra.")]
        [SerializeField] private RectTransform ventana;

        [Tooltip("La barra que se desliza (arte 'Stamina barra.png').")]
        [SerializeField] private RectTransform barra;

        [Tooltip("Imagen de la barra (necesaria para el material de recorte).")]
        [SerializeField] private RawImage barraImage;

        [Tooltip("PlayerController que aporta la stamina. Si esta vacio, se busca en los padres.")]
        [SerializeField] private PlayerController player;

        [Header("Arte (pixeles del PNG original)")]
        [Tooltip("Largo de la parte recta de la barra (sin el triangulito).")]
        [SerializeField] private float largoLinea = 625f;

        [Tooltip("Largo total del arte de la barra (linea + triangulito).")]
        [SerializeField] private float largoArte = 637f;

        [Header("Colores")]
        [Tooltip("Por debajo de este valor la barra se pone roja.")]
        [SerializeField, Range(0f, 1f)] private float umbralRojo = 0.25f;

        [Tooltip("Por debajo de este valor la barra se pone naranja.")]
        [SerializeField, Range(0f, 1f)] private float umbralNaranja = 0.5f;

        [SerializeField] private Color colorNormal = Color.white;
        [SerializeField] private Color colorNaranja = new Color(1f, 0.55f, 0.1f);
        [SerializeField] private Color colorRojo = new Color(1f, 0.16f, 0.12f);

        [Header("Fade (al maximo y sin correr)")]
        [Tooltip("Imagen del contenedor, para el fade out. Si esta vacia, se busca 'Contenedor' sola.")]
        [SerializeField] private RawImage contenedorImage;

        [Tooltip("Segundos al 100% de stamina sin correr antes de que la barra se oculte.")]
        [SerializeField, Min(0f)] private float retrasoOcultar = 1.5f;

        [Tooltip("Segundos que tarda el fade out y el fade in (0 = instantaneo).")]
        [SerializeField, Min(0f)] private float duracionFade = 0.35f;

        [Tooltip("Marcado: el fade oculta todo (contenedor + barra). Desmarcado: solo la barra de relleno.")]
        [SerializeField] private bool fadeCompleto = true;

        [Header("Parpadeo (stamina agotada)")]
        [Tooltip("Ciclos por segundo del parpadeo rojo <-> negro mientras se recupera de 0 al 100%.")]
        [SerializeField, Min(0f)] private float frecuenciaParpadeo = 2f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RectSizeId = Shader.PropertyToID("_RectSize");
        private static readonly int ClipMinId = Shader.PropertyToID("_ClipMin");
        private static readonly int ClipMaxId = Shader.PropertyToID("_ClipMax");

        // Instancia de material solo en Play (UGUI no admite MaterialPropertyBlock).
        private Material _material;
        private float _anchoVentana;

        // Fade: opacidad actual y tiempo acumulado al 100% sin correr.
        private float _alpha = 1f;
        private float _tiempoSinCorrer;

        // En Play es la instancia; en el Scene view es el material del asset
        // (el que se ve mientras editas).
        private Material MaterialEfectivo
        {
            get
            {
                if (_material != null) return _material;
                return barraImage != null ? barraImage.material : null;
            }
        }

        private void Awake()
        {
            // En el Scene view no se toca nada: ni se clona el material ni se
            // mueve la barra (las posiciones las retoca el usuario a mano).
            if (!Application.isPlaying) return;

            if (player == null)
                player = GetComponentInParent<PlayerController>();

            if (ventana == null || barra == null || barraImage == null)
            {
                Debug.LogError("[StaminaBarHUD] Faltan referencias de UI (ventana/barra/barraImage). " +
                               "Ejecuta PrisaControlada/Configurar StaminaHUD. Componente deshabilitado.", this);
                enabled = false;
                return;
            }

            if (player == null)
            {
                Debug.LogError("[StaminaBarHUD] No se encontro PlayerController en los padres. " +
                               "Componente deshabilitado.", this);
                enabled = false;
                return;
            }

            // UGUI no admite MaterialPropertyBlock: cada barra necesita su propia
            // instancia de material para poder cambiar el recorte y el color.
            Material source = barraImage.material;
            if (source == null || !source.HasProperty(ClipMinId))
            {
                Debug.LogError("[StaminaBarHUD] La barra no tiene el material StaminaBarClip " +
                               "(falta la propiedad _ClipMin). Ejecuta PrisaControlada/Configurar StaminaHUD. " +
                               "Componente deshabilitado.", barraImage);
                enabled = false;
                return;
            }

            _material = new Material(source);
            _material.hideFlags = HideFlags.HideAndDontSave;
            barraImage.material = _material;

            if (contenedorImage == null)
            {
                Transform cont = transform.Find("Contenedor");
                if (cont != null)
                    contenedorImage = cont.GetComponent<RawImage>();
            }

            AjustarAnchoBarra();
            Aplicar(EstaminaNormalizada(), 0f);
        }

        private void OnEnable()
        {
            // Vista previa del recorte en el Scene view (al cargar la escena
            // o al recompilar).
            if (!Application.isPlaying)
                ActualizarRecorte();
        }

        private void OnDestroy()
        {
            if (_material == null) return;

            if (Application.isPlaying)
                Destroy(_material);
            else
                DestroyImmediate(_material);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                // Scene view: solo se refresca el recorte, para que el corte
                // se vea mientras movés la barra o el contenedor a mano.
                // Nunca se mueven transforms (para no ensuciar la escena ni
                // pelear con tus ajustes).
                ActualizarRecorte();
                return;
            }

            if (_material == null || player == null) return;

            // Si el usuario cambia el ancho de la ventana a mano, se reajusta.
            if (!Mathf.Approximately(_anchoVentana, ventana.rect.width))
                AjustarAnchoBarra();

            Aplicar(EstaminaNormalizada(), Time.deltaTime);
        }

        // Lo usa la tool de Editor al terminar de cablear: deja la barra en el
        // estado inicial (100%, con el triangulito detras de la pared derecha)
        // y refresca el recorte del material (el asset: el que se ve en el
        // Scene view).
        public void ColocarEnCienPorCiento()
        {
            if (barra == null || ventana == null) return;

            MoverBarra(1f);
            ActualizarRecorte();
        }

        private float EstaminaNormalizada()
        {
            if (player == null) return 1f;

            float max = player.MaxStamina;
            return max > 0f ? Mathf.Clamp01(player.CurrentStamina / max) : 1f;
        }

        private void Aplicar(float t, float dt)
        {
            MoverBarra(t);
            ActualizarRecorte();
            ActualizarColor(t);
            ActualizarFade(t, dt);
        }

        // La barra se estira en horizontal para que su parte recta llene
        // exactamente la ventana del contenedor: asi, al 100% el triangulito
        // queda fuera (tapado por la pared) y no se ve.
        private void AjustarAnchoBarra()
        {
            _anchoVentana = ventana.rect.width;
            if (_anchoVentana <= 0f || largoLinea <= 0f) return;

            float escala = _anchoVentana / largoLinea;
            Vector2 size = barra.sizeDelta;
            size.x = largoArte * escala;
            barra.sizeDelta = size;
        }

        // Desplaza la barra de derecha a izquierda: el triangulito (que esta al
        // final de la parte recta) marca el nivel dentro de la ventana.
        //   t = 1 -> el borde izquierdo de la barra queda en el borde izquierdo
        //            de la ventana: la linea llena la ventana y el triangulo
        //            queda DETRAS de la pared derecha (no se ve).
        //   t = 0 -> una ventana mas a la izquierda: no se ve nada.
        //   t = .5 -> la barra sobresale por la izquierda y el triangulito
        //            queda en el medio de la ventana.
        private void MoverBarra(float t)
        {
            if (barra == null || ventana == null) return;

            t = Mathf.Clamp01(t);
            Rect v = ventana.rect;

            // Borde izquierdo deseado de la barra, medido en el espacio de la ventana.
            float xIzqObjetivo = v.xMin + v.width * (t - 1f);

            // Donde tiene que quedar el pivot de la barra para que su borde
            // izquierdo (rect.xMin, en pixeles locales suyos) caiga ahi.
            // La posicion se FIJA cada frame (no se acumula un delta).
            Vector3 pivot = ventana.InverseTransformPoint(barra.position);
            float pivotX = xIzqObjetivo - barra.rect.xMin * RelacionEscalaX();

            barra.position = ventana.TransformPoint(new Vector3(pivotX, pivot.y, pivot.z));
        }

        // Le pasa al shader la ventana de recorte expresada en los pixeles
        // locales de la barra (el sistema uv * _RectSize, con origen en la
        // esquina inferior izquierda del rect, o sea rect.min).
        private void ActualizarRecorte()
        {
            if (barra == null || ventana == null || barraImage == null) return;

            Material mat = MaterialEfectivo;
            if (mat == null || !mat.HasProperty(ClipMinId)) return;

            Rect v = ventana.rect;
            Rect r = barra.rect;

            // Ventana llevada al espacio local de la barra (ahi vive su malla)
            // y despuesta al origen de sus pixeles locales.
            Vector3 esqMin = barra.InverseTransformPoint(
                ventana.TransformPoint(new Vector3(v.xMin, v.yMin, 0f)));
            Vector3 esqMax = barra.InverseTransformPoint(
                ventana.TransformPoint(new Vector3(v.xMax, v.yMax, 0f)));

            Vector4 clipMin = new Vector4(esqMin.x - r.xMin, esqMin.y - r.yMin, 0f, 0f);
            Vector4 clipMax = new Vector4(esqMax.x - r.xMin, esqMax.y - r.yMin, 0f, 0f);
            Vector4 rectSize = new Vector4(r.width, r.height, 0f, 0f);

            // Solo se escribe si cambio algo: asi no se marca sucio el material
            // del asset en cada repintado del editor.
            if (mat.GetVector(ClipMinId) != clipMin) mat.SetVector(ClipMinId, clipMin);
            if (mat.GetVector(ClipMaxId) != clipMax) mat.SetVector(ClipMaxId, clipMax);
            if (mat.GetVector(RectSizeId) != rectSize) mat.SetVector(RectSizeId, rectSize);
        }

        private void ActualizarColor(float t)
        {
            Material mat = MaterialEfectivo;
            if (mat == null || !mat.HasProperty(ColorId)) return;

            Color color;

            if (player != null && player.IsStaminaExhausted)
            {
                // Quedo en 0 y se esta llenando: parpadeo suave rojo <-> negro
                // hasta llegar al 100% (ahi el controller lo saca de agotada).
                float onda = Mathf.Sin(Time.time * frecuenciaParpadeo * Mathf.PI * 2f) * 0.5f + 0.5f;
                color = Color.Lerp(Color.black, colorRojo, onda);
            }
            else if (t <= umbralRojo)
                color = colorRojo;
            else if (t <= umbralNaranja)
                color = colorNaranja;
            else
                color = colorNormal;

            mat.SetColor(ColorId, color);
        }

        // Al 100% y sin correr durante un ratito, la barra se va con fade out;
        // en cuanto el jugador vuelve a correr (o la stamina baja) reaparece
        // con fade in.
        private void ActualizarFade(float t, float dt)
        {
            bool corriendo = player != null && player.IsSprinting;
            bool ocultable = t >= 1f && !corriendo;

            if (ocultable)
            {
                _tiempoSinCorrer += dt;
                if (_tiempoSinCorrer >= retrasoOcultar)
                    _alpha = Mathf.MoveTowards(_alpha, 0f, duracionFade > 0.0001f ? dt / duracionFade : 1f);
            }
            else
            {
                _tiempoSinCorrer = 0f;
                if (_alpha < 1f)
                    _alpha = Mathf.MoveTowards(_alpha, 1f, duracionFade > 0.0001f ? dt / duracionFade : 1f);
            }

            AplicarAlpha();
        }

        // La opacidad va por el color de los RawImages (funciona igual con el
        // material por defecto del contenedor y con el shader de la barra) y
        // solo se escribe cuando el alpha cambia, asi no se pisan a mano los
        // tintes RGB que le hayas puesto.
        private void AplicarAlpha()
        {
            float alphaContenedor = fadeCompleto ? _alpha : 1f;

            if (contenedorImage != null)
            {
                Color c = contenedorImage.color;
                if (!Mathf.Approximately(c.a, alphaContenedor))
                {
                    c.a = alphaContenedor;
                    contenedorImage.color = c;
                }
            }

            if (barraImage != null)
            {
                Color c = barraImage.color;
                if (!Mathf.Approximately(c.a, _alpha))
                {
                    c.a = _alpha;
                    barraImage.color = c;
                }
            }
        }

        // Cuantos pixeles de la barra equivalen a un pixel de la ventana
        // (1 salvo que alguien reescale la jerarquia a mano).
        private float RelacionEscalaX()
        {
            float escalaVentana = ventana.lossyScale.x;
            return Mathf.Abs(escalaVentana) > 1e-6f ? barra.lossyScale.x / escalaVentana : 1f;
        }
    }
}
