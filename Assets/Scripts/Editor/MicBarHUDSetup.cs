using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    // Tool: reemplaza el canvas viejo del microfono (MicNoiseMeter_Canvas, una
    // barra horizontal con un Image "fill") por la barra vertical nueva
    // (MicBarHUD_Canvas): tres segmentos de barritas (blancas abajo, amarillas
    // en el medio, rojas arriba) que se muestran de a poco de abajo hacia arriba
    // segun el nivel de voz, usando el shader PrisaControlada/MicBarReveal, y el
    // icono del microfono debajo de la barra.
    //
    // La UI queda en la Jerarquia como GameObjects normales para retocarla a
    // mano; MicBarHUD solo calcula el relleno (logica, no crea UI).
    //
    // Idempotente: si la barra ya existe se conserva tal cual (no pisa tus
    // ajustes) y solo se completa lo que falte.
    public static class MicBarHUDSetup
    {
        private const string CanvasName = "MicBarHUD_Canvas";
        private const string OldCanvasName = "MicNoiseMeter_Canvas";
        private const string PlayerPrefabPath = "Assets/Prefabs/player/Player.prefab";

        private const string TexBlancasPath = "Assets/Resources/HUD/Mic barritas blancas.png";
        private const string TexAmarillasPath = "Assets/Resources/HUD/Mic barritas amarillas.png";
        private const string TexRojasPath = "Assets/Resources/HUD/Mic barritas rojas.png";
        private const string TexIconoPath = "Assets/Resources/HUD/Mic icono.png";
        private const string ShaderPath = "Assets/Resources/HUD/MicBarReveal.shader";
        private const string MaterialPath = "Assets/Resources/HUD/MicBarReveal.mat";
        private const string ShaderName = "PrisaControlada/MicBarReveal";

        // Orden entre HUDs: por encima de stamina (500) y de la camara (350),
        // por debajo de GameOver (600) y munición (610).
        private const int SortingOrder = 505;

        // Layout por defecto - replicado del arte original (lienzo 1920x1080,
        // origen abajo-izquierda y eje y hacia arriba):
        //   widget: izquierda 27, base 350, 47x379
        //   icono: 47x84 en la base del widget
        //   barra (Bar): x=5, y=94 dentro del widget, 37x285
        //   segmentos (coords locales de Bar): blancas y=0 h=141,
        //   amarillas y=144 h=93, rojas y=240 h=45
        private const float WidgetLeft = 27f;
        private const float WidgetBottom = 350f;
        private const float WidgetWidth = 47f;
        private const float WidgetHeight = 379f;

        private const float IconWidth = 47f;
        private const float IconHeight = 84f;

        private const float BarX = 5f;
        private const float BarY = 94f;
        private const float BarWidth = 37f;
        private const float BarHeight = 285f;

        private const float SegBlancasY = 0f;
        private const float SegBlancasH = 141f;
        private const float SegAmarillasY = 144f;
        private const float SegAmarillasH = 93f;
        private const float SegRojasY = 240f;
        private const float SegRojasH = 45f;

        [MenuItem("PrisaControlada/Configurar MicBarHUD")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Mic Bar HUD", "Guarda la escena primero.", "OK");
                return;
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Mic Bar HUD", "No se encontro el Player en la escena.", "OK");
                return;
            }

            Texture2D texBlancas = AssetDatabase.LoadAssetAtPath<Texture2D>(TexBlancasPath);
            Texture2D texAmarillas = AssetDatabase.LoadAssetAtPath<Texture2D>(TexAmarillasPath);
            Texture2D texRojas = AssetDatabase.LoadAssetAtPath<Texture2D>(TexRojasPath);
            Texture2D texIcono = AssetDatabase.LoadAssetAtPath<Texture2D>(TexIconoPath);

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                shader = Shader.Find(ShaderName);

            if (texBlancas == null || texAmarillas == null || texRojas == null ||
                texIcono == null || shader == null)
            {
                EditorUtility.DisplayDialog("Mic Bar HUD",
                    "Faltan assets en Assets/Resources/HUD (Mic barritas blancas/amarillas/rojas, " +
                    "Mic icono o el shader MicBarReveal.shader).", "OK");
                return;
            }

            // Material comun de los segmentos (el script hace una instancia por
            // segmento en Play porque UGUI no soporta MaterialPropertyBlock).
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                material.SetFloat("_Fill", 1f);
                AssetDatabase.CreateAsset(material, MaterialPath);
                AssetDatabase.SaveAssets();
            }

            // --- 1. Sacar el canvas viejo del microfono ---
            int removed = RemoveOldCanvasFromPrefab();
            removed += RemoveOldCanvasFromScene();

            // --- 2. Barra vertical nueva ---
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

            // --- MicBar: contenedor del widget (barra + icono) ---
            RectTransform micBar = FindOrCreate(canvasRect, "MicBar", out bool createdMicBar);
            if (micBar == null) return;

            if (createdMicBar)
            {
                micBar.anchorMin = Vector2.zero;
                micBar.anchorMax = Vector2.zero;
                micBar.pivot = new Vector2(0f, 0f);
                micBar.anchoredPosition = new Vector2(WidgetLeft, WidgetBottom);
                micBar.sizeDelta = new Vector2(WidgetWidth, WidgetHeight);
            }

            // --- Icono del microfono (debajo de la barra) ---
            RectTransform iconRect = FindOrCreate(micBar, "Icono", out bool createdIcon);
            if (iconRect == null) return;

            if (createdIcon)
            {
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.zero;
                iconRect.pivot = new Vector2(0f, 0f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(IconWidth, IconHeight);
            }

            RawImage icono = iconRect.GetComponent<RawImage>();
            if (icono == null)
                icono = iconRect.gameObject.AddComponent<RawImage>();
            icono.texture = texIcono;
            icono.raycastTarget = false;

            // --- Bar: contenedor de los tres segmentos ---
            RectTransform barRect = FindOrCreate(micBar, "Bar", out bool createdBar);
            if (barRect == null) return;

            if (createdBar)
            {
                barRect.anchorMin = Vector2.zero;
                barRect.anchorMax = Vector2.zero;
                barRect.pivot = new Vector2(0f, 0f);
                barRect.anchoredPosition = new Vector2(BarX, BarY);
                barRect.sizeDelta = new Vector2(BarWidth, BarHeight);
            }

            RawImage segBlancas = CreateSegment(barRect, "Blancas", texBlancas, material,
                SegBlancasY, SegBlancasH);
            RawImage segAmarillas = CreateSegment(barRect, "Amarillas", texAmarillas, material,
                SegAmarillasY, SegAmarillasH);
            RawImage segRojas = CreateSegment(barRect, "Rojas", texRojas, material,
                SegRojasY, SegRojasH);

            if (segBlancas == null || segAmarillas == null || segRojas == null)
                return;

            // --- 3. MicBarHUD: logica del relleno ---
            MicNoiseMeter mic = player.GetComponent<MicNoiseMeter>();
            if (mic == null)
                mic = player.GetComponentInChildren<MicNoiseMeter>(true);

            MicBarHUD hud = canvasRect.GetComponent<MicBarHUD>();
            if (hud == null)
                hud = canvasRect.gameObject.AddComponent<MicBarHUD>();

            SerializedObject soHud = new SerializedObject(hud);
            soHud.FindProperty("bar").objectReferenceValue = barRect;
            SerializedProperty segsProp = soHud.FindProperty("segments");
            segsProp.arraySize = 3;
            segsProp.GetArrayElementAtIndex(0).objectReferenceValue = segBlancas;
            segsProp.GetArrayElementAtIndex(1).objectReferenceValue = segAmarillas;
            segsProp.GetArrayElementAtIndex(2).objectReferenceValue = segRojas;
            soHud.FindProperty("mic").objectReferenceValue = mic;
            soHud.ApplyModifiedProperties();

            // --- 4. Reapuntar MicNoiseMeter al canvas nuevo ---
            // (el canvas viejo quedo destruido: ahora oculta/muestra esta barra
            // con la tecla M y ya no queda ningun "fill" conectado)
            if (mic != null)
            {
                SerializedObject soMic = new SerializedObject(mic);
                soMic.FindProperty("canvas").objectReferenceValue = canvas;
                SerializedProperty fillProp = soMic.FindProperty("fill");
                if (fillProp != null)
                    fillProp.objectReferenceValue = null;
                soMic.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = canvasRect.gameObject;

            Debug.Log("[MicBarHUD] Canvas viejo eliminado: " + removed + " objeto(s). Barra vertical " +
                      "creada bajo Player > " + CanvasName + " > MicBar (Icono + Bar > Blancas/Amarillas/Rojas). " +
                      "Escena guardada.");
            EditorUtility.DisplayDialog("Mic Bar HUD",
                (removed > 0
                    ? "Canvas viejo eliminado (" + removed + " objeto(s) " + OldCanvasName + ").\n\n"
                    : "No se encontro " + OldCanvasName + " (ya estaba eliminado).\n\n") +
                "Barra vertical creada en la Jerarquia (Player/" + CanvasName + ").\n\n" +
                "Se selecciono el canvas: podes moverlo a mano. En Play las barritas " +
                "se llenan de abajo hacia arriba segun el volumen de voz.", "OK");
        }

        // ---------------------------------------------------------------
        // Segmentos: abajo-izquierda como pivot, altura fija, mismo material
        // base (el script instancia uno por segmento en Play).
        // ---------------------------------------------------------------
        private static RawImage CreateSegment(Transform parent, string name, Texture2D texture,
            Material material, float y, float height)
        {
            RectTransform rect = FindOrCreate(parent, name, out bool created);
            if (rect == null) return null;

            if (created)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(0f, y);
                rect.sizeDelta = new Vector2(BarWidth, height);
            }

            RawImage img = rect.GetComponent<RawImage>();
            if (img == null)
                img = rect.gameObject.AddComponent<RawImage>();
            img.texture = texture;
            img.material = material;
            img.raycastTarget = false;
            return img;
        }

        // ---------------------------------------------------------------
        // Canvas viejo: barra horizontal del micfono que vivia dentro del
        // prefab Player (ya sin su relleno; ahora solo queda su estructura).
        // ---------------------------------------------------------------
        private static int RemoveOldCanvasFromPrefab()
        {
            if (!System.IO.File.Exists(PlayerPrefabPath)) return 0;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            int removed = 0;

            try
            {
                Transform oldCanvas = FindDeep(root.transform, OldCanvasName);
                if (oldCanvas != null)
                {
                    Object.DestroyImmediate(oldCanvas.gameObject);
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

        private static int RemoveOldCanvasFromScene()
        {
            int removed = 0;

            foreach (Transform t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || t.name != OldCanvasName) continue;
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
