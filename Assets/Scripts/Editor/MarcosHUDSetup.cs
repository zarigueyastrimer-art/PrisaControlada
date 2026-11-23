using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    //TP2 - Matías Morilla
    // Tool: quita (o vuelve a poner) las lineas blancas de "marco" que dejan
    // las barras:
    //
    //   Stamina: el arte "Stamina contenedor.png" es una U finita (una linea
    //             horizontal con las paredes) que se ve siempre. Se oculta
    //             desactivando el componente RawImage del Contenedor (NO el
    //             GameObject, porque Ventana y Barra son hijos suyos).
    //
    //   Zoom: el arte "Zoom barra.png" tiene pegada al borde derecho de la
    //         escalera una barra vertical solida de 6px (mas dos tapas
    //         horizontales). Se cambia por "Zoom recorrido.png", que es el
    //         mismo arte sin ese marco (solo los iconos y las rayitas).
    //
    // Idempotente: se puede correr las veces que quieras; no pisa nada mas.
    public static class MarcosHUDSetup
    {
        private const string TexConMarco = "Assets/Resources/HUD/Zoom barra.png";
        private const string TexSinMarco = "Assets/Resources/HUD/Zoom recorrido.png";

        [MenuItem("PrisaControlada/Ocultar marcos HUD (Stamina + Zoom)")]
        public static void Ocultar()
        {
            Aplicar(true);
        }

        [MenuItem("PrisaControlada/Mostrar marcos HUD (Stamina + Zoom)")]
        public static void Mostrar()
        {
            Aplicar(false);
        }

        private static void Aplicar(bool ocultar)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Marcos HUD", "Guarda la escena primero.", "OK");
                return;
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Marcos HUD", "No se encontro el Player en la escena.", "OK");
                return;
            }

            int cambios = 0;
            string detalle = "";

            // --- Stamina: se oculta el grafico del contenedor ---
            Transform cont = player.transform.Find("StaminaHUD_Canvas/Contenedor");
            if (cont != null)
            {
                RawImage raw = cont.GetComponent<RawImage>();
                if (raw != null && raw.enabled == ocultar)
                {
                    raw.enabled = !ocultar;
                    EditorUtility.SetDirty(raw);
                    cambios++;
                    detalle += "Stamina: el marco del contenedor " +
                               (ocultar ? "quedo oculto." : "volvio a verse.") + "\n";
                }
            }
            else
            {
                detalle += "Stamina: no se encontro StaminaHUD_Canvas/Contenedor.\n";
            }

            // --- Zoom: arte con marco o sin marco ---
            Transform zoomBar = player.transform.Find("ZoomHUD_Canvas/ZoomBar");
            if (zoomBar != null)
            {
                RawImage raw = zoomBar.GetComponent<RawImage>();
                if (raw != null)
                {
                    Texture2D tex = LoadTexture(ocultar ? TexSinMarco : TexConMarco);
                    if (tex != null && raw.texture != tex)
                    {
                        raw.texture = tex;
                        EditorUtility.SetDirty(raw);
                        cambios++;
                        detalle += "Zoom: la barra ahora usa " +
                                   (ocultar ? "Zoom recorrido.png (sin marco)." : "Zoom barra.png (con marco).") + "\n";
                    }
                }
            }
            else
            {
                detalle += "Zoom: no se encontro ZoomHUD_Canvas/ZoomBar.\n";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[MarcosHUD] " + (ocultar ? "Marcos ocultos" : "Marcos visibles") + ". " + detalle.Replace("\n", " "));

            EditorUtility.DisplayDialog("Marcos HUD",
                (cambios > 0 ? "Cambios:\n" + detalle
                             : "No habia nada que cambiar (ya estaba como lo pediste).\n") +
                "\n" +
                (ocultar
                    ? "Ahora se ven solo la barra de stamina (la de color) y las rayitas del zoom."
                    : "Volvieron a verse el marco de la stamina y la linea vertical del zoom."),
                "OK");
        }

        private static Texture2D LoadTexture(string path)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (tex == null)
                Debug.LogError("[MarcosHUD] No se pudo cargar la textura: " + path);

            return tex;
        }
    }
}
