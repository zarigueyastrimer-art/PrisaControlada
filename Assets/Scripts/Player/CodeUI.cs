using System.Text;
using PrisaControlada.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    // HUD numerico para puertas con codigo de 4 digitos.
    [DisallowMultipleComponent]
    public class CodeUI : MonoBehaviour
    {
        private const int CodeLength = 4;

        private static CodeUI _instance;

        private CodeDoor _door;
        private readonly StringBuilder _input = new StringBuilder(CodeLength);

        private GameObject _root;
        private CanvasGroup _group;
        private Text _display;
        private Text _status;
        private PlayerController _player;
        private InventoryController _inventory;
        private PlayerCamera _camera;
        private bool _built;

        public static bool IsOpen => _instance != null && _instance._door != null;

        public static CodeUI Instance
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = FindObjectOfType<CodeUI>();
                if (_instance == null)
                {
                    GameObject player = GameObject.Find("Player");
                    GameObject go = new GameObject("CodeUI");
                    if (player != null)
                        go.transform.SetParent(player.transform, false);
                    _instance = go.AddComponent<CodeUI>();
                }

                _instance.EnsureUi();
                return _instance;
            }
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
            HideRoot();

            _player = GetComponent<PlayerController>();
            if (_player == null) _player = FindObjectOfType<PlayerController>();
            _inventory = GetComponent<InventoryController>();
            if (_inventory == null) _inventory = FindObjectOfType<InventoryController>();
            _camera = GetComponentInChildren<PlayerCamera>();
            if (_camera == null && _player != null) _camera = _player.GetComponentInChildren<PlayerCamera>();
            if (_camera == null) _camera = FindObjectOfType<PlayerCamera>();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (_door == null) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (kb.backspaceKey.wasPressedThisFrame)
            {
                RemoveDigit();
                return;
            }

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                Submit();
                return;
            }

            if (kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame) AddDigit('0');
            else if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) AddDigit('1');
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) AddDigit('2');
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) AddDigit('3');
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) AddDigit('4');
            else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) AddDigit('5');
            else if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) AddDigit('6');
            else if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) AddDigit('7');
            else if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) AddDigit('8');
            else if (kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame) AddDigit('9');
        }

        public void Show(CodeDoor door)
        {
            if (door == null) return;

            EnsureUi();
            _door = door;
            _input.Length = 0;
            RefreshDisplay();
            SetStatus("Ingrese el codigo de 4 digitos");

            if (_root != null) _root.SetActive(true);
            if (_group != null)
            {
                _group.alpha = 1f;
                _group.blocksRaycasts = true;
                _group.interactable = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_player != null) _player.SetControlEnabled(false);
            if (_inventory != null) _inventory.SetInteractBlocked(true);
            if (_camera != null) _camera.SetLookEnabled(false);
        }

        public void Close()
        {
            _door = null;
            _input.Length = 0;
            HideRoot();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (_player != null) _player.SetControlEnabled(true);
            if (_inventory != null) _inventory.SetInteractBlocked(false);
            if (_camera != null) _camera.SetLookEnabled(true);
        }

        public void Submit()
        {
            if (_door == null) return;

            string code = _input.ToString();
            if (code.Length < CodeLength)
            {
                SetStatus("Faltan digitos");
                return;
            }

            if (_door.TryUnlock(code))
            {
                SetStatus("Acceso concedido");
                InteractionPrompt.Show("Puerta abierta.");
                Close();
            }
            else
            {
                SetStatus("Codigo incorrecto");
                _input.Length = 0;
                RefreshDisplay();
            }
        }

        public void ClearInput()
        {
            _input.Length = 0;
            RefreshDisplay();
            SetStatus("Ingrese el codigo de 4 digitos");
        }

        public void AddDigit(char digit)
        {
            if (_door == null || _input.Length >= CodeLength) return;
            if (digit < '0' || digit > '9') return;

            _input.Append(digit);
            RefreshDisplay();

            if (_input.Length == CodeLength)
                Submit();
        }

        public void RemoveDigit()
        {
            if (_input.Length <= 0) return;
            _input.Length -= 1;
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            if (_display == null) return;

            StringBuilder sb = new StringBuilder(CodeLength * 2);
            for (int i = 0; i < CodeLength; i++)
            {
                if (i < _input.Length)
                    sb.Append(_input[i]);
                else
                    sb.Append('_');

                if (i < CodeLength - 1)
                    sb.Append(' ');
            }

            _display.text = sb.ToString();
        }

        private void SetStatus(string message)
        {
            if (_status != null)
                _status.text = message;
        }

        private void HideRoot()
        {
            if (_root != null) _root.SetActive(false);
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }
        }

        private void EnsureUi()
        {
            if (_built && _root != null) return;
            BuildUi();
            _built = true;
        }

        private void BuildUi()
        {
            Transform existing = transform.Find("CodeUI_Canvas");
            if (existing != null)
            {
                _root = existing.gameObject;
                _group = _root.GetComponent<CanvasGroup>();
                _display = _root.transform.Find("Panel/Display")?.GetComponent<Text>();
                _status = _root.transform.Find("Panel/Status")?.GetComponent<Text>();
                if (_display != null) return;
            }

            _root = new GameObject("CodeUI_Canvas", typeof(RectTransform));
            _root.transform.SetParent(transform, false);

            RectTransform canvasRect = _root.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            Canvas canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;

            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _root.AddComponent<GraphicRaycaster>();
            _group = _root.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Fondo tenue para centrar la atencion.
            GameObject dim = CreateRect("Dim", _root.transform);
            Stretch(dim.GetComponent<RectTransform>());
            Image dimImage = dim.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.55f);
            dimImage.raycastTarget = true;

            GameObject panel = CreateRect("Panel", _root.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(420f, 520f);
            panelRect.anchoredPosition = Vector2.zero;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.96f);

            GameObject title = CreateText(panel.transform, "Title", "CODIGO", 30, TextAnchor.UpperCenter);
            RectTransform titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.88f);
            titleRect.anchorMax = new Vector2(1f, 0.98f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            GameObject displayGo = CreateText(panel.transform, "Display", "_ _ _ _", 44, TextAnchor.MiddleCenter);
            _display = displayGo.GetComponent<Text>();
            _display.fontStyle = FontStyle.Bold;
            _display.color = new Color(0.4f, 1f, 0.55f);
            RectTransform displayRect = displayGo.GetComponent<RectTransform>();
            displayRect.anchorMin = new Vector2(0.05f, 0.74f);
            displayRect.anchorMax = new Vector2(0.95f, 0.86f);
            displayRect.offsetMin = Vector2.zero;
            displayRect.offsetMax = Vector2.zero;

            GameObject statusGo = CreateText(panel.transform, "Status", string.Empty, 22, TextAnchor.MiddleCenter);
            _status = statusGo.GetComponent<Text>();
            _status.color = new Color(1f, 0.85f, 0.3f);
            RectTransform statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.05f, 0.66f);
            statusRect.anchorMax = new Vector2(0.95f, 0.73f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            BuildNumpad(panel.transform);

            RefreshDisplay();
        }

        private void BuildNumpad(Transform panel)
        {
            // Grid 4x4: 1-9, Borrar, 0, OK  (cancel via ESC)
            string[,] layout =
            {
                { "1", "2", "3", "C" },
                { "4", "5", "6", "<" },
                { "7", "8", "9", "OK" },
                { "",  "0", "",  "" }
            };

            float padTop = 0.62f;
            float padBottom = 0.08f;
            float padLeft = 0.08f;
            float padRight = 0.92f;
            int rows = 4;
            int cols = 4;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    string label = layout[r, c];
                    if (string.IsNullOrEmpty(label)) continue;

                    float x0 = padLeft + (padRight - padLeft) * (c / (float)cols);
                    float x1 = padLeft + (padRight - padLeft) * ((c + 1) / (float)cols);
                    float y1 = padTop - (padTop - padBottom) * (r / (float)rows);
                    float y0 = padTop - (padTop - padBottom) * ((r + 1) / (float)rows);

                    GameObject btnGo = CreateRect("Btn_" + label, panel);
                    RectTransform rt = btnGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(x0, y0);
                    rt.anchorMax = new Vector2(x1, y1);
                    rt.offsetMin = new Vector2(4f, 4f);
                    rt.offsetMax = new Vector2(-4f, -4f);

                    Image img = btnGo.AddComponent<Image>();
                    img.color = label == "OK"
                        ? new Color(0.15f, 0.45f, 0.2f, 1f)
                        : new Color(0.18f, 0.18f, 0.22f, 1f);

                    Button button = btnGo.AddComponent<Button>();
                    button.targetGraphic = img;

                    GameObject labelGo = CreateText(btnGo.transform, "Label", label, label == "OK" ? 22 : 28, TextAnchor.MiddleCenter);
                    Stretch(labelGo.GetComponent<RectTransform>());

                    string captured = label;
                    button.onClick.AddListener(() => OnButton(captured));
                }
            }
        }

        private void OnButton(string label)
        {
            switch (label)
            {
                case "C":
                    ClearInput();
                    break;
                case "<":
                    RemoveDigit();
                    break;
                case "OK":
                    Submit();
                    break;
                default:
                    if (label.Length == 1 && label[0] >= '0' && label[0] <= '9')
                        AddDigit(label[0]);
                    break;
            }
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject CreateText(Transform parent, string name, string content, int size, TextAnchor anchor)
        {
            GameObject go = CreateRect(name, parent);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            return go;
        }
    }
}
