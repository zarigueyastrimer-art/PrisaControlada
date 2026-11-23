using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    //TP2 - Matías Morilla
    // Tool: reemplaza la barra de stamina vieja (que se dibujaba en runtime con
    // un canvas verde) por la nueva:
    //   Player > StaminaHUD_Canvas > Contenedor (arte "Stamina contenedor.png")
    //                              > Ventana (recorte, sin grafico)
    //                                > Barra (arte "Stamina barra.png")
    //
    // La barra no se achica: se desliza de derecha a izquierda dentro del
    // contenedor y el shader PrisaControlada/StaminaBarClip recorta todo lo que
    // queda fuera de la ventana interior.
    //
    // La UI queda en la Jerarquia para retocarla a mano; no se crea nada en
    // runtime. Idempotente: si ya existe se conserva (no pisa tus ajustes) y
    // solo se completa lo que falte.
    public static class StaminaHUDSetup
    {
        private const string CanvasName = "StaminaHUD_Canvas";
        private const string ContenedorName = "Contenedor";
        private const string VentanaName = "Ventana";
        private const string BarraName = "Barra";

        private const string TexContenedorPath = "Assets/Resources/HUD/Stamina contenedor.png";
        private const string TexBarraPath = "Assets/Resources/HUD/Stamina barra.png";
        private const string ShaderPath = "Assets/Resources/HUD/StaminaBarClip.shader";
        private const string MaterialPath = "Assets/Resources/HUD/StaminaBarClip.mat";
        private const string ShaderName = "PrisaControlada/StaminaBarClip";

        // Orden entre HUDs: por encima de la camara (350), por debajo de
        // micro (505), zoom (506), GameOver (600) y municion (610).
        private const int SortingOrder = 500;

        // Layout por defecto - replicado del arte original (lienzo 1920x1080):
        //   contenedor: x[594-1325] y[782-807] (arriba-abajo) -> 732x26,
        //               o sea 273px de base desde el borde inferior.
        //   es un soporte en U: paredes de 6px a los lados y piso de 6px abajo.
        //   ventana interior: x[6-726] y[6-26] -> 720x20.
        private const float ContenedorWidth = 732f;
        private const float ContenedorHeight = 26f;
        private const float ContenedorBase = 273f;   // desde el borde inferior
        private const float Pared = 6f;

        private const float VentanaWidth = 720f;
        private const float VentanaHeight = 20f;
        private const float VentanaX = Pared;         // desde la pared izquierda
        private const float VentanaY = Pared;         // desde el piso del soporte

        // "Stamina barra.png" -> parte recta de 625px + triangulito de 12px.
        private const float LineaLargo = 625f;
        private const float ArteLargo = 637f;
        private const float BarraHeight = 19f;

        [MenuItem("PrisaControlada/Configurar StaminaHUD")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Stamina HUD", "Guarda la escena primero.", "OK");
                return;
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Stamina HUD", "No se encontro el Player en la escena.", "OK");
                return;
            }

            Texture2D texContenedor = LoadTexture(TexContenedorPath);
            Texture2D texBarra = LoadTexture(TexBarraPath);
            Shader shader = LoadShader();

            if (texContenedor == null || texBarra == null || shader == null)
            {
                EditorUtility.DisplayDialog("Stamina HUD",
                    "Faltan los assets en Assets/Resources/HUD (Stamina barra.png / " +
                    "Stamina contenedor.png / StaminaBarClip.shader).", "OK");
                return;
            }

            Material material = LoadOrCreateMaterial(shader);

            // --- Sacar el HUD viejo (dibujaba la UI en runtime) ---
            int removed = RemoveOldHud();

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

            // --- Contenedor: el soporte en U (fijo) ---
            RectTransform contRect = FindOrCreate(canvasRect, ContenedorName, out bool createdCont);
            if (contRect == null) return;

            if (createdCont)
            {
                contRect.anchorMin = new Vector2(0.5f, 0f);
                contRect.anchorMax = new Vector2(0.5f, 0f);
                contRect.pivot = new Vector2(0f, 0f);
                contRect.anchoredPosition = new Vector2(-ContenedorWidth * 0.5f, ContenedorBase);
                contRect.sizeDelta = new Vector2(ContenedorWidth, ContenedorHeight);
            }

            RawImage contenedor = contRect.GetComponent<RawImage>();
            if (contenedor == null)
                contenedor = contRect.gameObject.AddComponent<RawImage>();
            contenedor.texture = texContenedor;
            contenedor.raycastTarget = false;

            // --- Ventana: el interior del contenedor (define el recorte) ---
            RectTransform ventRect = FindOrCreate(contRect, VentanaName, out bool createdVent);
            if (ventRect == null) return;

            if (createdVent)
            {
                ventRect.anchorMin = Vector2.zero;
                ventRect.anchorMax = Vector2.zero;
                ventRect.pivot = Vector2.zero;
                ventRect.anchoredPosition = new Vector2(VentanaX, VentanaY);
                ventRect.sizeDelta = new Vector2(VentanaWidth, VentanaHeight);
            }

            // --- Barra: la que se desliza ---
            RectTransform barraRect = FindOrCreate(ventRect, BarraName, out bool createdBarra);
            if (barraRect == null) return;

            if (createdBarra)
            {
                barraRect.anchorMin = Vector2.zero;
                barraRect.anchorMax = Vector2.zero;
                barraRect.pivot = Vector2.zero;
            }

            // El ancho lo maneja el script en runtime (la parte recta tiene que
            // llenar exactamente la ventana), asi que siempre se recalcula con
            // el ancho actual del contenedor. La Y tambien arranca en su sitio
            // (apoyada en el piso); la posicion X la pone el script al 100%.
            float escala = ventRect.rect.width > 0f
                ? ventRect.rect.width / LineaLargo
                : VentanaWidth / LineaLargo;

            Vector2 sizeBarra = barraRect.sizeDelta;
            sizeBarra.x = ArteLargo * escala;
            if (createdBarra)
                sizeBarra.y = BarraHeight;   // alto del arte (19px)
            barraRect.sizeDelta = sizeBarra;

            RawImage barra = barraRect.GetComponent<RawImage>();
            if (barra == null)
                barra = barraRect.gameObject.AddComponent<RawImage>();
            barra.texture = texBarra;
            barra.material = material;
            barra.raycastTarget = false;

            // --- Logica: StaminaBarHUD ---
            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null)
                controller = player.GetComponentInChildren<PlayerController>(true);

            StaminaBarHUD hud = canvasRect.GetComponent<StaminaBarHUD>();
            if (hud == null)
                hud = canvasRect.gameObject.AddComponent<StaminaBarHUD>();

            SerializedObject so = new SerializedObject(hud);
            SetReference(so, "ventana", ventRect);
            SetReference(so, "barra", barraRect);
            SetReference(so, "barraImage", barra);
            SetReference(so, "contenedorImage", contenedor);
            if (controller != null)
                SetReference(so, "player", controller);
            so.ApplyModifiedProperties();

            // Pose inicial (100%: el triangulito detras de la pared derecha) y
            // recorte del material actualizado, para que el Scene view ya muestre
            // el corte real de la barra dentro del contenedor.
            hud.ColocarEnCienPorCiento();
            EditorUtility.SetDirty(material);

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = canvasRect.gameObject;

            Debug.Log("[StaminaHUD] " + (removed > 0 ? "HUD viejo eliminado (" + removed + "). " : "") +
                      "Barra de stamina creada en " + CanvasName + " > Contenedor > Ventana > Barra. " +
                      "Escena guardada.");
            EditorUtility.DisplayDialog("Stamina HUD",
                (removed > 0
                    ? "La barra de stamina vieja (la que se dibujaba en runtime) fue eliminada.\n\n"
                    : "") +
                "Barra nueva en la Jerarquia (Player/" + CanvasName + " > Contenedor > Ventana > Barra).\n\n" +
                "La barra se desliza de derecha a izquierda dentro del contenedor: el triangulito " +
                "marca la stamina y al 100% queda tapado por la pared derecha. En el Scene view " +
                "ya se ve el corte del shader (y se actualiza si moves la barra o el contenedor).\n\n" +
                "El ancho y la posicion X los maneja el script (es la stamina): correr esta tool " +
                "de nuevo los deja al 100%. La Y, los colores y los umbrales los retocas vos a " +
                "mano en la Jerarquia y en el componente StaminaBarHUD.", "OK");
        }

        // ---------------------------------------------------------------
        // El HUD viejo (PlayerStaminaHUD) creaba un canvas en runtime.
        // Se quitan esos componentes de la escena (y del prefab, si estuviera).
        // ---------------------------------------------------------------
        private static int RemoveOldHud()
        {
            int removed = 0;

            foreach (PlayerStaminaHUD old in Object.FindObjectsByType<PlayerStaminaHUD>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (old == null) continue;
                if (!old.gameObject.scene.IsValid()) continue;

                GameObject go = old.gameObject;

                // Si el GameObject solo tenia el HUD viejo, se borra entero.
                bool soloHud = true;
                foreach (Component c in go.GetComponents<Component>())
                {
                    if (c == null || c is Transform || c is PlayerStaminaHUD) continue;
                    soloHud = false;
                    break;
                }

                if (soloHud)
                    Undo.DestroyObjectImmediate(go);
                else
                    Undo.DestroyObjectImmediate(old);

                removed++;
            }

            removed += RemoveOldHudFromPrefab();

            return removed;
        }

        private static int RemoveOldHudFromPrefab()
        {
            const string PlayerPrefabPath = "Assets/Prefabs/player/Player.prefab";
            if (!System.IO.File.Exists(PlayerPrefabPath)) return 0;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            int removed = 0;

            try
            {
                foreach (PlayerStaminaHUD old in root.GetComponentsInChildren<PlayerStaminaHUD>(true))
                {
                    if (old == null) continue;

                    GameObject go = old.gameObject;
                    bool soloHud = true;
                    foreach (Component c in go.GetComponents<Component>())
                    {
                        if (c == null || c is Transform || c is PlayerStaminaHUD) continue;
                        soloHud = false;
                        break;
                    }

                    if (soloHud)
                        Object.DestroyImmediate(go);
                    else
                        Object.DestroyImmediate(old);

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

        // ---------------------------------------------------------------
        // Assets
        // ---------------------------------------------------------------
        private static Texture2D LoadTexture(string path)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (tex == null)
                Debug.LogError("[StaminaHUD] No se pudo cargar la textura: " + path);

            return tex;
        }

        private static Shader LoadShader()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader != null) return shader;

            AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
            shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader != null) return shader;

            shader = Shader.Find(ShaderName);
            if (shader == null)
                Debug.LogError("[StaminaHUD] No se pudo cargar el shader: " + ShaderPath);

            return shader;
        }

        private static Material LoadOrCreateMaterial(Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            material = new Material(shader);
            material.SetFloat("_UseUIAlphaClip", 0f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[StaminaHUD] Material creado: " + MaterialPath);
            return material;
        }

        // ---------------------------------------------------------------
        // Utilidades
        // ---------------------------------------------------------------
        private static void SetReference(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError("[StaminaHUD] El componente StaminaBarHUD no tiene la propiedad '" +
                               propertyName + "'.");
                return;
            }

            prop.objectReferenceValue = value;
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
