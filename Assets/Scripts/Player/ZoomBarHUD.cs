using UnityEngine;

namespace PrisaControlada.Player
{
    //TP2 - Ian Rosenberg
    // Barra de zoom (lado derecho de la pantalla). La barra es estatica: lo
    // unico que se mueve es el marcador, que sube de abajo (sin zoom) hacia
    // arriba (zoom completo).
    //
    // El zoom es SOLO el click derecho sin arma (PlayerCamera.UpdateUnarmedZoom):
    // el script no interpreta el FOV, porque con un arma equipada el FOV lo
    // maneja el arma y eso no cuenta como zoom. PlayerCamera expone IsZooming
    // y ZoomProgress (0..1).
    //
    // La barra aparece con fade cuando se hace zoom y desaparece con fade si no
    // se usa la mecanica durante un rato (hideDelay), hasta la proxima vez que
    // se haga zoom.
    //
    // No crea UI: la jerarquia (ZoomHUD_Canvas > ZoomBar > Pista > Marcador)
    // la arma la tool de Editor "PrisaControlada/Configurar ZoomHUD".
    [DisallowMultipleComponent]
    public class ZoomBarHUD : MonoBehaviour
    {
        [Header("UI (lo crea la tool de Editor)")]
        [Tooltip("Contenedor de la barra: se activa/desactiva para mostrarla u ocultarla.")]
        [SerializeField] private RectTransform barra;

        [Tooltip("Pista vertical: define el recorrido del marcador (de abajo hacia arriba).")]
        [SerializeField] private RectTransform pista;

        [Tooltip("Marcador que se mueve dentro de la pista.")]
        [SerializeField] private RectTransform marcador;

        [Tooltip("CanvasGroup de la barra: se usa para el fade in/out.")]
        [SerializeField] private CanvasGroup grupo;

        [Header("Zoom")]
        [Tooltip("PlayerCamera: es quien sabe si hay zoom (click derecho sin arma). Si esta vacio, se busca en la escena.")]
        [SerializeField] private PlayerCamera playerCamera;

        [Header("Marcador")]
        [Tooltip("Hasta cuanto sube el marcador dentro de la pista. 1 = tope completo de la barra, 0.5 = hasta la mitad. Se puede ajustar a mano desde el Inspector.")]
        [SerializeField, Range(0f, 1f)] private float recorrido = 1f;

        [Header("Visibilidad")]
        [Tooltip("Segundos sin zoom antes de que la barra empiece a desaparecer.")]
        [SerializeField, Min(0.1f)] private float hideDelay = 1.5f;

        [Tooltip("Duracion del fade in/out.")]
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.25f;

        [Tooltip("Arrancar con la barra oculta hasta el primer zoom.")]
        [SerializeField] private bool startHidden = true;

        private float _lastZoomTime;
        private bool _wantVisible;
        private float _alpha;

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = FindFirstObjectByType<PlayerCamera>();

            if (barra == null || pista == null || marcador == null || grupo == null)
            {
                Debug.LogError("[ZoomBarHUD] Faltan referencias de UI (barra/pista/marcador/grupo). " +
                               "Ejecuta PrisaControlada/Configurar ZoomHUD. Componente deshabilitado.", this);
                enabled = false;
                return;
            }

            grupo.interactable = false;
            grupo.blocksRaycasts = false;

            _wantVisible = !startHidden;
            _alpha = _wantVisible ? 1f : 0f;
            grupo.alpha = _alpha;
            barra.gameObject.SetActive(_wantVisible);
            _lastZoomTime = Time.unscaledTime;
        }

        private void Update()
        {
            // Zoom real (solo click derecho sin arma): lo dice PlayerCamera.
            bool zooming = playerCamera != null && playerCamera.IsZooming;

            if (zooming)
            {
                // Hay zoom: mostrar y reiniciar el reloj de inactividad.
                _lastZoomTime = Time.unscaledTime;
                SetWantVisible(true);
            }
            else if (_wantVisible && Time.unscaledTime - _lastZoomTime >= hideDelay)
            {
                // Se paso el rato sin zoom: la barra empieza a desaparecer.
                SetWantVisible(false);
            }

            // El progreso sigue al FOV, asi que al soltar el boton el marcador
            // baja solo mientras la camara vuelve al FOV normal.
            float t = playerCamera != null ? playerCamera.ZoomProgress : 0f;

            // Marcador: de abajo (t=0) a arriba (t=1) dentro de la pista.
            // "recorrido" limita cuanto sube: 1 = tope completo de la barra.
            float travel = Mathf.Max(0f, pista.rect.height - marcador.rect.height)
                         * Mathf.Clamp01(recorrido);
            Vector2 pos = marcador.anchoredPosition;
            pos.y = t * travel;
            marcador.anchoredPosition = pos;

            // Fade in/out (sin escala de tiempo: funciona con el juego pausado).
            float targetAlpha = _wantVisible ? 1f : 0f;
            if (!Mathf.Approximately(_alpha, targetAlpha))
            {
                _alpha = Mathf.MoveTowards(_alpha, targetAlpha,
                    Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration));
                grupo.alpha = _alpha;
            }

            // Cuando termino el fade out, apagar el objeto para no renderizar.
            if (!_wantVisible && _alpha <= 0.001f && barra.gameObject.activeSelf)
                barra.gameObject.SetActive(false);
        }

        private void SetWantVisible(bool visible)
        {
            if (_wantVisible == visible) return;

            _wantVisible = visible;

            // Al mostrar, activar el objeto antes de que suba el alpha.
            if (visible && barra != null)
                barra.gameObject.SetActive(true);
        }
    }
}
