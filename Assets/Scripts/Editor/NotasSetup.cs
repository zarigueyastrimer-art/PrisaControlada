using System.Collections.Generic;
using PrisaControlada.Core;
using PrisaControlada.Items;
using PrisaControlada.Menu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    //TP2 - Matías Morilla
    // Setup del sistema de notas + dificultad. Es idempotente: si ya está
    // armado no duplica nada. Corre una vez por escena (escena activa y
    // guardada), con Tools > PrisaControlada > Notas: Setup:
    //  - siempre crea el catálogo en Resources con 3 notas de ejemplo si falta;
    //  - escena de juego: NotaLore en los "NotaLore", NotaLoreViewer en el
    //    "NotaLoreCanvas" y el objeto "ResetNotas" (tecla P);
    //  - menú: la lista de ??? + popup en PaginaHistoria/Derecha, los récords
    //    por dificultad en PaginaHistoria/Izquierda, el selector de
    //    dificultad en PaginaJugado y también "ResetNotas".
    // No guarda la escena: hay que Ctrl+S después.
    public static class NotasSetup
    {
        private const string CatalogoPath = "Assets/Resources/NotasCatalogo.asset";
        private const string MenuPath = "Tools/PrisaControlada/Notas: Setup (catálogo + escena activa)";
        private const string BotonSpritePath = "Assets/Scenes/menu/botonChico.png";

        // Mismo orden de prioridad que el NoteViewer (900): por encima del HUD.
        private const int OrdenVisor = 900;

        [MenuItem(MenuPath, false, 320)]
        public static void Setup()
        {
            NotasCatalogo catalogo = AsegurarCatalogo();

            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Notas", "Guarda la escena antes de hacer el setup.", "OK");
                return;
            }

            int cambios = 0;
            cambios += ConfigurarPickups(catalogo);
            cambios += ConfigurarVisor();
            cambios += ConfigurarPaginaHistoria(catalogo);
            cambios += ConfigurarHistorial();
            cambios += ConfigurarSelector();
            cambios += ConfigurarReset();

            if (cambios > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[Notas] Setup listo en '{scene.name}': {cambios} cambio(s). " +
                          "Guardá la escena (Ctrl+S).");
            }
            else
            {
                Debug.Log($"[Notas] '{scene.name}' ya estaba armada: nada que cambiar.");
            }
        }

        // ---- Catálogo ----

        // Crea el asset con 3 notas de ejemplo si todavía no existe.
        private static NotasCatalogo AsegurarCatalogo()
        {
            NotasCatalogo catalogo = AssetDatabase.LoadAssetAtPath<NotasCatalogo>(CatalogoPath);
            if (catalogo != null) return catalogo;

            catalogo = ScriptableObject.CreateInstance<NotasCatalogo>();

            // Nota 1: la de prueba que ya estaba en la escena.
            catalogo.notas.Add(new NotasCatalogo.Nota
            {
                id = "nota_prototipo",
                titulo = "Nota prototipo",
                texto = "esta es solo una nota de testeo porque se rompio mi pc y tube que arreglar\n" +
                        "las cosas que faltaban en 2 dias y no me da el tiempo perdon profe jejejejej\n\n" +
                        "jjejejejejjejejejjejejejejjejejejjejejejjejejjejeeeaneiuand\n" +
                        "isafneifnasifonatwbnuasdif u8vn fsfnae9fvo,auifjnsdgiundsignsmFGISFIAOMPFVW9GNSD\n" +
                        "MSOFQUWNFIOSNDFPAFAIN"
            });

            // Nota 2 y 3: lore de ejemplo (reemplazalas por las tuyas).
            catalogo.notas.Add(new NotasCatalogo.Nota
            {
                id = "nota_guardia",
                titulo = "Turno del guardia",
                texto = "Turno 14. El zumbido volvio a sonar a las 3 otra vez.\n" +
                        "Nadie revisa el ala este desde el incidente.\n" +
                        "Si estas leyendo esto, no abras la puerta verde."
            });

            catalogo.notas.Add(new NotasCatalogo.Nota
            {
                id = "nota_recado",
                titulo = "Recado sin firmar",
                texto = "Me lleve lo que quedaba del cajon de arriba.\n" +
                        "Si llegas tarde, buscalo bajo la mesa.\n" +
                        "Y no esperes a que vuelva a sonar el zumbido."
            });

            AssetDatabase.CreateAsset(catalogo, CatalogoPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Notas] Catálogo creado en {CatalogoPath}. " +
                      "Abrilo para agregar notas en masa (botón + de la lista).");

            return catalogo;
        }

        // ---- Escena de juego ----

        // Agrega NotaLore a los objetos "NotaLore" (y variantes "NotaLore (1)")
        // que todavía no lo tengan, y les asigna el catálogo.
        private static int ConfigurarPickups(NotasCatalogo catalogo)
        {
            int n = 0;

            foreach (Transform t in TodosLosTransforms())
            {
                if (!EsObjetoNotaLore(t.name)) continue;

                NotaLore nota = t.GetComponent<NotaLore>();
                if (nota == null)
                {
                    nota = Undo.AddComponent<NotaLore>(t.gameObject);
                    n++;
                }

                SerializedObject so = new SerializedObject(nota);
                SerializedProperty prop = so.FindProperty("catalogo");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = catalogo;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    n++;
                }
            }

            return n;
        }

        // "NotaLore" exacto o duplicados ("NotaLore (1)"), pero NUNCA
        // "NotaLoreCanvas" (ese es la UI, no el pickup).
        private static bool EsObjetoNotaLore(string nombre)
        {
            return nombre == "NotaLore" || nombre.StartsWith("NotaLore (");
        }

        // Agrega NotaLoreViewer al "NotaLoreCanvas" y le sube el sortingOrder
        // del canvas para que el visor quede por encima del HUD.
        private static int ConfigurarVisor()
        {
            int n = 0;

            foreach (Transform t in TodosLosTransforms())
            {
                if (t.name != "NotaLoreCanvas") continue;

                if (t.GetComponent<NotaLoreViewer>() == null)
                {
                    Undo.AddComponent<NotaLoreViewer>(t.gameObject);
                    n++;
                }

                Canvas canvas = t.GetComponentInChildren<Canvas>(true);
                if (canvas != null && canvas.sortingOrder < OrdenVisor)
                {
                    canvas.sortingOrder = OrdenVisor;
                    n++;
                }
            }

            return n;
        }

        // ---- Menú: lista de notas ??? + popup de lectura ----

        private static int ConfigurarPaginaHistoria(NotasCatalogo catalogo)
        {
            Transform pagina = BuscarPorNombre("PaginaHistoria");
            if (pagina == null) return 0;

            Transform derecha = pagina.Find("Derecha");
            if (derecha == null)
            {
                Debug.LogWarning("[Notas] Encontré PaginaHistoria pero no su hijo 'Derecha'.");
                return 0;
            }

            int n = 0;

            // 1) Borrar el diseño viejo ("NotasTexto": una sola lista de texto).
            Transform viejo = derecha.Find("NotasTexto");
            if (viejo != null && viejo.GetComponent<NotasMenu>() != null)
            {
                Undo.DestroyObjectImmediate(viejo.gameObject);
                n++;
            }

            // 2) Contenedor de las filas (una por nota del catálogo).
            Transform lista = derecha.Find("NotasLista");
            if (lista == null)
            {
                GameObject go = new GameObject("NotasLista", typeof(RectTransform));
                go.transform.SetParent(derecha, false);

                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(20f, 20f);
                rt.offsetMax = new Vector2(-20f, -20f);

                VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(4, 4, 4, 4);
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                // Si hay muchas notas, lo que no entre en la página se recorta.
                go.AddComponent<RectMask2D>();

                Undo.RegisterCreatedObjectUndo(go, "Notas Setup");

                lista = go.transform;
                n++;
            }

            // 3) Popup de lectura en el canvas raíz (por encima del libro).
            Canvas canvasRaiz = derecha.GetComponentInParent<Canvas>();
            Transform padrePopup = canvasRaiz != null ? canvasRaiz.transform : derecha.root;
            Transform popup = padrePopup.Find("PopupNota");
            TextMeshProUGUI popupTexto = null;
            CanvasGroup popupGroup = null;

            if (popup == null)
            {
                GameObject popupGo = new GameObject("PopupNota", typeof(RectTransform));
                popupGo.transform.SetParent(padrePopup, false);
                popupGo.transform.SetAsLastSibling();

                RectTransform pr = popupGo.GetComponent<RectTransform>();
                pr.anchorMin = Vector2.zero;
                pr.anchorMax = Vector2.one;
                pr.sizeDelta = Vector2.zero;

                // Fondo oscuro: da contraste y absorbe los clicks
                // (al tocarlo se cierra el popup).
                GameObject fondo = new GameObject("Fondo", typeof(RectTransform));
                fondo.transform.SetParent(popupGo.transform, false);
                RectTransform fr = fondo.GetComponent<RectTransform>();
                fr.anchorMin = Vector2.zero;
                fr.anchorMax = Vector2.one;
                fr.sizeDelta = Vector2.zero;
                Image img = fondo.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.88f);

                // Texto de la nota (título + cuerpo: lo pinta NotasMenu).
                GameObject txtGo = new GameObject("Texto", typeof(RectTransform));
                txtGo.transform.SetParent(popupGo.transform, false);
                RectTransform tr = txtGo.GetComponent<RectTransform>();
                tr.anchorMin = Vector2.zero;
                tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(180f, 110f);
                tr.offsetMax = new Vector2(-180f, -110f);

                popupTexto = txtGo.AddComponent<TextMeshProUGUI>();
                TextMeshProUGUI estilo = BuscarEstilo(pagina);
                if (estilo != null)
                    popupTexto.font = estilo.font;
                popupTexto.fontSize = 20f;
                popupTexto.color = Color.white;
                popupTexto.alignment = TextAlignmentOptions.Top;
                popupTexto.raycastTarget = false;

                popupGroup = popupGo.AddComponent<CanvasGroup>();
                popupGroup.alpha = 0f;
                popupGroup.interactable = false;
                popupGroup.blocksRaycasts = false;

                Undo.RegisterCreatedObjectUndo(popupGo, "Notas Setup");

                n++;
            }
            else
            {
                popupGroup = popup.GetComponent<CanvasGroup>();
                popupTexto = Buscar<TextMeshProUGUI>(popup, "Texto");

                if (popupGroup == null)
                {
                    popupGroup = popup.gameObject.AddComponent<CanvasGroup>();
                    popupGroup.alpha = 0f;
                    popupGroup.interactable = false;
                    popupGroup.blocksRaycasts = false;
                    n++;
                }
            }

            // 4) Componente NotasMenu con todas las referencias.
            NotasMenu menu = lista.GetComponent<NotasMenu>();
            if (menu == null)
            {
                menu = Undo.AddComponent<NotasMenu>(lista.gameObject);
                n++;
            }

            SerializedObject so = new SerializedObject(menu);
            n += Asignar(so, "lista", lista);
            n += Asignar(so, "popupTexto", popupTexto);
            n += Asignar(so, "popupGroup", popupGroup);
            n += Asignar(so, "estilo", BuscarEstilo(pagina));
            n += Asignar(so, "spriteBoton", AssetDatabase.LoadAssetAtPath<Sprite>(BotonSpritePath));
            n += Asignar(so, "catalogo", catalogo);

            return n;
        }

        // ---- Menú: récords por dificultad (página historia, izquierda) ----

        private static int ConfigurarHistorial()
        {
            Transform pagina = BuscarPorNombre("PaginaHistoria");
            if (pagina == null) return 0;

            Transform izquierda = pagina.Find("Izquierda");
            if (izquierda == null) return 0;

            int n = 0;

            HistorialDistancia hist = izquierda.GetComponent<HistorialDistancia>();
            if (hist == null)
            {
                hist = Undo.AddComponent<HistorialDistancia>(izquierda.gameObject);
                n++;
            }

            SerializedObject so = new SerializedObject(hist);
            n += Asignar(so, "facil", Buscar<TextMeshProUGUI>(izquierda, "Distancia Facil"));
            n += Asignar(so, "estandar", Buscar<TextMeshProUGUI>(izquierda, "Distancia Estandar"));
            n += Asignar(so, "dificil", Buscar<TextMeshProUGUI>(izquierda, "Distancia Dificil"));

            return n;
        }

        // ---- Menú: botones de dificultad (página jugar) ----

        private static int ConfigurarSelector()
        {
            Transform jugado = BuscarPorNombre("PaginaJugado");
            if (jugado == null) return 0;

            int n = 0;

            SelectorDificultad sel = jugado.GetComponent<SelectorDificultad>();
            if (sel == null)
            {
                sel = Undo.AddComponent<SelectorDificultad>(jugado.gameObject);
                n++;
            }

            Button btn0 = Buscar<Button>(jugado, "BtnDif0");
            Button btn1 = Buscar<Button>(jugado, "BtnDif1");
            Button btn2 = Buscar<Button>(jugado, "BtnDif2");

            if (btn0 == null || btn1 == null || btn2 == null)
            {
                Debug.LogWarning("[Notas] No encontré BtnDif0/1/2 en PaginaJugado.");
                return n;
            }

            SerializedObject so = new SerializedObject(sel);
            SerializedProperty arr = so.FindProperty("botones");
            if (arr != null && (arr.arraySize < 3 || arr.GetArrayElementAtIndex(0).objectReferenceValue == null))
            {
                arr.arraySize = 3;
                arr.GetArrayElementAtIndex(0).objectReferenceValue = btn0;
                arr.GetArrayElementAtIndex(1).objectReferenceValue = btn1;
                arr.GetArrayElementAtIndex(2).objectReferenceValue = btn2;
                so.ApplyModifiedPropertiesWithoutUndo();
                n++;
            }

            return n;
        }

        // ---- Las dos escenas: objeto de la tecla P ----

        private static int ConfigurarReset()
        {
            if (BuscarPorNombre("ResetNotas") != null) return 0;

            GameObject go = new GameObject("ResetNotas");
            go.AddComponent<ResetNotasInput>();
            Undo.RegisterCreatedObjectUndo(go, "Notas Setup");
            return 1;
        }

        // ---- utilidades ----

        // Asigna una referencia al inspector solo si está vacía.
        // Devuelve 1 si cambió algo (para contar cambios del setup).
        private static int Asignar(SerializedObject so, string campo, Object valor)
        {
            SerializedProperty prop = so.FindProperty(campo);
            if (prop == null || valor == null) return 0;
            if (prop.objectReferenceValue != null) return 0;

            prop.objectReferenceValue = valor;
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        private static Transform BuscarPorNombre(string nombre)
        {
            foreach (Transform t in TodosLosTransforms())
            {
                if (t.name == nombre) return t;
            }

            return null;
        }

        // Busca un componente de tipo T en un GO con ese nombre, dentro de raiz.
        private static T Buscar<T>(Transform raiz, string nombre) where T : Component
        {
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == nombre)
                    return t.GetComponent<T>();
            }

            return null;
        }

        // Primer TMP de la página izquierda: sirve de estilo (fuente/color)
        // para todo lo que se cree del lado derecho y para el popup.
        private static TextMeshProUGUI BuscarEstilo(Transform pagina)
        {
            Transform izquierda = pagina.Find("Izquierda");
            return izquierda != null ? izquierda.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        }

        private static IEnumerable<Transform> TodosLosTransforms()
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }
}
