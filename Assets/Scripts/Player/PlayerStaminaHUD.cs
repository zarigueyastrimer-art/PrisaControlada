using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    // HUD de stamina: crea su Canvas y barra automaticamente en runtime.
    // La barra se escala por ancho (no usa fillAmount, funciona sin sprite).
    [DisallowMultipleComponent]
    public class PlayerStaminaHUD : MonoBehaviour
    {
        [Header("Ajustes visuales")]
        [SerializeField] private float barWidth = 250f;
        [SerializeField] private float barHeight = 18f;
        [SerializeField] private float offsetY = 30f;
        [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.25f;
        [SerializeField] private Color normalColor = new Color(0.15f, 0.85f, 0.25f);
        [SerializeField] private Color lowColor = new Color(0.9f, 0.1f, 0.1f);

        // Evita que se dibujen 2 barras si hay varios componentes en la escena.
        private static PlayerStaminaHUD _active;

        private PlayerController _player;
        private RectTransform _fillRT;
        private Image _fill;
        private bool _ownsBar;

        private void Awake()
        {
            if (_active != null && _active != this)
            {
                // Otro PlayerStaminaHUD ya dibuja la barra: este no hace nada.
                _ownsBar = false;
                enabled = false;
                return;
            }

            _active = this;
            _ownsBar = true;

            _player = GetComponent<PlayerController>();
            if (_player == null)
                _player = GetComponentInParent<PlayerController>();

            BuildUI();
        }

        private void OnDestroy()
        {
            if (_active == this)
                _active = null;
        }

        private void OnEnable()
        {
            if (_player != null)
                _player.OnStaminaChanged += OnStaminaChanged;
        }

        private void OnDisable()
        {
            if (_player != null)
                _player.OnStaminaChanged -= OnStaminaChanged;
        }

        private void BuildUI()
        {
            // Canvas propio, siempre encima de todo
            GameObject canvasGo = new GameObject("StaminaHUD_Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // Panel contenedor centrado arriba
            GameObject panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 1f);
            panelRT.anchorMax = new Vector2(0.5f, 1f);
            panelRT.pivot = new Vector2(0.5f, 1f);
            panelRT.anchoredPosition = new Vector2(0f, -offsetY);
            panelRT.sizeDelta = new Vector2(barWidth, barHeight);

            // Fondo negro
            Image bg = CreateImage(panel.transform, "BG", new Color(0f, 0f, 0f, 0.55f));
            SetStretch(bg.rectTransform);

            // Relleno verde/rojo: se escala por ancho (anchorMax.x = t)
            _fill = CreateImage(panel.transform, "Fill", normalColor);
            _fillRT = _fill.rectTransform;
            _fillRT.anchorMin = Vector2.zero;
            _fillRT.anchorMax = new Vector2(1f, 1f);
            _fillRT.offsetMin = Vector2.zero;
            _fillRT.offsetMax = Vector2.zero;
        }

        private Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void OnStaminaChanged(float current, float max)
        {
            UpdateBar(current, max);
        }

        private void Update()
        {
            if (_player != null)
                UpdateBar(_player.CurrentStamina, _player.MaxStamina);
        }

        private void UpdateBar(float current, float max)
        {
            if (_fillRT == null) return;

            float t = max > 0f ? Mathf.Clamp01(current / max) : 1f;

            // Escalar el ancho del fill: 1 = completa, 0 = vacia.
            _fillRT.anchorMax = new Vector2(t, 1f);

            // Color segun umbral
            _fill.color = t <= lowThreshold ? lowColor : normalColor;
        }
    }
}