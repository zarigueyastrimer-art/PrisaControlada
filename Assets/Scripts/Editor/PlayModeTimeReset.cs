using UnityEditor;
using UnityEngine;

namespace PrisaControlada.Editor
{
    // Este proyecto corre con "Enter Play Mode Options" (EditorSettings) y la
    // recarga de dominio DESACTIVADA, asi que el estado estatico y Time.timeScale
    // sobreviven de una sesion de Play a la siguiente.
    //
    // NoteViewer pone Time.timeScale = 0 cuando abris una nota. Si el Play se
    // corto con la nota abierta (o el valor quedo traspapelado), el siguiente
    // Play arrancaba "en pausa": Time.deltaTime = 0 y ni la camara ni el
    // jugador responden (la "camara suelta").
    //
    // Este hook fuerza Time.timeScale = 1 al entrar en Play Mode.
    [InitializeOnLoad]
    public static class PlayModeTimeReset
    {
        static PlayModeTimeReset()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode &&
                state != PlayModeStateChange.EnteredPlayMode)
                return;

            if (Mathf.Approximately(Time.timeScale, 1f))
                return;

            Debug.LogWarning(
                $"[PlayMode] Time.timeScale era {Time.timeScale} al iniciar Play. " +
                "Restablecido a 1 (el juego arrancaba en pausa).");
            Time.timeScale = 1f;
        }
    }
}
