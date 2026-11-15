using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    // Tool: crea la jerarquia REAL del icono de linterna (FlashlightHUD) en la
    // escena abierta (Prototype Old) y cablea las referencias del componente.
    // La UI queda en la Jerarquia como GameObjects normales para retocarla a
    // mano (tamaño, posicion, color); NO se genera nada en runtime.
    //
    // Idempotente: si el canvas/icono ya existen se conservan tal cual (no pisa
    // tus ajustes) y solo se completan las piezas que falten + referencias.
    public static class FlashlightHUDSetup
    {
        private const string CanvasName = "FlashlightHUD_Canvas";

        private const string IconLinternaPath = "Assets/Resources/HUD/Linterna.png";

        // Layout por defecto (valores a 1920x1080 de referencia). Esta es la
        // posicion real usada por el icono de arma del HUD de Tab.
        private const float IconMaxWidth = 185f;
        private const float IconMaxHeight = 97f;
        private const float IconOffsetX = -147.5f;
        private const float IconOffsetY = 87f;
        private const int SortingOrder = 615;       // encima del AmmoOverviewHUD(610)

        [MenuItem("PrisaControlada/Configurar FlashlightHUD")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Flashlight HUD", "Guarda la escena primero.", "OK");
                return;
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Flashlight HUD", "No se encontro el Player en la escena.", "OK");
                return;
            }

            Texture2D texLinterna = AssetDatabase.LoadAssetAtPath<Texture2D>(IconLinternaPath);
            if (texLinterna == null)
            {
                EditorUtility.DisplayDialog("Flashlight HUD",
                    "Falta el icono en Assets/Resources/HUD/Linterna.png.", "OK");
                return;
            }

            FlashlightHUD hud = player.GetComponent<FlashlightHUD>();
            if (hud == null)
                hud = player.AddComponent<FlashlightHUD>();

            // Tamaño del icono segun su proporcion real (mismo fit que los demas).
            Vector2 iconSize = FitIcon(texLinterna);

            // --- Canvas (ScreenSpaceOverlay 1920x1080) ---
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

            // El fade (aparecer/desaparecer) se aplica con este CanvasGroup.
            CanvasGroup group = canvasRect.GetComponent<CanvasGroup>();
            if (group == null)
                group = canvasRect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // --- Icono (misma posicion que el icono del HUD de arma) ---
            RectTransform iconRect = FindOrCreate(canvasRect, "Icon", out bool createdIcon);
            if (iconRect == null) return;

            if (createdIcon)
            {
                iconRect.anchorMin = new Vector2(1f, 0f);
                iconRect.anchorMax = new Vector2(1f, 0f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(IconOffsetX, IconOffsetY);
                iconRect.sizeDelta = new Vector2(185f, 92f);
            }

            RawImage icon = iconRect.GetComponent<RawImage>();
            if (icon == null)
                icon = iconRect.gameObject.AddComponent<RawImage>();
            icon.texture = texLinterna;
            icon.raycastTarget = false;

            // --- Cablear referencias en el componente ---
            AmmoOverviewHUD ammoHUD = player.GetComponent<AmmoOverviewHUD>();
            FlashlightToggle flashlight = FindFlashlight();

            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("ammoHUD").objectReferenceValue = ammoHUD;
            so.FindProperty("flashlight").objectReferenceValue = flashlight;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = canvasRect.gameObject;

            Debug.Log("[FlashlightHUD] Icono creado en la Jerarquia bajo Player > " + CanvasName +
                      " (Icon). Referencias cableadas y escena guardada.");
            EditorUtility.DisplayDialog("Flashlight HUD",
                "Icono creado en la Jerarquia (Player/" + CanvasName + "/Icon).\n\n" +
                "Se selecciono el canvas; podes moverlo o cambiar su tamaño a mano.\n" +
                "En Play: se ve solo con la linterna en la mano. Si hay un arma " +
                "equipada, el HUD de arma usa esta misma posicion.", "OK");
        }

        // La linterna puede estar inactiva en la escena: la busca incluyendo
        // inactivas, priorizando la que esta activa en la jerarquia.
        private static FlashlightToggle FindFlashlight()
        {
            FlashlightToggle[] all = Object.FindObjectsByType<FlashlightToggle>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (FlashlightToggle ft in all)
                if (ft != null && ft.gameObject.activeInHierarchy)
                    return ft;

            return all.Length > 0 ? all[0] : null;
        }

        // Escala el icono para que entre en la caja maxima sin deformarse.
        private static Vector2 FitIcon(Texture2D texture)
        {
            float scale = Mathf.Min(
                IconMaxWidth / texture.width,
                IconMaxHeight / texture.height);

            return new Vector2(
                Mathf.Round(texture.width * scale),
                Mathf.Round(texture.height * scale));
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
