using UnityEditor;
using UnityEngine;

// Modos de iluminacion SOLO para la Scene View del editor.
//
// - Unlit: Unity reemplaza las luces y el ambiente del juego por su iluminacion
//   por defecto de editor, que siempre deja ver la geometria (ideal para
//   pasillos oscuros).
// - Lit: vuelve a usar la iluminacion real de la escena (luces + sombras).
//
// NO toca RenderSettings, la escena, los materiales ni nada del juego:
// el Game View y el Play Mode se ven exactamente igual que siempre.
//
// Uso: Tools > Scene View > Lit (luz de la escena)    [Ctrl+Shift+K]
//      Tools > Scene View > Unlit (ignorar luz...)    [Ctrl+Shift+L]
// La opcion queda guardada en EditorPrefs, asi que persiste entre sesiones.
[InitializeOnLoad]
public static class SceneViewUnlitMode
{
    const string kPrefKey = "PrisaControlada.SceneViewUnlit";

    const string kLitMenuPath = "Tools/Scene View/Lit (luz de la escena)";
    const string kLitMenuItem = kLitMenuPath + " %#k";

    const string kUnlitMenuPath = "Tools/Scene View/Unlit (ignorar luz de la escena)";
    const string kUnlitMenuItem = kUnlitMenuPath + " %#l";

    static SceneViewUnlitMode()
    {
        EditorApplication.delayCall += () => SetUnlit(IsUnlit(), false);
    }

    // Vuelve a la iluminacion real de la escena.
    [MenuItem(kLitMenuItem, false, 1000)]
    private static void SetLit()
    {
        EditorPrefs.SetBool(kPrefKey, false);
        SetUnlit(false, true);
    }

    [MenuItem(kLitMenuItem, true)]
    private static bool SetLitValidation()
    {
        Menu.SetChecked(kLitMenuItem, !IsUnlit());
        return true;
    }

    // Alterna entre Unlit y Lit (se mantiene el atajo de siempre).
    [MenuItem(kUnlitMenuItem, false, 1001)]
    private static void Toggle()
    {
        bool enabled = !IsUnlit();
        EditorPrefs.SetBool(kPrefKey, enabled);
        SetUnlit(enabled, true);
    }

    [MenuItem(kUnlitMenuItem, true)]
    private static bool ToggleValidation()
    {
        Menu.SetChecked(kUnlitMenuItem, IsUnlit());
        return true;
    }

    private static bool IsUnlit()
    {
        return EditorPrefs.GetBool(kPrefKey, false);
    }

    private static void SetUnlit(bool unlit, bool notify)
    {
        foreach (SceneView view in SceneView.sceneViews)
        {
            if (view == null)
            {
                continue;
            }

            if (view.sceneLighting == !unlit)
            {
                continue;
            }

            view.sceneLighting = !unlit;
        }

        SceneView.RepaintAll();

        if (notify && SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.ShowNotification(
                new GUIContent(unlit ? "Scene View: Unlit" : "Scene View: luz de la escena"));
        }
    }
}
