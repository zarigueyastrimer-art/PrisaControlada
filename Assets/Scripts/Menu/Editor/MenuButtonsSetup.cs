using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Menu
{
    /// <summary>
    /// Menú de editor: "PrisaControlada/Menu Libro: Configurar botones".
    ///
    /// A diferencia de "Armar en escena activa", ESTE no reconstruye nada:
    /// sólo busca los botones que ya existen en la escena y les asigna sus
    /// acciones (salir / Prototype Old / Prototype) a través de un
    /// <see cref="MenuButtonActions"/>.
    ///
    /// Es seguro de correr tantas veces como quieras: no borra, no duplica
    /// y no modifica el resto del menú.
    /// </summary>
    public static class MenuButtonsSetup
    {
        private const string QuitButtonName = "BotonSalir";
        private const string PlayButtonName = "BotonJugar";
        private const string PrototypeButtonName = "BotonPrototipo";

        [MenuItem("PrisaControlada/Menu Libro: Configurar botones")]
        public static void Configure()
        {
            // --- Botones ---
            var quitBtn = FindButton(QuitButtonName);
            var playBtn = FindButton(PlayButtonName);
            var protoBtn = FindButton(PrototypeButtonName);

            int found = (quitBtn != null ? 1 : 0) + (playBtn != null ? 1 : 0) + (protoBtn != null ? 1 : 0);
            if (found == 0)
            {
                Debug.LogError(
                    "[Menu Botones] No se encontró ningún botón en la escena.\n" +
                    "Primero ejecutá 'PrisaControlada/Menu Libro: Armar en escena activa' " +
                    "una vez para crear la estructura, y después usá este menú.");
                return;
            }

            // --- Componente MenuButtonActions ---
            var host = FindHost();
            var actions = host.GetComponent<MenuButtonActions>();
            if (actions == null) actions = Undo.AddComponent<MenuButtonActions>(host.gameObject);

            var so = new SerializedObject(actions);
            SetRef(so, "quitButton", quitBtn);
            SetRef(so, "playButton", playBtn);
            SetRef(so, "prototypeButton", protoBtn);
            so.FindProperty("playScene").stringValue = "Prototype Old";
            so.FindProperty("prototypeScene").stringValue = "Prototype";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(host.gameObject.scene);

            Debug.Log(
                "<color=#77ff77>[Menu Botones] Botones configurados:\n" +
                $"  {QuitButtonName} -> salir del juego\n" +
                $"  {PlayButtonName} -> escena 'Prototype Old'\n" +
                $"  {PrototypeButtonName} -> escena 'Prototype'\n" +
                "No hace falta correr 'Armar en escena activa' de nuevo.</color>");
        }

        private static Button FindButton(string name)
        {
            foreach (var btn in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (btn.name == name) return btn;

            Debug.LogWarning($"[Menu Botones] No se encontró el botón '{name}' en la escena.");
            return null;
        }

        /// <summary>
        /// Devuelve el objeto que aloja MenuButtonActions: primero busca uno que
        /// ya lo tenga, si no usa el BookMenu, y en última instancia crea uno.
        /// </summary>
        private static GameObject FindHost()
        {
            var existing = Object.FindFirstObjectByType<MenuButtonActions>(FindObjectsInactive.Include);
            if (existing != null) return existing.gameObject;

            var bookMenu = GameObject.Find("BookMenu");
            if (bookMenu != null) return bookMenu;

            var go = new GameObject("MenuButtonActions");
            Undo.RegisterCreatedObjectUndo(go, "Menu Botones");
            return go;
        }

        private static void SetRef(SerializedObject so, string name, Object value)
        {
            var prop = so.FindProperty(name);
            if (prop != null) prop.objectReferenceValue = value;
        }
    }
}
