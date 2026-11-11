using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Menu
{
    /// <summary>
    /// Arma el menú de libro sobre la escena que está abierta (no la guarda):
    /// fondo FondoLibro + los 4 marcadores como pestañas clicables, cada uno con su página.
    ///
    /// Es idempotente: si ya existe algo (incluidos objetos que hayas colocado a mano con
    /// esos sprites) se reutiliza en vez de duplicarlo.
    /// </summary>
    public static class BookMenuSetup
    {
        private const string Folder = "Assets/Scenes/menu/";
        private const string BookSpriteFile = Folder + "FondoLibro.png";

        private static readonly Vector2 SpriteSize = new Vector2(1920f, 1080f);
        private const float BookTopY = 104f;      // borde superior del libro, en pixeles del sprite
        private const float HitPadding = 6f;      // un poco de margen alrededor de la pestaña

        // Zona de las dos páginas del libro, en pixeles del sprite (origen arriba-izquierda).
        private static readonly Rect PagesZone = new Rect(275f, 150f, 1410f, 885f);

        private sealed class MarkerDef
        {
            public string id;
            public string fileName;
            public string markerName;
            public string label;        // texto que se muestra sobre el marcador
            public string pageName;
            public string pageTitle;
            public Rect pixelArea;
            public BookMenuController.TabAction action;
        }

        private static readonly MarkerDef[] Markers =
        {
            new MarkerDef
            {
                id = "jugado",
                fileName = "marcadorJugar.png",
                markerName = "MarcadorJugar",
                label = "jugar",
                pageName = "PaginaJugado",
                pageTitle = "JUGADO",
                pixelArea = new Rect(342f, 34f, 161f, 98f)
            },
            new MarkerDef
            {
                id = "opciones",
                fileName = "marcadorOpciones.png",
                markerName = "MarcadorOpciones",
                label = "opciones",
                pageName = "PaginaOpciones",
                pageTitle = "OPCIONES",
                pixelArea = new Rect(518f, 21f, 161f, 98f)
            },
            new MarkerDef
            {
                id = "creditos",
                fileName = "marcadorCreditos.png",
                markerName = "MarcadorCreditos",
                label = "creditos",
                pageName = "PaginaCreditos",
                pageTitle = "CREDITOS",
                pixelArea = new Rect(698f, 29f, 161f, 100f)
            },
            new MarkerDef
            {
                id = "salir",
                fileName = "marcadorSalir.png",
                markerName = "MarcadorSalir",
                label = "salir",
                pageName = "PaginaSalir",
                pageTitle = null,
                pixelArea = new Rect(1461f, 36f, 190f, 98f),
                action = BookMenuController.TabAction.None
            },
            new MarkerDef
            {
                id = "historia",
                fileName = "marcadorHistoria.png",
                markerName = "MarcadorHistoria",
                label = null,
                pageName = "PaginaHistoria",
                pageTitle = null,
                pixelArea = new Rect(1050f, 30f, 160f, 100f),
                action = BookMenuController.TabAction.None
            }
        };

        [MenuItem("PrisaControlada/Menu Libro: Armar en escena activa")]
        public static void Build()
        {
            foreach (var def in Markers)
                EnsureSprite(Folder + def.fileName);
            EnsureSprite(BookSpriteFile);

            var canvas = FindOrCreateCanvas();
            var scene = canvas.gameObject.scene;
            if (!scene.path.EndsWith("MainMenu.unity"))
                Debug.LogWarning($"[Menu Libro] La escena activa es '{scene.name}' y no es MainMenu. Se arma igual; si no es lo que querés, revertí con Ctrl+Z.");

            // --- Fondo del libro ---
            var root = GetOrAdoptRect(canvas.transform, "BookMenu", null);
            Stretch(root);

            var bookImage = GetOrAdoptImage(root, "Libro", BookSpriteFile);
            bookImage.raycastTarget = false;
            bookImage.color = Color.white;

            // --- Páginas (siempre por encima del libro) ---
            var pagesRoot = GetOrAdoptRect(root, "Paginas", null);
            pagesRoot.anchorMin = new Vector2(PagesZone.xMin / SpriteSize.x, (SpriteSize.y - PagesZone.yMax) / SpriteSize.y);
            pagesRoot.anchorMax = new Vector2(PagesZone.xMax / SpriteSize.x, (SpriteSize.y - PagesZone.yMin) / SpriteSize.y);
            pagesRoot.offsetMin = Vector2.zero;
            pagesRoot.offsetMax = Vector2.zero;

            var font = LoadFont();
            var markers = new BookMarker[Markers.Length];
            var pages = new GameObject[Markers.Length];

            for (int i = 0; i < Markers.Length; i++)
            {
                var def = Markers[i];
                markers[i] = GetOrAdoptMarker(root, def, font);
                pages[i] = def.pageName == null ? null : CreatePage(pagesRoot, def, font);
            }

            WireController(root, bookImage, pagesRoot, markers, pages);
            ApplyOrder(bookImage.transform, pagesRoot, markers);

            // Sólo la página inicial visible en el editor.
            for (int i = 0; i < pages.Length; i++)
                if (pages[i] != null)
                    pages[i].SetActive(i == 0);

            HideLegacyUi();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("<color=#77ff77>[Menu Libro] Menú de libro armado en la escena activa. Probalo en Play y guardá con Ctrl+S (no se guardó solo).</color>");
        }

        // ---------------------------------------------------------------- estructura

        private static Canvas FindOrCreateCanvas()
        {
            Canvas canvas = null;
            foreach (var candidate in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (candidate.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (candidate.name == "MainCanvas") { canvas = candidate; break; }
                if (canvas == null) canvas = candidate;
            }

            if (canvas != null) return canvas;

            var go = new GameObject("MainCanvas", typeof(RectTransform));
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(go, "Menu Libro");
            return canvas;
        }

        private static RectTransform GetOrAdoptRect(Transform parent, string name, string texturePath)
        {
            var found = parent.Find(name);
            if (found != null)
            {
                var current = found as RectTransform;
                if (current != null) return current;
            }

            // Si ya hay un objeto en la escena con ese sprite (colocado a mano), se adopta.
            if (!string.IsNullOrEmpty(texturePath))
            {
                var existing = FindImageByTexture(texturePath);
                if (existing != null)
                {
                    Undo.SetTransformParent(existing.transform, parent, "Menu Libro");
                    existing.name = name;
                    if (!existing.gameObject.activeSelf)
                    {
                        Undo.RecordObject(existing.gameObject, "Menu Libro");
                        existing.gameObject.SetActive(true);
                    }
                    var rt = existing.rectTransform;
                    rt.localScale = Vector3.one;
                    return rt;
                }
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Menu Libro");
            return (RectTransform)go.transform;
        }

        private static Image GetOrAdoptImage(RectTransform parent, string name, string texturePath)
        {
            var rt = GetOrAdoptRect(parent, name, texturePath);
            Stretch(rt);

            var image = rt.GetComponent<Image>();
            if (image == null) image = Undo.AddComponent<Image>(rt.gameObject);

            image.sprite = LoadSprite(texturePath);
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        private static BookMarker GetOrAdoptMarker(RectTransform root, MarkerDef def, Font font)
        {
            var texturePath = Folder + def.fileName;
            var rt = GetOrAdoptRect(root, def.markerName, texturePath);
            Stretch(rt);

            var image = rt.GetComponent<Image>();
            if (image == null) image = Undo.AddComponent<Image>(rt.gameObject);
            image.sprite = LoadSprite(texturePath);
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = true;
            image.color = Color.white;

            var marker = rt.GetComponent<BookMarker>();
            if (marker == null) marker = Undo.AddComponent<BookMarker>(rt.gameObject);

            var area = def.pixelArea;
            area.x -= HitPadding;
            area.y -= HitPadding;
            area.width += HitPadding * 2f;
            area.height += HitPadding * 2f;
            area.x = Mathf.Max(0f, area.x);
            area.y = Mathf.Max(0f, area.y);
            area.width = Mathf.Min(SpriteSize.x - area.x, area.width);
            area.height = Mathf.Min(SpriteSize.y - area.y, area.height);

            var so = new SerializedObject(marker);
            so.FindProperty("pixelArea").rectValue = area;
            so.FindProperty("spriteSize").vector2Value = SpriteSize;
            so.FindProperty("bookTopY").floatValue = BookTopY;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Texto sobre la pestaña
            AddMarkerLabel(rt, marker, def, font);

            return marker;
        }

        private static void AddMarkerLabel(RectTransform markerRt, BookMarker marker, MarkerDef def, Font font)
        {
            var existing = markerRt.Find("Label");
            if (string.IsNullOrEmpty(def.label))
            {
                // Sin texto: eliminar Label viejo si existe
                if (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                    var soDel = new SerializedObject(marker);
                    soDel.FindProperty("labelText").objectReferenceValue = null;
                    soDel.ApplyModifiedPropertiesWithoutUndo();
                }
                return;
            }

            if (existing != null)
            {
                var text = existing.GetComponent<Text>();
                if (text != null)
                {
                    text.text = def.label;
                    var so = new SerializedObject(marker);
                    so.FindProperty("labelText").objectReferenceValue = text;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                return;
            }

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(markerRt, false);
            Undo.RegisterCreatedObjectUndo(labelGo, "Menu Libro");

            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.5f, 0.5f);
            labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.pivot = new Vector2(0.5f, 0.5f);

            // Centro del pixelArea en coordenadas del sprite (origen arriba-izquierda).
            float cx = def.pixelArea.x + def.pixelArea.width * 0.5f;
            float cySprite = def.pixelArea.y + def.pixelArea.height * 0.5f;
            // Convertir a coordenadas UI (origen abajo-izquierda).
            float cy = SpriteSize.y - cySprite;

            labelRt.anchoredPosition = new Vector2(cx, cy);
            labelRt.sizeDelta = new Vector2(def.pixelArea.width, 40f);

            var textComp = Undo.AddComponent<Text>(labelGo);
            textComp.font = font;
            textComp.text = def.label;
            textComp.fontSize = 26;
            textComp.color = new Color(0.24f, 0.15f, 0.10f, 1f);
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.horizontalOverflow = HorizontalWrapMode.Overflow;
            textComp.verticalOverflow = VerticalWrapMode.Overflow;
            textComp.raycastTarget = false; // no bloquea el click del marcador

            var soMarker = new SerializedObject(marker);
            soMarker.FindProperty("labelText").objectReferenceValue = textComp;
            soMarker.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePage(RectTransform pagesRoot, MarkerDef def, Font font)
        {
            var rt = GetOrAdoptRect(pagesRoot, def.pageName, null);
            Stretch(rt);

            // --- División de la página en Izquierda (0-50%) y Derecha (50-100%) ---
            var izquierda = GetOrAdoptRect(rt, "Izquierda", null);
            izquierda.anchorMin = new Vector2(0f, 0f);
            izquierda.anchorMax = new Vector2(0.5f, 1f);
            izquierda.offsetMin = Vector2.zero;
            izquierda.offsetMax = Vector2.zero;
            izquierda.pivot = new Vector2(0f, 0.5f);

            var derecha = GetOrAdoptRect(rt, "Derecha", null);
            derecha.anchorMin = new Vector2(0.5f, 0f);
            derecha.anchorMax = new Vector2(1f, 1f);
            derecha.offsetMin = Vector2.zero;
            derecha.offsetMax = Vector2.zero;
            derecha.pivot = new Vector2(0f, 0.5f);

            // --- Título centrado sobre toda la página (solo si hay pageTitle) ---
            if (!string.IsNullOrEmpty(def.pageTitle))
            {
                var titleRt = GetOrAdoptRect(rt, "Titulo", null);
                titleRt.anchorMin = new Vector2(0.5f, 1f);
                titleRt.anchorMax = new Vector2(0.5f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.anchoredPosition = new Vector2(0f, -40f);
                titleRt.sizeDelta = new Vector2(700f, 90f);

                var text = titleRt.GetComponent<Text>();
                if (text == null) text = Undo.AddComponent<Text>(titleRt.gameObject);
                text.font = font;
                text.text = def.pageTitle;
                text.fontSize = 46;
                text.color = new Color(0.24f, 0.15f, 0.10f, 1f);
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
            }
            else
            {
                // Sin título: eliminar Titulo viejo si existe
                var oldTitle = rt.Find("Titulo");
                if (oldTitle != null) Undo.DestroyObjectImmediate(oldTitle.gameObject);
            }

            // --- Contenido específico de la página de salir (solo izquierda) ---
            if (def.id == "salir")
                BuildSalirPage(izquierda, font);

            // --- Contenido específico de la página de jugar ---
            if (def.id == "jugado")
                BuildJugadoPage(izquierda, derecha, font);

            return rt.gameObject;
        }

        private static void BuildSalirPage(RectTransform ladoIzquierdo, Font font)
        {
            // Texto: ¿seguro que quieres salir?
            var textoRt = GetOrAdoptRect(ladoIzquierdo, "TextoSalir", null);
            textoRt.anchorMin = new Vector2(0f, 1f);
            textoRt.anchorMax = new Vector2(1f, 1f);
            textoRt.pivot = new Vector2(0.5f, 1f);
            textoRt.anchoredPosition = new Vector2(0f, -60f);
            textoRt.sizeDelta = new Vector2(500f, 70f);

            var texto = textoRt.GetComponent<Text>();
            if (texto == null) texto = Undo.AddComponent<Text>(textoRt.gameObject);
            texto.font = font;
            texto.text = "¿seguro que quieres salir?";
            texto.fontSize = 30;
            texto.color = new Color(0.24f, 0.15f, 0.10f, 1f);
            texto.alignment = TextAnchor.MiddleCenter;
            texto.horizontalOverflow = HorizontalWrapMode.Overflow;
            texto.verticalOverflow = VerticalWrapMode.Overflow;
            texto.raycastTarget = false;

            // Botón "salir" debajo del texto, en la izquierda
            var btnRt = GetOrAdoptRect(ladoIzquierdo, "BotonSalir", null);
            btnRt.anchorMin = new Vector2(0.5f, 1f);
            btnRt.anchorMax = new Vector2(0.5f, 1f);
            btnRt.pivot = new Vector2(0.5f, 1f);
            btnRt.anchoredPosition = new Vector2(0f, -170f);
            btnRt.sizeDelta = new Vector2(260f, 55f);

            var btnImg = btnRt.GetComponent<Image>();
            if (btnImg == null) btnImg = Undo.AddComponent<Image>(btnRt.gameObject);
            btnImg.color = new Color(0.5f, 0.1f, 0.1f, 0.95f);

            var btn = btnRt.GetComponent<Button>();
            if (btn == null) btn = Undo.AddComponent<Button>(btnRt.gameObject);
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });

            var btnTextGo = new GameObject("Label", typeof(RectTransform));
            btnTextGo.transform.SetParent(btnRt, false);
            Undo.RegisterCreatedObjectUndo(btnTextGo, "Menu Libro");

            var btnTextRt = btnTextGo.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.sizeDelta = Vector2.zero;

            var btnText = btnTextGo.AddComponent<Text>();
            btnText.text = "salir";
            btnText.font = font;
            btnText.fontSize = 24;
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.horizontalOverflow = HorizontalWrapMode.Overflow;
            btnText.verticalOverflow = VerticalWrapMode.Overflow;
            btnText.raycastTarget = false;
        }

        private static void BuildJugadoPage(RectTransform ladoIzquierdo, RectTransform ladoDerecho, Font font)
        {
            if (ladoIzquierdo == null)
            {
                Debug.LogError("[Menu Libro] ladoIzquierdo es null en BuildJugadoPage!");
                return;
            }
            if (ladoDerecho == null)
            {
                Debug.LogError("[Menu Libro] ladoDerecho es null en BuildJugadoPage!");
                return;
            }
            if (font == null)
            {
                Debug.LogError("[Menu Libro] Font es null en BuildJugadoPage, usando fallback");
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            // ---------- IZQUIERDA ----------
            var tituloIzq = GetOrAdoptRect(ladoIzquierdo, "TituloJugando", null);
            if (tituloIzq == null)
            {
                Debug.LogError("[Menu Libro] tituloIzq es null!");
                return;
            }
            tituloIzq.anchorMin = new Vector2(0.5f, 1f);
            tituloIzq.anchorMax = new Vector2(0.5f, 1f);
            tituloIzq.pivot = new Vector2(0.5f, 1f);
            tituloIzq.anchoredPosition = new Vector2(0f, -40f);
            tituloIzq.sizeDelta = new Vector2(460f, 80f);

            var txtTituloIzq = GetOrAddText(tituloIzq);
            if (txtTituloIzq == null)
            {
                Debug.LogError("[Menu Libro] txtTituloIzq es null!");
                return;
            }
            txtTituloIzq.text = "LiminaI Nightmer";
            txtTituloIzq.font = font;
            txtTituloIzq.fontSize = 44;
            txtTituloIzq.color = new Color(0.24f, 0.15f, 0.10f, 1f);
            txtTituloIzq.alignment = TextAnchor.MiddleCenter;
            txtTituloIzq.horizontalOverflow = HorizontalWrapMode.Overflow;
            txtTituloIzq.verticalOverflow = VerticalWrapMode.Overflow;
            txtTituloIzq.raycastTarget = false;

            var subtituloIzq = GetOrAdoptRect(ladoIzquierdo, "SubtituloJugando", null);
            subtituloIzq.anchorMin = new Vector2(0.5f, 1f);
            subtituloIzq.anchorMax = new Vector2(0.5f, 1f);
            subtituloIzq.pivot = new Vector2(0.5f, 1f);
            subtituloIzq.anchoredPosition = new Vector2(0f, -110f);
            subtituloIzq.sizeDelta = new Vector2(420f, 60f);

            var txtSubIzq = GetOrAddText(subtituloIzq);
            txtSubIzq.text = "es como el paciente\nnombre yu estado aunque no\nsabemos que es exactamente";
            txtSubIzq.font = font;
            txtSubIzq.fontSize = 16;
            txtSubIzq.color = new Color(0.24f, 0.15f, 0.10f, 1f);
            txtSubIzq.alignment = TextAnchor.MiddleCenter;
            txtSubIzq.horizontalOverflow = HorizontalWrapMode.Overflow;
            txtSubIzq.verticalOverflow = VerticalWrapMode.Overflow;
            txtSubIzq.raycastTarget = false;

            var btnJugar = CrearBoton(ladoIzquierdo, "BotonJugar", "Jugar",
                Folder + "BotonNormal.png", new Vector2(0f, -220f), new Vector2(260f, 55f), font, 26);

            var btnPrototipo = CrearBoton(ladoIzquierdo, "BotonPrototipo", "Prototipo",
                Folder + "botonChico.png", new Vector2(0f, -300f), new Vector2(220f, 45f), font, 22);

            // ---------- DERECHA ----------
            var tituloDer = GetOrAdoptRect(ladoDerecho, "TituloDificultad", null);
            tituloDer.anchorMin = new Vector2(0.5f, 1f);
            tituloDer.anchorMax = new Vector2(0.5f, 1f);
            tituloDer.pivot = new Vector2(0.5f, 1f);
            tituloDer.anchoredPosition = new Vector2(0f, -40f);
            tituloDer.sizeDelta = new Vector2(460f, 80f);

            var txtTituloDer = GetOrAddText(tituloDer);
            txtTituloDer.text = "la Dificultad";
            txtTituloDer.font = font;
            txtTituloDer.fontSize = 44;
            txtTituloDer.color = new Color(0.24f, 0.15f, 0.10f, 1f);
            txtTituloDer.alignment = TextAnchor.MiddleCenter;
            txtTituloDer.horizontalOverflow = HorizontalWrapMode.Overflow;
            txtTituloDer.verticalOverflow = VerticalWrapMode.Overflow;
            txtTituloDer.raycastTarget = false;

            var subtituloDer = GetOrAdoptRect(ladoDerecho, "SubtituloDificultad", null);
            subtituloDer.anchorMin = new Vector2(0.5f, 1f);
            subtituloDer.anchorMax = new Vector2(0.5f, 1f);
            subtituloDer.pivot = new Vector2(0.5f, 1f);
            subtituloDer.anchoredPosition = new Vector2(0f, -110f);
            subtituloDer.sizeDelta = new Vector2(420f, 70f);

            var txtSubDer = GetOrAddText(subtituloDer);
            txtSubDer.text = "mental por la que pasa el\npaciente es difícil de\ncomprender para crazy\ncuidoso...";
            txtSubDer.font = font;
            txtSubDer.fontSize = 16;
            txtSubDer.color = new Color(0.24f, 0.15f, 0.10f, 1f);
            txtSubDer.alignment = TextAnchor.MiddleCenter;
            txtSubDer.horizontalOverflow = HorizontalWrapMode.Overflow;
            txtSubDer.verticalOverflow = VerticalWrapMode.Overflow;
            txtSubDer.raycastTarget = false;

            string[] filasTexto = {
                "Mas variedad de objetos y enemigos mas lentos",
                "la experiencia estandar ideal para primera partida",
                "enemigos impredecibles\nescates de loot\ndificultad elevada en general"
            };
            string[] filasBoton = { "facil", "Normal", "Dificil" };
            float[] yFila = { -190f, -280f, -370f };

            for (int i = 0; i < 3; i++)
            {
                float y = yFila[i];

                var txtRt = GetOrAdoptRect(ladoDerecho, "TxtDif" + i, null);
                txtRt.anchorMin = new Vector2(0f, 1f);
                txtRt.anchorMax = new Vector2(1f, 1f);
                txtRt.pivot = new Vector2(0.5f, 1f);
                txtRt.anchoredPosition = new Vector2(-60f, y);
                txtRt.sizeDelta = new Vector2(260f, 70f);

                var txtTxt = GetOrAddText(txtRt);
                txtTxt.text = filasTexto[i];
                txtTxt.font = font;
                txtTxt.fontSize = 18;
                txtTxt.color = new Color(0.24f, 0.15f, 0.10f, 1f);
                txtTxt.alignment = TextAnchor.MiddleCenter;
                txtTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txtTxt.verticalOverflow = VerticalWrapMode.Overflow;
                txtTxt.raycastTarget = false;

                CrearBoton(ladoDerecho, "BtnDif" + i, filasBoton[i],
                    Folder + "botonChico.png", new Vector2(110f, y), new Vector2(120f, 40f), font, 16);
            }

            var notaRt = GetOrAdoptRect(ladoDerecho, "NotaDificultad", null);
            notaRt.anchorMin = new Vector2(0.5f, 1f);
            notaRt.anchorMax = new Vector2(0.5f, 1f);
            notaRt.pivot = new Vector2(0.5f, 1f);
            notaRt.anchoredPosition = new Vector2(0f, -470f);
            notaRt.sizeDelta = new Vector2(420f, 60f);

            var txtNota = GetOrAddText(notaRt);
            txtNota.text = "las estadísticas se guardarán separadas según la dificultad";
            txtNota.font = font;
            txtNota.fontSize = 16;
            txtNota.color = new Color(0.24f, 0.15f, 0.10f, 1f);
            txtNota.alignment = TextAnchor.MiddleCenter;
            txtNota.horizontalOverflow = HorizontalWrapMode.Overflow;
            txtNota.verticalOverflow = VerticalWrapMode.Overflow;
            txtNota.raycastTarget = false;
        }

        private static Text GetOrAddText(RectTransform rt)
        {
            if (rt == null)
            {
                Debug.LogError("[Menu Libro] GetOrAddText: RectTransform es null");
                return null;
            }
            var txt = rt.GetComponent<Text>();
            if (txt == null)
            {
                try
                {
                    txt = rt.gameObject.AddComponent<Text>();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Menu Libro] Error agregando Text a {rt.name}: {e.Message}");
                    return null;
                }
            }
            if (txt == null)
                Debug.LogError($"[Menu Libro] GetOrAddText: AddComponent<Text> devolvió null en {rt.name}");
            return txt;
        }

        private static GameObject CrearBoton(Transform parent, string name, string label, string spritePath,
            Vector2 anchoredPos, Vector2 size, Font font, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Menu Libro");

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            var sprite = LoadSprite(spritePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[Menu Libro] No se pudo cargar sprite {spritePath}, botón sin imagen.");
            }
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            c.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = c;

            var txtGo = new GameObject("Label", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            Undo.RegisterCreatedObjectUndo(txtGo, "Menu Libro");

            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            var txt = txtGo.AddComponent<Text>();
            txt.text = label;
            txt.font = font;
            txt.fontSize = fontSize;
            txt.color = new Color(0.24f, 0.15f, 0.10f, 1f);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;

            return go;
        }

        private static void WireController(RectTransform root, Image bookImage, RectTransform pagesRoot,
            BookMarker[] markers, GameObject[] pages)
        {
            var controller = root.GetComponent<BookMenuController>();
            if (controller == null) controller = Undo.AddComponent<BookMenuController>(root.gameObject);

            var so = new SerializedObject(controller);
            so.FindProperty("bookRoot").objectReferenceValue = root;
            so.FindProperty("bookGraphic").objectReferenceValue = bookImage;
            so.FindProperty("pagesRoot").objectReferenceValue = pagesRoot;
            so.FindProperty("startIndex").intValue = 0;

            var tabs = so.FindProperty("tabs");
            tabs.arraySize = Markers.Length;
            for (int i = 0; i < Markers.Length; i++)
            {
                var def = Markers[i];
                var tab = tabs.GetArrayElementAtIndex(i);
                tab.FindPropertyRelative("id").stringValue = def.id;
                tab.FindPropertyRelative("marker").objectReferenceValue = markers[i];
                tab.FindPropertyRelative("page").objectReferenceValue = pages[i];
                tab.FindPropertyRelative("action").enumValueIndex = (int)def.action;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyOrder(Transform book, RectTransform pagesRoot, BookMarker[] markers)
        {
            for (int i = 0; i < markers.Length; i++)
                markers[i].transform.SetSiblingIndex(i);

            book.SetSiblingIndex(markers.Length);
            pagesRoot.SetSiblingIndex(markers.Length + 1);
            markers[0].transform.SetAsLastSibling();
        }

        private static void HideLegacyUi()
        {
            foreach (var name in new[] { "MainPanel", "CreditsCanvas" })
            {
                foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (rt.name != name || !rt.gameObject.activeSelf) continue;

                    Undo.RecordObject(rt.gameObject, "Menu Libro");
                    rt.gameObject.SetActive(false);
                    Debug.Log($"[Menu Libro] Se ocultó '{name}' (menú viejo de botones). Para volverlo a ver, activá el GameObject en la Hierarchy.");
                }
            }
        }

        // ---------------------------------------------------------------- utilidades

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Image FindImageByTexture(string texturePath)
        {
            foreach (var image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (image.sprite == null) continue;
                if (AssetDatabase.GetAssetPath(image.sprite) == texturePath)
                    return image;
            }

            return null;
        }

        private static void EnsureSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[Menu Libro] No se encontró el archivo {path}");
                return;
            }

            bool alreadyOk = importer.textureType == TextureImporterType.Sprite &&
                             importer.spriteImportMode == SpriteImportMode.Single &&
                             importer.alphaIsTransparency &&
                             !importer.mipmapEnabled;
            if (alreadyOk) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogError($"[Menu Libro] No se pudo cargar el sprite {path} (¿está importado como Sprite?).");
            return sprite;
        }

        private static Font LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(Folder + "Always Classy.ttf");
            if (font == null)
                font = AssetDatabase.LoadAssetAtPath<Font>("Assets/fuentes/titulo/coldnightforalligators.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                Debug.LogError("[Menu Libro] No se encontró ninguna fuente. Usando fuente por defecto del sistema.");
                // Crear una fuente temporal si todo falla (no debería pasar)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return font;
        }
    }
}
