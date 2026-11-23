using PrisaControlada.Core;
using PrisaControlada.Items;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PrisaControlada.Menu
{
    //TP2 - Matías Morilla
    // Página "historia" del menú: la lista de notas como signos de
    // interrogación. Por cada nota del catálogo hay una fila:
    //  - todavía no encontrada: "?";
    //  - encontrada: el título + un botón "Leer";
    //  - al apretar "Leer": la nota aparece en pantalla (popup) y se cierra
    //    con cualquier tecla o click.
    //
    // Las filas se arman en Start a partir del catálogo, así que si agregás
    // notas al asset, el menú las muestra solas. Va en "NotasLista"
    // (hijo de PaginaHistoria/Derecha), que crea el setup.
    [DisallowMultipleComponent]
    public class NotasMenu : MonoBehaviour
    {
        private static NotasMenu _instance;

        [Header("UI (la crea el setup)")]
        [Tooltip("Contenedor de las filas (con VerticalLayoutGroup). Si se deja vacío se usa este GameObject.")]
        [SerializeField] private Transform lista;

        [Tooltip("Texto del popup que muestra la nota completa.")]
        [SerializeField] private TextMeshProUGUI popupTexto;

        [Tooltip("CanvasGroup del popup (se oculta al arrancar).")]
        [SerializeField] private CanvasGroup popupGroup;

        [Tooltip("TMP de referencia: le copia fuente y color a las filas.")]
        [SerializeField] private TextMeshProUGUI estilo;

        [Tooltip("Sprite del botón 'Leer' (el mismo de los botones del menú).")]
        [SerializeField] private Sprite spriteBoton;

        [Header("Catálogo")]
        [Tooltip("Si se deja vacío se carga Resources/NotasCatalogo (el que crea el setup).")]
        [SerializeField] private NotasCatalogo catalogo;

        private bool _popupAbierto;
        private bool _popupJustOpened;

        public static NotasMenu Instance => _instance;

        // Lo lee ResetNotasInput para no resetear con la misma tecla que cierra.
        public static bool PopupAbierto => _instance != null && _instance._popupAbierto;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            OcultarPopup();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Start()
        {
            Pintar();
        }

        private void Update()
        {
            if (!_popupAbierto) return;

            // Un frame de margen para no cerrarse con el mismo click que lo abrió.
            if (_popupJustOpened)
            {
                _popupJustOpened = false;
                return;
            }

            // Cerrar con cualquier tecla o click
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            bool anyKey = kb != null && kb.anyKey.wasPressedThisFrame;
            bool anyClick = mouse != null && mouse.leftButton.wasPressedThisFrame;

            if (anyKey || anyClick)
                OcultarPopup();
        }

        // (Re)arma las filas según el catálogo y lo que coleccionaste.
        // Lo llama Start y también ResetNotasInput después de apretar P.
        public void Pintar()
        {
            Transform contenedor = lista != null ? lista : transform;

            // Limpiar filas anteriores (por si se repinta en el mismo momento).
            for (int i = contenedor.childCount - 1; i >= 0; i--)
                Destroy(contenedor.GetChild(i).gameObject);

            NotasCatalogo cat = catalogo != null
                ? catalogo
                : Resources.Load<NotasCatalogo>("NotasCatalogo");

            if (cat == null)
            {
                Debug.LogWarning("[NotasMenu] No hay catálogo de notas.", this);
                return;
            }

            var coleccionadas = Guardado.CargarNotas();

            foreach (NotasCatalogo.Nota nota in cat.notas)
            {
                if (nota == null || string.IsNullOrEmpty(nota.id)) continue;
                CrearFila(nota, contenedor, coleccionadas.Contains(nota.id));
            }
        }

        private void CrearFila(NotasCatalogo.Nota nota, Transform contenedor, bool tiene)
        {
            GameObject fila = new GameObject(tiene ? "Nota_" + nota.id : "Nota_?", typeof(RectTransform));
            fila.transform.SetParent(contenedor, false);

            // Las filas miden lo que necesitan (título) y ocupan todo el ancho.
            HorizontalLayoutGroup h = fila.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(6, 6, 4, 4);
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = false;

            // Título de la nota, o "?" si todavía no la encontraste.
            GameObject txtGo = new GameObject("Titulo", typeof(RectTransform));
            txtGo.transform.SetParent(fila.transform, false);

            TextMeshProUGUI txt = txtGo.AddComponent<TextMeshProUGUI>();
            Estilizar(txt);
            txt.text = tiene ? nota.titulo : "?";
            txt.alignment = TextAlignmentOptions.Left;
            if (!tiene)
                txt.fontStyle = FontStyles.Italic;

            LayoutElement leTxt = txtGo.AddComponent<LayoutElement>();
            leTxt.flexibleWidth = 1f;

            if (!tiene) return;

            // Botón "Leer": abre la nota en pantalla.
            GameObject btnGo = new GameObject("BtnLeer", typeof(RectTransform));
            btnGo.transform.SetParent(fila.transform, false);

            Image img = btnGo.AddComponent<Image>();
            if (spriteBoton != null)
                img.sprite = spriteBoton;

            Button btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;

            LayoutElement leBtn = btnGo.AddComponent<LayoutElement>();
            leBtn.minWidth = 90f;
            leBtn.preferredWidth = 90f;
            leBtn.flexibleWidth = 0f;

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(btnGo.transform, false);

            RectTransform lblRect = lblGo.GetComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
            Estilizar(lbl);
            lbl.text = "Leer";
            lbl.fontSize = 16f;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.raycastTarget = false;

            NotasCatalogo.Nota n = nota;
            btn.onClick.AddListener(() => AbrirPopup(n));
        }

        // Copia fuente/color del texto de referencia del libro.
        private void Estilizar(TextMeshProUGUI t)
        {
            if (estilo != null)
            {
                t.font = estilo.font;
                t.color = estilo.color;
            }
            else
            {
                t.color = new Color(0.24f, 0.15f, 0.10f, 1f);
            }

            t.fontSize = 18f;
            t.raycastTarget = false;
        }

        private void AbrirPopup(NotasCatalogo.Nota nota)
        {
            if (popupTexto == null || popupGroup == null)
            {
                Debug.LogWarning("[NotasMenu] Falta el popup: corré Tools > PrisaControlada > Notas: Setup en el menú.", this);
                return;
            }

            popupTexto.text = "<b>" + nota.titulo + "</b>\n\n" +
                              nota.texto +
                              "\n\n<i><size=70%>cualquier tecla para cerrar</size></i>";

            _popupAbierto = true;
            _popupJustOpened = true;
            popupGroup.alpha = 1f;
            popupGroup.interactable = true;
            popupGroup.blocksRaycasts = true;
        }

        private void OcultarPopup()
        {
            _popupAbierto = false;
            _popupJustOpened = false;

            if (popupGroup == null) return;
            popupGroup.alpha = 0f;
            popupGroup.interactable = false;
            popupGroup.blocksRaycasts = false;
        }
    }
}
