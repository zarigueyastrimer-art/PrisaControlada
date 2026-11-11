using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrisaControlada.Menu
{
    /// <summary>
    /// Marcador (pestaña) del libro del menú.
    ///
    /// El sprite ocupa toda la pantalla con el fondo transparente y la pestaña pintada
    /// dentro de <c>pixelArea</c>, así que el raycast se limita a esa zona: el marcador
    /// nunca tapa los botones de las páginas. Mientras el marcador queda detrás del libro
    /// sólo se puede clicar la parte visible (por encima del borde del libro).
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public class BookMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler, ICanvasRaycastFilter
    {
        [Header("Zona de la pestaña (pixeles del sprite, origen arriba-izquierda)")]
        [SerializeField] private Rect pixelArea = new Rect(0f, 0f, 1920f, 1080f);
        [SerializeField] private Vector2 spriteSize = new Vector2(1920f, 1080f);

        [Header("Detrás del libro")]
        [Tooltip("Borde superior del libro en pixeles del sprite (origen arriba-izquierda).")]
        [SerializeField] private float bookTopY = 104f;

        [Header("Tinte")]
        [SerializeField] private Color idleColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        [SerializeField] private Color hoverColor = Color.white;
        [SerializeField] private Color selectedColor = Color.white;

        [Header("Texto de la pestaña")]
        [SerializeField] private Text labelText;
        [SerializeField] private Color labelColor = new Color(0.24f, 0.15f, 0.10f, 1f);
        [SerializeField] private int labelFontSize = 26;

        /// <summary>Se dispara con el click izquierdo dentro de la pestaña.</summary>
        public event Action Clicked;

        private Image _image;
        private bool _hovered;
        private bool _selected;

        private void Awake()
        {
            _image = GetComponent<Image>();
            ApplyTint();
        }

        /// <summary>Lo llama <see cref="BookMenuController"/> al cambiar de página.</summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;
            ApplyTint();
        }

        /// <summary>Asigna el texto que se muestra sobre la pestaña.</summary>
        public void SetLabel(string text)
        {
            if (labelText == null) return;
            labelText.text = text;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            ApplyTint();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            ApplyTint();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, eventCamera, out var local))
                return false;

            var rect = rt.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return false;

            // Punto en el espacio del sprite de origen (1920x1080, origen arriba-izquierda).
            float px = (local.x - rect.xMin) / rect.width * spriteSize.x;
            float py = (rect.yMax - local.y) / rect.height * spriteSize.y;

            if (px < pixelArea.xMin || px > pixelArea.xMax || py < pixelArea.yMin || py > pixelArea.yMax)
                return false;

            // Detrás del libro sólo cuenta la parte que se ve por encima del borde.
            if (!_selected && py > bookTopY) return false;

            return true;
        }

        private void ApplyTint()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (_image == null) return;

            _image.color = _selected ? selectedColor : (_hovered ? hoverColor : idleColor);
        }
    }
}
