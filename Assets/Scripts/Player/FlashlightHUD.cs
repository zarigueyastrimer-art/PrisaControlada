using PrisaControlada.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    //TP2 - Ian Rosenberg
    // Icono de la linterna. Solo aparece mientras el jugador mantiene Tab y la
    // linterna es el objeto que tiene en la mano. El fade se hace con el
    // CanvasGroup para que la entrada y la salida sean suaves.
    //
    // La UI NO se construye por codigo: la jerarquia
    // (FlashlightHUD_Canvas/Icon) se crea en la escena con la tool de Editor
    // "PrisaControlada/Configurar FlashlightHUD" y de ahi se retoca a mano.
    [DisallowMultipleComponent]
    public class FlashlightHUD : MonoBehaviour
    {
        private const string CanvasName = "FlashlightHUD_Canvas";
        private const float SearchInterval = 0.25f;

        [Header("UI (asignada por la tool o a mano en la jerarquia)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RawImage icon;

        [Header("Referencias")]
        [Tooltip("HUD de arma (Tab). Coordina que solo se muestre el objeto que esta en la mano.")]
        [SerializeField] private AmmoOverviewHUD ammoHUD;
        [Tooltip("Linterna del jugador. Si se deja vacio, se busca al iniciar.")]
        [SerializeField] private FlashlightToggle flashlight;

        [Header("Tecla")]
        [SerializeField] private Key toggleKey = Key.Tab;

        [Header("Comportamiento")]
        [Tooltip("Velocidad de aparicion/desaparicion del icono (unidades de alfa por segundo).")]
        [SerializeField, Min(0f)] private float fadeSpeed = 14f;

        private PlayerHealth _health;
        private float _searchTimer;
        private FlashlightToggle[] _flashlights;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();

            if (!ResolveSceneReferences())
            {
                Debug.LogError(
                    "[FlashlightHUD] No se encontro la UI en la jerarquia del Player. " +
                    "Ejecuta el menu PrisaControlada/Configurar FlashlightHUD con la escena abierta. " +
                    "Componente deshabilitado.", this);
                enabled = false;
                return;
            }

            // El icono arranca oculto y solo aparece con Tab + linterna equipada.
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private void Update()
        {
            _searchTimer -= Time.unscaledDeltaTime;
            if (_searchTimer <= 0f)
            {
                _searchTimer = SearchInterval;
                RefreshFlashlights();
            }

            FlashlightToggle equipped = FindEquippedFlashlight();
            if (equipped != null)
                flashlight = equipped;

            bool tabHeld = ammoHUD != null
                ? ammoHUD.IsTabHeld
                : IsHeldFallback();

            // AmmoOverviewHUD decide que objeto esta en la mano. Asi la linterna
            // nunca aparece junto al icono de un arma.
            bool selectedFlashlight = ammoHUD == null || ammoHUD.ShouldShowFlashlight;
            bool weaponPanelHidden = ammoHUD == null || ammoHUD.PanelAlpha <= 0f;
            bool visible = equipped != null &&
                          tabHeld &&
                          selectedFlashlight &&
                          weaponPanelHidden &&
                          !Blocked();

            ApplyAlpha(visible ? 1f : 0f);
        }

        /// <summary>
        /// Alfa actual del icono de linterna (0..1).
        /// </summary>
        public float PanelAlpha => group != null ? group.alpha : 0f;

        private void ApplyAlpha(float target)
        {
            if (Mathf.Approximately(group.alpha, target)) return;

            group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
        }

        private bool IsHeldFallback()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;

            if (toggleKey == Key.Tab)
                return keyboard.tabKey.isPressed;

            return keyboard[toggleKey].isPressed;
        }

        // No tapar otros overlays con el icono (teclado de codigo, nota, muerte).
        private bool Blocked()
        {
            if (CodeUI.IsOpen || NoteViewer.IsOpen) return true;
            if (_health != null && _health.IsDead) return true;
            return false;
        }

        // La linterna puede no existir aun (o estar inactiva): se buscan todas,
        // incluyendo inactivas, y se prioriza la que realmente esta equipada.
        private void RefreshFlashlights()
        {
            _flashlights = FindObjectsByType<FlashlightToggle>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private FlashlightToggle FindEquippedFlashlight()
        {
            if (_flashlights == null) return null;

            foreach (FlashlightToggle candidate in _flashlights)
                if (candidate != null && candidate.IsEquipped)
                    return candidate;

            return null;
        }

        // ------------------------------------------------------------------
        // Referencias: primero las serielizadas (Inspector), y si faltan, las
        // resuelve por nombre en la jerarquia que arma la tool de Editor.
        // ------------------------------------------------------------------
        private bool ResolveSceneReferences()
        {
            if (canvas == null)
            {
                Transform canvasTransform = transform.Find(CanvasName);
                if (canvasTransform != null)
                    canvas = canvasTransform.GetComponent<Canvas>();
            }
            if (canvas == null) return false;

            if (group == null)
                group = canvas.GetComponent<CanvasGroup>();
            if (group == null)
                group = canvas.gameObject.AddComponent<CanvasGroup>();

            // El canvas nunca debe quedar visible por un error de referencias.
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            if (icon == null)
                icon = canvas.transform.Find("Icon")?.GetComponent<RawImage>();
            if (icon == null) return false;

            if (ammoHUD == null)
                ammoHUD = GetComponent<AmmoOverviewHUD>();

            if (flashlight == null)
                RefreshFlashlights();

            return true;
        }
    }
}
