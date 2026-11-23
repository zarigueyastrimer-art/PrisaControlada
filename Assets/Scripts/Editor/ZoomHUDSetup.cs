using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    //TP2 - Matías Morilla
    // Tool: crea la barra de zoom en el borde derecho de la pantalla:
    //   Player > ZoomHUD_Canvas > ZoomBar (arte "Zoom barra.png")
    //                      > Pista (recorrido del marcador)
    //                        > Marcador (arte "Zoom marcador.png")
    //
    // La barra es estatica; lo unico que se mueve es el marcador (lo hace
    // ZoomBarHUD, que mide el zoom por el FOV de la camara). Ademas, si no se
    // usa la mecanica de zoom durante un rato, la barra desaparece sola.
    //
    // La UI queda en la Jerarquia para retocarla a mano; no se crea nada en
    // runtime. Idempotente: si ya existe se conserva (no pisa tus ajustes) y
    // solo se completa lo que falte.
    public static class ZoomHUDSetup
    {
        private const string CanvasName = "ZoomHUD_Canvas";
        private const string BarName = "ZoomBar";
        private const string TrackName = "Pista";
        private const string MarkerName = "Marcador";

        private const string TexBarraPath = "Assets/Resources/HUD/Zoom barra.png";
        private const string TexMarcadorPath = "Assets/Resources/HUD/Zoom marcador.png";

        // Orden entre HUDs: por encima de la camara (350), stamina (500) y
        // micro (505); por debajo de GameOver (600) y municion (610).
        private const int SortingOrder = 506;

        // Layout por defecto - replicado del arte original (lienzo 1920x1080):
        //   widget: x[1790-1894] y[288-785] -> 105x498, margen derecho 25
        //   pista (franja rayada): x[1857-1894] y[344-736] -> dentro del widget
        //   marcador: 27x25, centrado en la pista
        private const float WidgetWidth = 105f;
        private const float WidgetHeight = 498f;
        private const float WidgetRight = 25f;      // margen desde el borde derecho
        private const float WidgetVertical = 3f;    // el arte esta 3px arriba del centro

        private const float TrackX = 67f;           // x local de la pista en el widget
        private const float TrackY = 49f;           // y local de la base de la pista
        private const float TrackWidth = 38f;
        private const float TrackHeight = 393f;

        private const float MarkerWidth = 27f;
        private const float MarkerHeight = 25f;

        [MenuItem("PrisaControlada/Configurar ZoomHUD")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Zoom HUD", "Guarda la escena primero.", "OK");
                return;
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Zoom HUD", "No se encontro el Player en la escena.", "OK");
                return;
            }

            Texture2D texBarra = LoadTexture(TexBarraPath);
            Texture2D texMarcador = LoadTexture(TexMarcadorPath);

            if (texBarra == null || texMarcador == null)
            {
                EditorUtility.DisplayDialog("Zoom HUD",
                    "Faltan los assets en Assets/Resources/HUD (Zoom barra.png / Zoom marcador.png).", "OK");
                return;
            }

            // --- Canvas ---
            RectTransform canvasRect = FindOrCreate(player.transform, CanvasName, out bool createdCanvas);
            if (canvasRect == null) return;

            if (createdCanvas)
            {
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.offsetMin = Vector2.zero;
                canvasRect.offsetMax = Vector2.zero;
            }

            Canvas canvas = canvasRect.GetComponent<Canvas>();
            if (canvas == null)
                canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = canvasRect.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // --- ZoomBar: el widget con el arte de la barra (fijo) ---
            RectTransform barRect = FindOrCreate(canvasRect, BarName, out bool createdBar);
            if (barRect == null) return;

            if (createdBar)
            {
                barRect.anchorMin = new Vector2(1f, 0.5f);
                barRect.anchorMax = new Vector2(1f, 0.5f);
                barRect.pivot = new Vector2(1f, 0.5f);
                barRect.anchoredPosition = new Vector2(-WidgetRight, WidgetVertical);
                barRect.sizeDelta = new Vector2(WidgetWidth, WidgetHeight);
            }

            RawImage barra = barRect.GetComponent<RawImage>();
            if (barra == null)
                barra = barRect.gameObject.AddComponent<RawImage>();
            barra.texture = texBarra;
            barra.raycastTarget = false;

            // CanvasGroup: se usa para el fade in/out de la barra.
            CanvasGroup grupo = barRect.GetComponent<CanvasGroup>();
            if (grupo == null)
                grupo = barRect.gameObject.AddComponent<CanvasGroup>();
            grupo.interactable = false;
            grupo.blocksRaycasts = false;

            // --- Pista: recorrido vertical del marcador (sin grafico) ---
            RectTransform trackRect = FindOrCreate(barRect, TrackName, out bool createdTrack);
            if (trackRect == null) return;

            if (createdTrack)
            {
                trackRect.anchorMin = Vector2.zero;
                trackRect.anchorMax = Vector2.zero;
                trackRect.pivot = Vector2.zero;
                trackRect.anchoredPosition = new Vector2(TrackX, TrackY);
                trackRect.sizeDelta = new Vector2(TrackWidth, TrackHeight);
            }

            // --- Marcador: lo unico que se mueve ---
            RectTransform markerRect = FindOrCreate(trackRect, MarkerName, out bool createdMarker);
            if (markerRect == null) return;

            if (createdMarker)
            {
                markerRect.anchorMin = new Vector2(0.5f, 0f);
                markerRect.anchorMax = new Vector2(0.5f, 0f);
                markerRect.pivot = new Vector2(0.5f, 0f);
                markerRect.anchoredPosition = Vector2.zero;
                markerRect.sizeDelta = new Vector2(MarkerWidth, MarkerHeight);
            }

            RawImage marcador = markerRect.GetComponent<RawImage>();
            if (marcador == null)
                marcador = markerRect.gameObject.AddComponent<RawImage>();
            marcador.texture = texMarcador;
            marcador.raycastTarget = false;

            // --- Logica: ZoomBarHUD ---
            // El zoom es solo el click derecho sin arma: lo expone PlayerCamera.
            PlayerCamera playerCam = player.GetComponentInChildren<PlayerCamera>(true);
            if (playerCam == null)
                playerCam = Object.FindFirstObjectByType<PlayerCamera>();

            ZoomBarHUD hud = canvasRect.GetComponent<ZoomBarHUD>();
            bool createdHud = false;
            if (hud == null)
            {
                hud = canvasRect.gameObject.AddComponent<ZoomBarHUD>();
                createdHud = true;
            }

            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("barra").objectReferenceValue = barRect;
            so.FindProperty("pista").objectReferenceValue = trackRect;
            so.FindProperty("marcador").objectReferenceValue = markerRect;
            so.FindProperty("grupo").objectReferenceValue = grupo;
            if (playerCam != null)
                so.FindProperty("playerCamera").objectReferenceValue = playerCam;

            // "recorrido" = hasta cuanto sube el marcador (1 = tope de la barra).
            // Solo se setea al crear el componente; si ya existe se respeta el
            // valor que haya ajustado a mano.
            if (createdHud)
            {
                SerializedProperty propRecorrido = so.FindProperty("recorrido");
                if (propRecorrido != null)
                    propRecorrido.floatValue = 1f;
            }

            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = canvasRect.gameObject;

            Debug.Log("[ZoomHUD] Barra de zoom creada en " + CanvasName +
                      " (derecha de la pantalla). El marcador sube con el zoom y la barra " +
                      "desaparece si no se usa la mecanica durante un rato.");
            EditorUtility.DisplayDialog("Zoom HUD",
                "Barra de zoom creada en la Jerarquia (Player/" + CanvasName + ").\n\n" +
                "La barra esta a la derecha; el marcador sube de abajo hacia arriba segun " +
                "el zoom y la barra desaparece si no zoomedas por un rato.\n\n" +
                "Se selecciono el canvas por si queres moverla a mano.", "OK");
        }

        // Carga una textura; si AssetDatabase aun no la tiene, fuerza la
        // importacion y reintenta antes de rendirse.
        private static Texture2D LoadTexture(string path)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (tex == null)
                Debug.LogError("[ZoomHUD] No se pudo cargar la textura: " + path);

            return tex;
        }

        private static RectTransform FindOrCreate(Transform parent, string name, out bool created)
        {
            created = false;

            Transform existing = parent.Find(name);
            if (existing != null)
            {
                // Un objeto con ese nombre pero sin RectTransform no sirve: se reemplaza.
                RectTransform asRect = existing as RectTransform;
                if (asRect != null)
                    return asRect;

                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            created = true;
            return go.GetComponent<RectTransform>();
        }
    }
}
