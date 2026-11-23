using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    //TP2 - Matías Morilla
    // Tool: crea la jerarquia REAL del temporizador (GameTimerHUD) en la
    // escena abierta y cablea las referencias del componente.
    // La UI queda en la Jerarquia como GameObjects normales para retocarla a
    // mano (tamano, posicion, color); NO se genera nada en runtime.
    //
    // Idempotente: si el canvas/texto ya existen se conservan tal cual (no pisa
    // los ajustes manuales) y solo se completan las piezas que falten.
    public static class GameTimerHUDSetup
    {
        private const string CanvasName = "GameTimerHUD_Canvas";
        private const string TimerTextName = "TimerText";
        private const string FontPath = "Assets/Resources/HUD/DS-DIGIT.ttf";

        // Layout por defecto (valores a 1920x1080 de referencia).
        private const float TimerWidth = 420f;
        private const float TimerHeight = 70f;
        private const float TopMargin = 40f;
        private const int TimerFontSize = 48;

        // Por encima de la camara (350), pero por debajo de los prompts y
        // overlays de codigo (400+), muerte (600), etc.
        private const int SortingOrder = 360;

        [MenuItem("PrisaControlada/Configurar GameTimerHUD")]
        public static void Setup()
        {
            SetupInternal(true);
        }

        // Permite ejecutar la configuracion sin abrir el dialogo (por ejemplo,
        // desde una herramienta automatica del Editor).
        internal static bool SetupInternal(bool showDialog)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                return Fail("Game Timer HUD", "Guarda la escena primero.", showDialog);
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                return Fail("Game Timer HUD", "No se encontro el Player en la escena.", showDialog);
            }

            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                return Fail("Game Timer HUD",
                    "Falta la fuente en Assets/Resources/HUD/DS-DIGIT.ttf.", showDialog);
            }

            // --- Canvas (ScreenSpaceOverlay 1920x1080) ---
            RectTransform canvasRect = FindOrCreate(player.transform, CanvasName, out bool createdCanvas);
            if (canvasRect == null)
                return Fail("Game Timer HUD", "No se pudo crear el canvas.", showDialog);

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

            // --- Texto: centro superior ---
            RectTransform timerRect = FindOrCreate(canvasRect, TimerTextName, out bool createdTimer);
            if (timerRect == null)
                return Fail("Game Timer HUD", "No se pudo crear el texto del temporizador.", showDialog);

            if (createdTimer)
            {
                timerRect.anchorMin = new Vector2(0.5f, 1f);
                timerRect.anchorMax = new Vector2(0.5f, 1f);
                timerRect.pivot = new Vector2(0.5f, 1f);
                timerRect.anchoredPosition = new Vector2(0f, -TopMargin);
                timerRect.sizeDelta = new Vector2(TimerWidth, TimerHeight);
            }

            Text timerText = timerRect.GetComponent<Text>();
            bool createdText = false;
            if (timerText == null)
            {
                timerText = timerRect.gameObject.AddComponent<Text>();
                createdText = true;
            }

            if (createdTimer || createdText)
            {
                timerText.font = font;
                timerText.fontSize = TimerFontSize;
                timerText.alignment = TextAnchor.MiddleCenter;
                timerText.color = Color.white;
                timerText.horizontalOverflow = HorizontalWrapMode.Overflow;
                timerText.verticalOverflow = VerticalWrapMode.Overflow;
            }
            else if (timerText.font == null)
            {
                timerText.font = font;
            }

            timerText.text = "00:00:00";
            timerText.raycastTarget = false;

            // --- Logica: GameTimerHUD ---
            GameTimerHUD hud = canvasRect.GetComponent<GameTimerHUD>();
            if (hud == null)
                hud = canvasRect.gameObject.AddComponent<GameTimerHUD>();

            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("timerText").objectReferenceValue = timerText;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(canvasRect.gameObject);
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = canvasRect.gameObject;

            Debug.Log("[GameTimerHUD] Temporizador creado en la Jerarquia bajo Player > " +
                      CanvasName + " (" + TimerTextName + "). Referencias cableadas y escena guardada.");

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Game Timer HUD",
                    "Temporizador creado en la Jerarquia (Player/" + CanvasName + ").\n\n" +
                    "Se selecciono el texto; podes moverlo o cambiarle el tamano a mano.\n" +
                    "En Play: muestra MM:SS:MS y reinicia al llegar a 99:59:99.", "OK");
            }

            return true;
        }

        private static bool Fail(string title, string message, bool showDialog)
        {
            Debug.LogError("[" + title + "] " + message);
            if (showDialog)
                EditorUtility.DisplayDialog(title, message, "OK");
            return false;
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
