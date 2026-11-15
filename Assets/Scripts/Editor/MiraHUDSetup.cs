using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    // Tool: reemplaza el "circulito" interactuable por la mira de 4 esquinas
    // ("mira abierta"), partida de Assets/VHSEffectURP/hud/hudUpdate/ en 4 PNG
    // de 30x30.
    //
    //   Player (prefab) > Crosshair_Canvas > Crosshair (contenedor, sin grafico)
    //                                     > Esquina sup izq / sup der / inf izq / inf der
    //
    // La mira queda siempre visible y, al apuntar a algo interactuable, las 4
    // esquinas se deslizan 20px hacia el centro (smooth) sin achicarse ni
    // cambiar de sprite. La logica vive en InteractionCrosshair.
    //
    // Idempotente: si ya existe se conserva (no pisa tus ajustes) y solo se
    // completa lo que falte. Trabaja sobre el prefab (la mira vive ahi, no en
    // la escena) y luego ajusta la instancia de la escena.
    public static class MiraHUDSetup
    {
        private const string PrefabPath = "Assets/Prefabs/player/Player.prefab";
        private const string CanvasName = "Crosshair_Canvas";
        private const string CrosshairName = "Crosshair";
        private const string TexDir = "Assets/VHSEffectURP/hud/hudUpdate/";

        // Orden del arreglo esquinas: sup izq, sup der, inf izq, inf der.
        // En UI la Y sube: las esquinas superiores van en +35.
        private static readonly string[] Nombres =
        {
            "Esquina sup izq", "Esquina sup der", "Esquina inf izq", "Esquina inf der"
        };

        private static readonly string[] Texturas =
        {
            "Mira esq sup izq.png", "Mira esq sup der.png",
            "Mira esq inf izq.png", "Mira esq inf der.png"
        };

        private static readonly Vector2[] Posiciones =
        {
            new Vector2(-35f, 35f), new Vector2(35f, 35f),
            new Vector2(-35f, -35f), new Vector2(35f, -35f)
        };

        private const float LadoEsquina = 30f;   // arte de cada esquina
        private const float LadoMira = 100f;      // apertura total (contenedor)
        private const float DistanciaCierre = 20f; // px por eje -> mira cerrada 60x60
        private const float DuracionCierre = 0.15f;

        [MenuItem("PrisaControlada/Configurar MiraHUD")]
        public static void Setup()
        {
            if (!System.IO.File.Exists(PrefabPath))
            {
                EditorUtility.DisplayDialog("Mira HUD", "No existe " + PrefabPath + ".", "OK");
                return;
            }

            Texture2D[] texes = new Texture2D[Nombres.Length];
            for (int i = 0; i < Nombres.Length; i++)
            {
                texes[i] = LoadTexture(TexDir + Texturas[i]);
                if (texes[i] == null)
                {
                    EditorUtility.DisplayDialog("Mira HUD",
                        "Faltan los recortes de la mira en " + TexDir + "\n(" + Texturas[i] + ").", "OK");
                    return;
                }
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Transform canvasT = FindDeep(root.transform, CanvasName);
                if (canvasT == null)
                {
                    EditorUtility.DisplayDialog("Mira HUD",
                        "No existe " + CanvasName + " en " + PrefabPath + ".", "OK");
                    return;
                }

                // --- Contenedor de la mira (antes tenia el circulo) ---
                RectTransform crossRect = FindOrCreate(canvasT, CrosshairName, out _);
                if (crossRect == null) return;

                Image circulo = crossRect.GetComponent<Image>();
                if (circulo != null)
                    Object.DestroyImmediate(circulo);   // el circulito se va

                crossRect.anchorMin = new Vector2(0.5f, 0.5f);
                crossRect.anchorMax = new Vector2(0.5f, 0.5f);
                crossRect.pivot = new Vector2(0.5f, 0.5f);
                crossRect.anchoredPosition = Vector2.zero;
                crossRect.sizeDelta = new Vector2(LadoMira, LadoMira);

                // --- Las 4 esquinas ---
                RectTransform[] esquinas = new RectTransform[Nombres.Length];

                for (int i = 0; i < Nombres.Length; i++)
                {
                    RectTransform rt = FindOrCreate(crossRect, Nombres[i], out bool creada);
                    if (rt == null) return;

                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);

                    // Posicion y tamano solo si la creamos nosotros: si ya
                    // existe, tu retoque a mano se respeta.
                    if (creada)
                    {
                        rt.anchoredPosition = Posiciones[i];
                        rt.sizeDelta = new Vector2(LadoEsquina, LadoEsquina);
                    }

                    RawImage ri = rt.GetComponent<RawImage>();
                    if (ri == null)
                        ri = rt.gameObject.AddComponent<RawImage>();
                    if (ri.texture == null)
                        ri.texture = texes[i];
                    ri.color = Color.white;
                    ri.raycastTarget = false;

                    esquinas[i] = rt;
                }

                // --- Logica: InteractionCrosshair ---
                InteractionCrosshair logica = canvasT.GetComponent<InteractionCrosshair>();
                bool creadaLogica = logica == null;
                if (creadaLogica)
                    logica = canvasT.gameObject.AddComponent<InteractionCrosshair>();

                SerializedObject so = new SerializedObject(logica);

                SerializedProperty propEsquinas = so.FindProperty("esquinas");
                if (propEsquinas == null || !propEsquinas.isArray)
                {
                    Debug.LogError("[MiraHUD] InteractionCrosshair no tiene la propiedad 'esquinas'.");
                    return;
                }

                propEsquinas.arraySize = esquinas.Length;
                for (int i = 0; i < esquinas.Length; i++)
                    propEsquinas.GetArrayElementAtIndex(i).objectReferenceValue = esquinas[i];

                if (creadaLogica)
                {
                    SerializedProperty pDist = so.FindProperty("distanciaCierre");
                    if (pDist != null) pDist.floatValue = DistanciaCierre;

                    SerializedProperty pDur = so.FindProperty("duracionCierre");
                    if (pDur != null) pDur.floatValue = DuracionCierre;
                }

                so.ApplyModifiedProperties();

                // --- Siempre visible ---
                CanvasGroup group = canvasT.GetComponent<CanvasGroup>();
                if (group == null)
                    group = canvasT.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 1f;
                group.interactable = false;
                group.blocksRaycasts = false;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // --- Instancia en la escena (hereda el prefab; solo asegura alpha) ---
            InteractionCrosshair enEscena = AjustarEscena(out bool escenaCambiada);

            if (escenaCambiada)
            {
                Scene scene = SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            // Seleccionamos la mira de la escena (si esta abierta) o el prefab.
            GameObject aSeleccionar = enEscena != null ? enEscena.gameObject : null;
            if (aSeleccionar == null)
                aSeleccionar = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeGameObject = aSeleccionar;

            Debug.Log("[MiraHUD] Mira de 4 esquinas configurada en " + PrefabPath +
                      " > " + CanvasName + " > " + CrosshairName + " (4 esquinas de 30px a +/-35).");

            EditorUtility.DisplayDialog("Mira HUD",
                "Circulito reemplazado por la mira de 4 esquinas (siempre visible).\n\n" +
                "En la Jerarquia: Player > " + CanvasName + " > " + CrosshairName +
                " > Esquina sup izq / sup der / inf izq / inf der (es un prefab: " +
                "Assets/Prefabs/player/Player.prefab).\n\n" +
                "Al apuntar a algo interactuable las 4 esquinas se cierran suave " +
                "hacia el centro (20px por eje = la 'mira cerrada'). En el componente " +
                "InteractionCrosshair (sobre Crosshair_Canvas) podes retocar:\n" +
                "  - Duracion Cierre: que tan lento/rapido cierra\n" +
                "  - Distancia Cierre: cuantos px se acercan al centro\n\n" +
                "Si movés una esquina a mano, esa pasa a ser su posicion abierta.", "OK");
        }

        // ---------------------------------------------------------------
        // Escena: la instancia del Player hereda el prefab. Solo nos
        // aseguramos de que el CanvasGroup este en alpha 1 (antes arrancaba
        // en 0 y solo se veia al mirar algo interactuable).
        // ---------------------------------------------------------------
        private static InteractionCrosshair AjustarEscena(out bool cambio)
        {
            cambio = false;
            InteractionCrosshair primero = null;

            foreach (InteractionCrosshair ih in Object.FindObjectsByType<InteractionCrosshair>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ih == null) continue;
                if (!ih.gameObject.scene.IsValid()) continue;

                primero = ih;

                CanvasGroup group = ih.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = ih.gameObject.AddComponent<CanvasGroup>();
                    cambio = true;
                }

                if (group.alpha != 1f)
                {
                    group.alpha = 1f;
                    cambio = true;
                }

                if (group.interactable)
                {
                    group.interactable = false;
                    cambio = true;
                }

                if (group.blocksRaycasts)
                {
                    group.blocksRaycasts = false;
                    cambio = true;
                }
            }

            return primero;
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
                Debug.LogError("[MiraHUD] No se pudo cargar la textura: " + path);

            return tex;
        }

        // ---------------------------------------------------------------
        // Utilidades
        // ---------------------------------------------------------------
        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }

            return null;
        }

        private static RectTransform FindOrCreate(Transform parent, string name, out bool creada)
        {
            creada = false;

            Transform existing = parent.Find(name);
            if (existing != null)
            {
                RectTransform asRect = existing as RectTransform;
                if (asRect != null)
                    return asRect;

                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            creada = true;
            return go.GetComponent<RectTransform>();
        }
    }
}
