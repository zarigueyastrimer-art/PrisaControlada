using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    // Tool: crea la jerarquia REAL del bloque de municion (AmmoOverviewHUD) en
    // la escena abierta (Prototype Old) y cablea las referencias del componente.
    // La UI queda en la Jerarquia como GameObjects normales para retocarla a
    // mano (tamanos, posiciones, color, fuente); NO se genera nada en runtime.
    //
    // La jerarquia migrada es:
    // Player/AmmoOverviewHUD_Canvas/CurrentItem/Icon+Count
    //
    // Solo se muestra el arma equipada. El bloque copia el layout real del icono
    // de linterna, de modo que ambos ocupan exactamente el mismo lugar.
    public static class AmmoOverviewHUDSetup
    {
        private const string CanvasName = "AmmoOverviewHUD_Canvas";
        private const string CurrentItemName = "CurrentItem";

        private const string IconEscopetaPath = "Assets/Resources/HUD/Escopeta.png";
        private const string IconRevolverPath = "Assets/Resources/HUD/Revolver.png";
        private const string IconGlockPath = "Assets/Resources/HUD/Glock.png";
        private const string FontPath = "Assets/Resources/HUD/DS-DIGIT.ttf";

        // Valores de respaldo si todavia no existe el HUD de linterna.
        private const float FallbackIconOffsetX = -147.5f;
        private const float FallbackIconOffsetY = 87f;
        private const float FallbackIconWidth = 185f;
        private const float FallbackIconHeight = 92f;

        // El contador queda debajo del icono, sin taparlo.
        private const float CountOffsetY = -52f;
        private const float CountHeight = 46f;
        private const int CountFontSize = 42;
        private const int SortingOrder = 610;

        [MenuItem("PrisaControlada/Configurar AmmoOverviewHUD")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Ammo Overview HUD", "Guarda la escena primero.", "OK");
                return;
            }

            ConfigureScene(scene, true);
        }

        // Usado por el menu y por la migracion automatica de la escena.
        public static bool ConfigureScene(Scene scene, bool showDialog)
        {
            if (!scene.IsValid())
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("Ammo Overview HUD", "La escena no es valida.", "OK");
                return false;
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("Ammo Overview HUD", "No se encontro el Player en la escena.", "OK");
                return false;
            }

            Texture2D texEscopeta = AssetDatabase.LoadAssetAtPath<Texture2D>(IconEscopetaPath);
            Texture2D texRevolver = AssetDatabase.LoadAssetAtPath<Texture2D>(IconRevolverPath);
            Texture2D texGlock = AssetDatabase.LoadAssetAtPath<Texture2D>(IconGlockPath);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            if (texEscopeta == null || texRevolver == null || texGlock == null || font == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Ammo Overview HUD",
                        "Faltan assets en Assets/Resources/HUD (iconos Revolver/Glock/Escopeta o DS-DIGIT.ttf).", "OK");
                }
                return false;
            }

            AmmoOverviewHUD hud = player.GetComponent<AmmoOverviewHUD>();
            if (hud == null)
                hud = player.AddComponent<AmmoOverviewHUD>();

            // --- Canvas (ScreenSpaceOverlay 1920x1080) ---
            RectTransform canvasRect = FindOrCreate(player.transform, CanvasName, out bool createdCanvas);
            if (canvasRect == null) return false;

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

            // El fade de Tab se aplica con este CanvasGroup (lo maneja el script).
            CanvasGroup group = canvasRect.GetComponent<CanvasGroup>();
            if (group == null)
                group = canvasRect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Migracion desde la lista anterior de tres armas. El Panel viejo
            // contenia los bloques, por lo que al eliminarlo se van todos.
            RemoveLegacyObjects(canvasRect);

            // --- Bloque unico: icono del arma equipada + contador de reserva ---
            RectTransform currentItem = FindOrCreate(canvasRect, CurrentItemName, out bool createdCurrentItem);
            if (currentItem == null) return false;

            if (createdCurrentItem)
                CopyFlashlightLayout(player.transform, currentItem);

            RawImage icon = BuildIcon(currentItem, texEscopeta);
            Text count = BuildCount(currentItem, font);
            if (icon == null || count == null)
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("Ammo Overview HUD", "No se pudo crear CurrentItem.", "OK");
                return false;
            }

            // --- Cablear referencias en el componente ---
            SerializedObject so = new SerializedObject(hud);
            SetObjectReference(so, "canvas", canvas);
            SetObjectReference(so, "group", group);
            SetObjectReference(so, "currentItem", currentItem);
            SetObjectReference(so, "icon", icon);
            SetObjectReference(so, "count", count);
            SetObjectReference(so, "shotgunTexture", texEscopeta);
            SetObjectReference(so, "revolverTexture", texRevolver);
            SetObjectReference(so, "glockTexture", texGlock);
            so.ApplyModifiedProperties();

            // El componente vive en una instancia del prefab Player: hay que
            // registrar las referencias como overrides de esa instancia.
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);

            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = currentItem.gameObject;

            Debug.Log("[AmmoOverviewHUD] Jerarquia migrada en Player > " + CanvasName +
                      " > " + CurrentItemName + " > Icon+Count. Icono alineado con la linterna; " +
                      "referencias y texturas cableadas. Escena guardada.");

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Ammo Overview HUD",
                    "HUD migrado en la Jerarquia:\n\n" +
                    "Player/" + CanvasName + "/" + CurrentItemName + "/Icon+Count\n\n" +
                    "Solo aparece con Tab y un arma equipada.\n" +
                    "Muestra la reserva de esa arma; la linterna no tiene contador.",
                    "OK");
            }

            return true;
        }

        private static void RemoveLegacyObjects(RectTransform canvas)
        {
            string[] legacyNames =
            {
                "Panel",
                "Block_Escopeta",
                "Block_Revolver",
                "Block_Glock",
            };

            foreach (string legacyName in legacyNames)
            {
                Transform legacy = canvas.Find(legacyName);
                if (legacy != null)
                    Object.DestroyImmediate(legacy.gameObject);
            }
        }

        // Copia el rectangulo real del icono de linterna. Asi, si el usuario lo
        // movio a mano en la escena, el HUD de arma queda exactamente alineado.
        private static void CopyFlashlightLayout(Transform player, RectTransform target)
        {
            Transform flashlightCanvas = player.Find("FlashlightHUD_Canvas");
            RectTransform flashlightIcon = flashlightCanvas != null
                ? flashlightCanvas.Find("Icon") as RectTransform
                : null;

            if (flashlightIcon != null)
            {
                target.anchorMin = flashlightIcon.anchorMin;
                target.anchorMax = flashlightIcon.anchorMax;
                target.pivot = flashlightIcon.pivot;
                target.anchoredPosition = flashlightIcon.anchoredPosition;
                target.sizeDelta = flashlightIcon.sizeDelta;
                return;
            }

            target.anchorMin = new Vector2(1f, 0f);
            target.anchorMax = new Vector2(1f, 0f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = new Vector2(FallbackIconOffsetX, FallbackIconOffsetY);
            target.sizeDelta = new Vector2(FallbackIconWidth, FallbackIconHeight);
        }

        private static RawImage BuildIcon(RectTransform currentItem, Texture2D texture)
        {
            RectTransform iconRect = FindOrCreate(currentItem, "Icon", out bool createdIcon);
            if (iconRect == null) return null;

            if (createdIcon)
            {
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
            }

            RawImage icon = iconRect.GetComponent<RawImage>();
            if (icon == null)
                icon = iconRect.gameObject.AddComponent<RawImage>();
            icon.texture = texture;
            icon.raycastTarget = false;

            return icon;
        }

        private static Text BuildCount(RectTransform currentItem, Font font)
        {
            RectTransform countRect = FindOrCreate(currentItem, "Count", out bool createdCount);
            if (countRect == null) return null;

            if (createdCount)
            {
                countRect.anchorMin = new Vector2(0f, 0f);
                countRect.anchorMax = new Vector2(1f, 0f);
                countRect.pivot = new Vector2(0.5f, 0f);
                countRect.anchoredPosition = new Vector2(0f, CountOffsetY);
                countRect.sizeDelta = new Vector2(0f, CountHeight);
            }

            Text count = countRect.GetComponent<Text>();
            if (count == null)
                count = countRect.gameObject.AddComponent<Text>();

            if (createdCount)
            {
                count.font = font;
                count.fontSize = CountFontSize;
                count.alignment = TextAnchor.MiddleCenter;
                count.color = Color.white;
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                count.verticalOverflow = VerticalWrapMode.Overflow;
                count.text = "0";
            }
            else if (count.font == null)
            {
                count.font = font;
            }
            count.raycastTarget = false;

            return count;
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
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
