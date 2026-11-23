using TMPro;
using PrisaControlada.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.Items
{
    //TP2 - Matías Morilla
    // Visor de notas de lore (texto). Vive sobre "NotaLoreCanvas", el canvas
    // que ya está en la escena con RawImage + título + cuerpo.
    //
    // Espejo del NoteViewer: pausa el juego, bloquea la interacción y se
    // cierra con cualquier tecla o click. La diferencia es que acá el UI
    // viene de la escena (no se auto-crea): si no está, avisa en consola
    // en vez de romper.
    [DisallowMultipleComponent]
    public class NotaLoreViewer : MonoBehaviour
    {
        private static NotaLoreViewer _instance;

        [Header("UI (se auto-busca si se deja vacío)")]
        [Tooltip("CanvasGroup que oculta/muestra todo el canvas de la nota.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("Primer TMP del canvas: título de la nota.")]
        [SerializeField] private TextMeshProUGUI titulo;

        [Tooltip("Segundo TMP del canvas: cuerpo de la nota.")]
        [SerializeField] private TextMeshProUGUI cuerpo;

        private bool _isOpen;
        private bool _justOpened;

        public static bool IsOpen => _instance != null && _instance._isOpen;

        // Solo busca en la escena: este visor necesita el canvas de la escena.
        public static NotaLoreViewer Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<NotaLoreViewer>();
                return _instance;
            }
        }

        // Muestra una nota y pausa el juego. Devuelve false si falta el UI.
        public static bool Mostrar(NotasCatalogo.Nota nota)
        {
            NotaLoreViewer visor = Instance;
            if (visor == null)
            {
                Debug.LogWarning("[NotaLore] No hay NotaLoreViewer en la escena: " +
                                 "corré Tools > PrisaControlada > Notas: Setup.");
                return false;
            }

            if (nota == null) return false;

            visor.EnsureUi();

            if (visor.titulo != null)
                visor.titulo.text = string.IsNullOrEmpty(nota.titulo) ? nota.id : nota.titulo;

            if (visor.cuerpo != null)
                visor.cuerpo.text = nota.texto ?? string.Empty;

            visor._isOpen = true;
            visor._justOpened = true;
            visor.SetVisible(true);

            // Pausar juego
            Time.timeScale = 0f;

            // Bloquear input del jugador
            InventoryController inv = FindFirstObjectByType<InventoryController>();
            if (inv != null) inv.SetInteractBlocked(true);

            return true;
        }

        public static void Cerrar()
        {
            if (_instance != null)
                _instance.Hide();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureUi();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            // Asegurar que el tiempo no quede en 0 si se destruye con la escena
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (!_isOpen) return;

            // Esperar un frame despues de abrir para no cerrarse con la misma tecla que lo abrio
            if (_justOpened)
            {
                _justOpened = false;
                return;
            }

            // Cerrar con cualquier tecla o click
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            bool anyKey = kb != null && kb.anyKey.wasPressedThisFrame;
            bool anyClick = mouse != null && mouse.leftButton.wasPressedThisFrame;

            if (anyKey || anyClick)
                Hide();
        }

        private void Hide()
        {
            _isOpen = false;
            SetVisible(false);

            // Reanudar juego
            Time.timeScale = 1f;

            // Desbloquear input
            InventoryController inv = FindFirstObjectByType<InventoryController>();
            if (inv != null) inv.SetInteractBlocked(false);
        }

        private void SetVisible(bool visible)
        {
            if (group == null) return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        // Busca el canvas (hijo que tiene el componente Canvas) y los TMPs si
        // no se asignaron a mano. El orden del árbol es RawImage, título,
        // cuerpo, así que el primer TMP es el título y el segundo el cuerpo.
        private void EnsureUi()
        {
            if (group == null)
            {
                Canvas canvas = GetComponentInChildren<Canvas>(true);
                if (canvas != null)
                {
                    group = canvas.GetComponent<CanvasGroup>();
                    if (group == null)
                        group = canvas.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (titulo == null || cuerpo == null)
            {
                TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);

                if (titulo == null && texts.Length > 0)
                    titulo = texts[0];

                if (cuerpo == null && texts.Length > 1)
                    cuerpo = texts[1];
            }
        }
    }
}
