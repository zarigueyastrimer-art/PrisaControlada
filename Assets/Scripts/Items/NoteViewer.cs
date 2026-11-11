using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace PrisaControlada.Items
{
    // Visor de notas. Muestra una imagen a pantalla completa, pausa el juego
    // y se cierra con cualquier tecla/click. Singleton, se auto-crea.
    public class NoteViewer : MonoBehaviour
    {
        private static NoteViewer _instance;

        private CanvasGroup _group;
        private Image _bgImage;
        private Image _noteImage;
        private bool _isOpen;
        private bool _justOpened;

        public static bool IsOpen => _instance != null && _instance._isOpen;

        public static NoteViewer Instance
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = FindObjectOfType<NoteViewer>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("NoteViewer");
                    _instance = go.AddComponent<NoteViewer>();
                }
                _instance.EnsureUi();
                return _instance;
            }
        }

        public static void Show(Texture2D image)
        {
            NoteViewer viewer = Instance;
            if (viewer == null) return;
            viewer.EnsureUi();

            if (image != null)
                viewer._noteImage.sprite = Sprite.Create(image, new Rect(0, 0, image.width, image.height), Vector2.one * 0.5f);

            viewer._noteImage.gameObject.SetActive(image != null);
            viewer._isOpen = true;
            viewer._justOpened = true;
            viewer._group.alpha = 1f;
            viewer._group.interactable = true;
            viewer._group.blocksRaycasts = true;

            // Pausar juego
            Time.timeScale = 0f;

            // Bloquear input del jugador
            var inv = FindObjectOfType<Player.InventoryController>();
            if (inv != null) inv.SetInteractBlocked(true);
        }

        public static void Close()
        {
            if (_instance == null) return;
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
            HideImmediate();
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
            HideImmediate();

            // Reanudar juego
            Time.timeScale = 1f;

            // Desbloquear input
            var inv = FindObjectOfType<Player.InventoryController>();
            if (inv != null) inv.SetInteractBlocked(false);
        }

        private void HideImmediate()
        {
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        private void EnsureUi()
        {
            if (_group != null && _bgImage != null && _noteImage != null) return;

            // Canvas fullscreen
            GameObject canvasGo = new GameObject("NoteViewer_Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.sizeDelta = Vector2.zero;

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            // Fondo negro semitransparente
            _bgImage = CreateFullscreenImage(canvasGo.transform, "Background", new Color(0f, 0f, 0f, 0.88f));

            // Imagen de la nota centrada
            GameObject noteGo = CreateFullscreenImage(canvasGo.transform, "NoteImage", Color.white).gameObject;
            _noteImage = noteGo.GetComponent<Image>();
            _noteImage.preserveAspect = true;

            RectTransform noteRect = noteGo.GetComponent<RectTransform>();
            noteRect.anchorMin = new Vector2(0.5f, 0.5f);
            noteRect.anchorMax = new Vector2(0.5f, 0.5f);
            noteRect.sizeDelta = new Vector2(800f, 800f);
            noteRect.anchoredPosition = Vector2.zero;
        }

        private static Image CreateFullscreenImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            return img;
        }
    }
}
