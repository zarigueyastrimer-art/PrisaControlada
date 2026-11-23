using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Menu
{
    //TP2 - Francisco Larrondo
    /// <summary>
    /// Corrige el anclaje de las etiquetas TMP que están sobre los marcadores.
    ///
    /// Cada marcador es un sprite de 1920x1080 estirado a toda la pantalla. Si el
    /// texto tiene su ancla en el centro (0.5, 0.5), deja de coincidir con la
    /// pestaña cuando cambia la relación de aspecto de la pantalla.
    ///
    /// La corrección convierte la posición actual de referencia (1920x1080) en un
    /// ancla normalizada. Así el texto conserva exactamente su lugar en 1920x1080
    /// y sigue a la pestaña en cualquier otra resolución.
    /// </summary>
    public static class MarkerLabelLayoutFixer
    {
        private const string KeepPositionMenu =
            "PrisaControlada/Menu Libro: Conservar posición y corregir anclas de etiquetas";

        private const string CenterInTabMenu =
            "PrisaControlada/Menu Libro: Centrar etiquetas en las pestañas";

        private static readonly Vector2 DefaultReferenceSize = new Vector2(1920f, 1080f);

        /// <summary>
        /// Aplica la migración al menú que ya esté abierto. No guarda la escena:
        /// conserva todos los cambios pendientes del usuario y sólo reemplaza el
        /// anclaje defectuoso de las cinco etiquetas.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ScheduleAutomaticFix()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                var activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid() || !activeScene.path.EndsWith("MainMenu.unity"))
                    return;

                KeepPositionAndFixAnchors();
            };
        }

        [MenuItem(KeepPositionMenu, priority = 100)]
        public static void KeepPositionAndFixAnchors()
        {
            var markers = Object.FindObjectsByType<BookMarker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int changed = 0;
            int alreadyCorrect = 0;
            int missing = 0;

            foreach (var marker in markers)
            {
                var label = FindLabel(marker);
                if (label == null)
                {
                    missing++;
                    continue;
                }

                var labelRt = label.rectTransform;

                // Si ya tiene un ancla normalizada (distinta del centro), no la toco.
                if (!UsesCenterAnchor(labelRt))
                {
                    alreadyCorrect++;
                    continue;
                }

                var referenceSize = GetReferenceSize(marker);
                var normalizedPosition = new Vector2(
                    0.5f + labelRt.anchoredPosition.x / referenceSize.x,
                    0.5f + labelRt.anchoredPosition.y / referenceSize.y);

                Undo.RecordObject(labelRt, "Corregir anclas de etiquetas");
                ApplyNormalizedAnchor(labelRt, normalizedPosition);
                EditorUtility.SetDirty(labelRt);
                EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
                changed++;
            }

            Debug.Log(
                $"<color=#77ff77>[Menu Libro] Anclas corregidas: {changed}. " +
                $"Ya correctas: {alreadyCorrect}. Sin etiqueta: {missing}. " +
                "Guardá la escena con Ctrl+S.</color>");
        }

        [MenuItem(CenterInTabMenu, priority = 101)]
        public static void CenterLabelsInTabs()
        {
            var markers = Object.FindObjectsByType<BookMarker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int changed = 0;

            foreach (var marker in markers)
            {
                var label = FindLabel(marker);
                if (label == null) continue;

                var so = new SerializedObject(marker);
                var pixelArea = so.FindProperty("pixelArea").rectValue;
                var spriteSize = so.FindProperty("spriteSize").vector2Value;
                if (spriteSize.x <= 0f || spriteSize.y <= 0f)
                    spriteSize = DefaultReferenceSize;

                // Centro de la pestaña en coordenadas UI (origen abajo-izquierda).
                float centerX = pixelArea.x + pixelArea.width * 0.5f;
                float centerYSprite = pixelArea.y + pixelArea.height * 0.5f;
                float centerY = spriteSize.y - centerYSprite;
                var normalizedPosition = new Vector2(
                    centerX / spriteSize.x,
                    centerY / spriteSize.y);

                Undo.RecordObject(label.rectTransform, "Centrar etiquetas en pestañas");
                ApplyNormalizedAnchor(label.rectTransform, normalizedPosition);
                EditorUtility.SetDirty(label.rectTransform);
                EditorSceneManager.MarkSceneDirty(marker.gameObject.scene);
                changed++;
            }

            Debug.Log(
                $"<color=#77ff77>[Menu Libro] Etiquetas centradas en las pestañas: {changed}. " +
                "Guardá la escena con Ctrl+S.</color>");
        }

        private static Graphic FindLabel(BookMarker marker)
        {
            if (marker == null) return null;

            var tmp = marker.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) return tmp;

            // Compatibilidad con etiquetas legacy de escenas anteriores.
            return marker.GetComponentInChildren<Text>(true);
        }

        private static Vector2 GetReferenceSize(BookMarker marker)
        {
            var so = new SerializedObject(marker);
            var size = so.FindProperty("spriteSize").vector2Value;
            if (size.x <= 0f || size.y <= 0f)
                size = DefaultReferenceSize;
            return size;
        }

        private static bool UsesCenterAnchor(RectTransform rt)
        {
            const float tolerance = 0.0001f;
            return Approximately(rt.anchorMin, rt.anchorMax) &&
                   Approximately(rt.anchorMin, new Vector2(0.5f, 0.5f), tolerance);
        }

        private static bool Approximately(Vector2 a, Vector2 b, float tolerance = 0.0001f)
        {
            return Mathf.Abs(a.x - b.x) <= tolerance &&
                   Mathf.Abs(a.y - b.y) <= tolerance;
        }

        private static void ApplyNormalizedAnchor(RectTransform rt, Vector2 normalizedPosition)
        {
            normalizedPosition.x = Mathf.Clamp01(normalizedPosition.x);
            normalizedPosition.y = Mathf.Clamp01(normalizedPosition.y);

            rt.anchorMin = normalizedPosition;
            rt.anchorMax = normalizedPosition;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
