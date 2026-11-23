using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    //TP2 - Matías Morilla
    // Tool: pone los bordes de los slots (el arte "Slots.png" de
    // Assets/VHSEffectURP/hud/hudUpdate) como hijos de los slots existentes
    // del inventario (Player > InventoryHUD_Canvas > SlotsBar > Slot1 | Slot2).
    //
    // Se usa el PNG original directamente (sin recortes auxiliares): cada
    // borde es un RawImage estirado al 100% del slot cuyo uvRect selecciona
    // su recuadro dentro de la hoja de 1920x1080 (izquierdo para Slot1,
    // derecho para Slot2). Asi el recuadro queda centrado sobre el slot solo,
    // y si el slot cambia de tamano el borde lo sigue.
    //
    // Idempotente: si el borde ya existe se conserva (no pisa tus ajustes)
    // y solo se completa lo que falte. No crea UI en runtime.
    public static class SlotsSetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/player/Player.prefab";

        // Arte original: hoja 1920x1080 con los dos recuadros de 193x193.
        private const string SheetPath = "Assets/VHSEffectURP/hud/hudUpdate/Slots.png";
        private const string BorderName = "Borde";

        private const float SheetWidth = 1920f;
        private const float SheetHeight = 1080f;

        // Aire alrededor del recuadro para no cortar el borde suave (px).
        private const float Margin = 2f;

        // Recuadros medidos en la hoja (origen arriba-izquierda, como en el PNG):
        //   izquierdo x[758-950] y[863-1055]
        //   derecho  x[966-1158] y[863-1055]
        // El primero va en Slot1 y el segundo en Slot2.
        private static readonly Rect[] Frames =
        {
            new Rect(758f - Margin, 863f - Margin, 193f + 2f * Margin, 193f + 2f * Margin),
            new Rect(966f - Margin, 863f - Margin, 193f + 2f * Margin, 193f + 2f * Margin)
        };

        private static readonly string[] SlotPaths =
        {
            "InventoryHUD_Canvas/SlotsBar/Slot1",
            "InventoryHUD_Canvas/SlotsBar/Slot2"
        };

        [MenuItem("PrisaControlada/Configurar Slots")]
        public static void Setup()
        {
            Texture2D sheet = LoadSheet();
            if (sheet == null)
            {
                EditorUtility.DisplayDialog("Slots",
                    "No se pudo cargar la textura:\n" + SheetPath + "\n\n" +
                    "Revisa que el archivo exista y que Unity lo haya importado " +
                    "(foco en Unity y mira la Console).", "OK");
                return;
            }

            if (!System.IO.File.Exists(PlayerPrefabPath))
            {
                EditorUtility.DisplayDialog("Slots", "No se encontro el prefab Player.", "OK");
                return;
            }

            int created = 0;
            int updated = 0;
            int missing = 0;
            bool changedAny = false;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                for (int i = 0; i < SlotPaths.Length; i++)
                {
                    Transform slot = root.transform.Find(SlotPaths[i]);
                    if (slot == null)
                    {
                        missing++;
                        Debug.LogError("[Slots] No se encontro el slot en el prefab: " + SlotPaths[i]);
                        continue;
                    }

                    bool changed = EnsureBorder(slot, sheet, Frames[i], out bool wasCreated);
                    if (wasCreated) created++;
                    else updated++;
                    if (changed) changedAny = true;
                }

                if (changedAny)
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (missing > 0)
            {
                EditorUtility.DisplayDialog("Slots",
                    "Faltan slots en el prefab (" + missing + " de " + SlotPaths.Length + ").\n" +
                    "Revisa que exista InventoryHUD_Canvas/SlotsBar/Slot1 y Slot2.", "OK");
                return;
            }

            // Seleccionar la barra de slots en la escena para retocar a mano.
            GameObject player = GameObject.Find("Player");
            Transform bar = player != null
                ? player.transform.Find("InventoryHUD_Canvas/SlotsBar")
                : null;
            Selection.activeGameObject = bar != null ? bar.gameObject : player;

            Debug.Log("[Slots] Bordes: " + created + " nuevo(s), " + updated +
                      " ya existente(s), usando " + SheetPath +
                      ". Player > InventoryHUD_Canvas > SlotsBar > Slot1|Slot2 > Borde.");
            EditorUtility.DisplayDialog("Slots",
                (created > 0
                    ? "Borde agregado a " + created + " slot(s).\n\n"
                    : "Los bordes ya existian (no se duplican).\n\n") +
                "Cada borde usa Slots.png original con uvRect para elegir su recuadro, " +
                "estirado al 100% del slot (centrado solo).\n" +
                "Se selecciono SlotsBar en la escena por si queres mover algo a mano.", "OK");
        }

        // Carga la hoja original; si AssetDatabase aun no la tiene, fuerza la
        // importacion y reintenta antes de rendirse.
        private static Texture2D LoadSheet()
        {
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (sheet != null) return sheet;

            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);

            if (sheet == null)
                Debug.LogError("[Slots] No se pudo cargar la textura: " + SheetPath);

            return sheet;
        }

        // Crea (o completa) el RawImage del borde dentro del slot.
        // created = se acaba de crear; valor de retorno = hubo cambios que
        // justifiquen guardar el prefab.
        private static bool EnsureBorder(Transform slot, Texture2D sheet, Rect frame, out bool created)
        {
            created = false;
            bool changed = false;

            Transform existing = slot.Find(BorderName);
            RectTransform rect = existing as RectTransform;

            // Un objeto con ese nombre pero sin RectTransform no sirve: se reemplaza.
            if (existing != null && rect == null)
            {
                Object.DestroyImmediate(existing.gameObject);
                existing = null;
                rect = null;
                changed = true;
            }

            if (rect == null)
            {
                GameObject go = new GameObject(BorderName, typeof(RectTransform));
                // SetParent sin worldPositionStays y al final: ultimo hijo = se
                // dibuja encima del preview y del fondo del slot.
                go.transform.SetParent(slot, false);

                rect = go.GetComponent<RectTransform>();
                // Estirado total al slot -> el recuadro queda centrado solo.
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                created = true;
                changed = true;
            }

            RawImage img = rect.GetComponent<RawImage>();
            if (img == null)
            {
                img = rect.gameObject.AddComponent<RawImage>();
                changed = true;
            }

            if (img.texture != sheet)
            {
                img.texture = sheet;
                changed = true;
            }

            // uvRect: porcion de la hoja que dibuja este borde (su recuadro).
            Rect uv = ToUvRect(frame);
            if (img.uvRect != uv)
            {
                img.uvRect = uv;
                changed = true;
            }

            if (img.raycastTarget)
            {
                img.raycastTarget = false;
                changed = true;
            }

            // Marca el objeto por si el prefab ya estaba cargado en memoria.
            if (changed)
                EditorUtility.SetDirty(img.gameObject);

            return changed;
        }

        // La hoja se mide con origen arriba-izquierda (como el PNG); en UV el
        // origen es abajo-izquierda, por eso se invierte el eje y.
        private static Rect ToUvRect(Rect pixels)
        {
            return new Rect(
                pixels.x / SheetWidth,
                1f - (pixels.y + pixels.height) / SheetHeight,
                pixels.width / SheetWidth,
                pixels.height / SheetHeight);
        }
    }
}
