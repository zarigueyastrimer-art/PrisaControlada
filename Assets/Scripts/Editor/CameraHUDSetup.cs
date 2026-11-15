using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    // Tool: (1) elimina el efecto VHS (VHS_Canvas del prefab Player, que reproduce
    // un video en loop con chroma key) y (2) crea el overlay de "camara" en la
    // escena: las 4 esquinas, la bateria arriba a la derecha y el punto rojo REC
    // arriba a la izquierda con el texto "rec" al lado.
    //
    // La UI queda en la Jerarquia como GameObjects normales para retocarla a
    // mano; NO se genera nada en runtime y no hay scripts de logica.
    //
    // Idempotente: si el VHS ya no esta, no hace nada; si el overlay ya existe
    // se conserva tal cual (no pisa tus ajustes) y solo completa lo que falte.
    public static class CameraHUDSetup
    {
        private const string CanvasName = "CameraHUD_Canvas";
        private const string VhsCanvasName = "VHS_Canvas";
        private const string PlayerPrefabPath = "Assets/Prefabs/player/Player.prefab";

        private const string IconEsquinasPath = "Assets/Resources/HUD/4 esquinas.png";
        private const string IconBateriaPath = "Assets/Resources/HUD/Bateria.png";
        private const string IconPuntoPath = "Assets/Resources/HUD/Punto rojo rec.png";
        private const string FontPath = "Assets/Resources/HUD/DS-DIGIT.ttf";

        // Layout por defecto - valores a 1920x1080 de referencia: las posiciones
        // replican el arte original de los PNG (bateria arriba-derecha, punto
        // rojo arriba-izquierda).
        private const float BateriaRight = 89f;  // margen desde el borde derecho (1920-1831)
        private const float BateriaTop = 74f;    // margen desde arriba
        private const float RecLeft = 71f;        // margen izquierdo del punto rojo
        private const float RecTop = 70f;
        private const float RecGap = 14f;         // aire entre punto y texto
        private const int RecFontSize = 36;
        private const int SortingOrder = 350;    // mismo slot que el VHS_Canvas que reemplaza

        [MenuItem("PrisaControlada/Configurar CameraHUD")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Camera HUD", "Guarda la escena primero.", "OK");
                return;
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Camera HUD", "No se encontro el Player en la escena.", "OK");
                return;
            }

            Texture2D texEsquinas = AssetDatabase.LoadAssetAtPath<Texture2D>(IconEsquinasPath);
            Texture2D texBateria = AssetDatabase.LoadAssetAtPath<Texture2D>(IconBateriaPath);
            Texture2D texPunto = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPuntoPath);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            if (texEsquinas == null || texBateria == null || texPunto == null || font == null)
            {
                EditorUtility.DisplayDialog("Camera HUD",
                    "Faltan assets en Assets/Resources/HUD (4 esquinas, Bateria, Punto rojo rec o DS-DIGIT.ttf).", "OK");
                return;
            }

            // --- 1. Sacar el efecto VHS ---
            int removed = RemoveVhsFromPrefab();
            removed += RemoveVhsFromScene();

            // --- 2. Overlay de camara ---
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

            // --- Esquinas: marco completo a pantalla completa ---
            RectTransform esquinasRect = FindOrCreate(canvasRect, "Esquinas", out bool createdEsquinas);
            if (esquinasRect == null) return;

            if (createdEsquinas)
            {
                esquinasRect.anchorMin = Vector2.zero;
                esquinasRect.anchorMax = Vector2.one;
                esquinasRect.offsetMin = Vector2.zero;
                esquinasRect.offsetMax = Vector2.zero;
            }

            RawImage esquinas = esquinasRect.GetComponent<RawImage>();
            if (esquinas == null)
                esquinas = esquinasRect.gameObject.AddComponent<RawImage>();
            esquinas.texture = texEsquinas;
            esquinas.raycastTarget = false;

            // --- Bateria: arriba a la derecha (misma posicion que el arte) ---
            RectTransform bateriaRect = FindOrCreate(canvasRect, "Bateria", out bool createdBateria);
            if (bateriaRect == null) return;

            if (createdBateria)
            {
                bateriaRect.anchorMin = new Vector2(1f, 1f);
                bateriaRect.anchorMax = new Vector2(1f, 1f);
                bateriaRect.pivot = new Vector2(1f, 1f);
                bateriaRect.anchoredPosition = new Vector2(-BateriaRight, -BateriaTop);
                bateriaRect.sizeDelta = new Vector2(texBateria.width, texBateria.height);
            }

            RawImage bateria = bateriaRect.GetComponent<RawImage>();
            if (bateria == null)
                bateria = bateriaRect.gameObject.AddComponent<RawImage>();
            bateria.texture = texBateria;
            bateria.raycastTarget = false;

            // --- REC: punto rojo + texto "rec" arriba a la izquierda ---
            RectTransform recRect = FindOrCreate(canvasRect, "Rec", out bool createdRec);
            if (recRect == null) return;

            if (createdRec)
            {
                recRect.anchorMin = new Vector2(0f, 1f);
                recRect.anchorMax = new Vector2(0f, 1f);
                recRect.pivot = new Vector2(0f, 1f);
                recRect.anchoredPosition = new Vector2(RecLeft, -RecTop);
                recRect.sizeDelta = new Vector2(texPunto.width + RecGap + 100f, texPunto.height);
            }

            RectTransform puntoRect = FindOrCreate(recRect, "Punto", out bool createdPunto);
            if (puntoRect == null) return;

            if (createdPunto)
            {
                puntoRect.anchorMin = new Vector2(0f, 1f);
                puntoRect.anchorMax = new Vector2(0f, 1f);
                puntoRect.pivot = new Vector2(0f, 1f);
                puntoRect.anchoredPosition = Vector2.zero;
                puntoRect.sizeDelta = new Vector2(texPunto.width, texPunto.height);
            }

            RawImage punto = puntoRect.GetComponent<RawImage>();
            if (punto == null)
                punto = puntoRect.gameObject.AddComponent<RawImage>();
            punto.texture = texPunto;
            punto.raycastTarget = false;

            // Parpadeo del punto REC (solo logica; el componente vive en el punto).
            RecBlinker blinker = puntoRect.GetComponent<RecBlinker>();
            if (blinker == null)
                blinker = puntoRect.gameObject.AddComponent<RecBlinker>();

            SerializedObject soBlink = new SerializedObject(blinker);
            soBlink.FindProperty("punto").objectReferenceValue = punto;
            soBlink.ApplyModifiedProperties();

            RectTransform labelRect = FindOrCreate(recRect, "RecLabel", out bool createdLabel);
            if (labelRect == null) return;

            if (createdLabel)
            {
                labelRect.anchorMin = new Vector2(0f, 1f);
                labelRect.anchorMax = new Vector2(0f, 1f);
                labelRect.pivot = new Vector2(0f, 1f);
                labelRect.anchoredPosition = new Vector2(texPunto.width + RecGap, 0f);
                labelRect.sizeDelta = new Vector2(100f, texPunto.height);
            }

            Text label = labelRect.GetComponent<Text>();
            if (label == null)
                label = labelRect.gameObject.AddComponent<Text>();

            if (createdLabel)
            {
                label.font = font;
                label.fontSize = RecFontSize;
                label.alignment = TextAnchor.MiddleLeft;
                label.color = Color.white;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.text = "rec";
            }
            else if (label.font == null)
            {
                label.font = font;
            }
            label.raycastTarget = false;

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = canvasRect.gameObject;

            Debug.Log("[CameraHUD] VHS eliminado: " + removed + " objeto(s). Overlay creado bajo Player > " +
                      CanvasName + " (Esquinas/Bateria/Rec > Punto+RecLabel). Escena guardada.");
            EditorUtility.DisplayDialog("Camera HUD",
                (removed > 0
                    ? "Efecto VHS eliminado (" + removed + " objeto(s) VHS_Canvas).\n\n"
                    : "No se encontro VHS_Canvas (ya estaba eliminado).\n\n") +
                "Overlay creado en la Jerarquia (Player/" + CanvasName + ").\n\n" +
                "Se selecciono el canvas; podes mover bateria, punto REC y texto a mano.", "OK");
        }

        // ---------------------------------------------------------------
        // VHS: se reproduce como video en loop (VhsOverlayController) dentro
        // de VHS_Canvas, tanto en el prefab Player como en la escena.
        // ---------------------------------------------------------------
        private static int RemoveVhsFromPrefab()
        {
            if (!System.IO.File.Exists(PlayerPrefabPath)) return 0;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            int removed = 0;

            try
            {
                Transform vhs = FindDeep(root.transform, VhsCanvasName);
                if (vhs != null)
                {
                    Object.DestroyImmediate(vhs.gameObject);
                    removed++;
                }

                if (removed > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return removed;
        }

        private static int RemoveVhsFromScene()
        {
            int removed = 0;

            foreach (Transform t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || t.name != VhsCanvasName) continue;
                if (!t.gameObject.scene.IsValid()) continue;

                Undo.DestroyObjectImmediate(t.gameObject);
                removed++;
            }

            return removed;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;
            return null;
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
