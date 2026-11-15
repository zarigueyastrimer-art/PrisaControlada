using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    // Temporizador del HUD: muestra MM:SS:MS y cuenta hacia adelante desde
    // 00:00:00. El ultimo campo conserva los dos primeros digitos del
    // contador de milisegundos (por ejemplo, 1234 ms se muestran como 12).
    // Al llegar a 99:59:99 vuelve a 00:00:00.
    //
    // La UI NO se construye por codigo: la jerarquia
    // (GameTimerHUD_Canvas/TimerText) se crea en la escena con la tool de
    // Editor "PrisaControlada/Configurar GameTimerHUD" y de ahi se puede
    // retocar a mano.
    [DisallowMultipleComponent]
    public class GameTimerHUD : MonoBehaviour
    {
        // 100 minutos, 59 segundos y dos digitos para el campo de
        // milisegundos: al llegar al final del ciclo, el siguiente intervalo
        // reinicia el temporizador.
        private const long MillisecondsPerCycle = 100L * 60L * 1000L;

        [Header("UI (asignada por la tool o a mano en la jerarquia)")]
        [Tooltip("Texto que muestra el tiempo en formato MM:SS:MS.")]
        [SerializeField] private Text timerText;

        private double _elapsedMilliseconds;
        private long _displayedMillisecondTick = -1;

        private void Awake()
        {
            if (!ResolveSceneReferences())
            {
                Debug.LogError(
                    "[GameTimerHUD] No se encontro el texto del temporizador. " +
                    "Ejecuta el menu PrisaControlada/Configurar GameTimerHUD con la escena abierta. " +
                    "Componente deshabilitado.", this);
                enabled = false;
                return;
            }

            _elapsedMilliseconds = 0d;
            _displayedMillisecondTick = -1;
            SetDisplayedMilliseconds(0);
        }

        private void Update()
        {
            // Time.deltaTime hace que el temporizador se detenga junto con el
            // juego (por ejemplo, mientras NoteViewer pausa la partida).
            _elapsedMilliseconds += Time.deltaTime * 1000d;

            long elapsedMilliseconds = (long)_elapsedMilliseconds;
            long millisecondTick = elapsedMilliseconds / 100L;
            if (millisecondTick == _displayedMillisecondTick) return;

            SetDisplayedMilliseconds(elapsedMilliseconds);
        }

        private void SetDisplayedMilliseconds(long totalMilliseconds)
        {
            _displayedMillisecondTick = totalMilliseconds / 100L;
            if (timerText != null)
                timerText.text = FormatTime(totalMilliseconds);
        }

        // Formato fijo: siempre dos digitos por campo. El campo de
        // milisegundos usa los dos primeros digitos del contador: 1234 ms
        // se representan como 12.
        public static string FormatTime(long totalMilliseconds)
        {
            long cycleMilliseconds = totalMilliseconds % MillisecondsPerCycle;
            if (cycleMilliseconds < 0) cycleMilliseconds += MillisecondsPerCycle;

            int minutes = (int)(cycleMilliseconds / 60000L);
            int seconds = (int)(cycleMilliseconds / 1000L % 60L);
            int milliseconds = (int)(cycleMilliseconds / 100L % 100L);

            return $"{minutes:00}:{seconds:00}:{milliseconds:00}";
        }

        // ------------------------------------------------------------------
        // Referencias: primero la serializada (Inspector) y, si falta, la
        // resuelve por nombre en la jerarquia que arma la tool de Editor.
        // No crea ningun objeto.
        // ------------------------------------------------------------------
        private bool ResolveSceneReferences()
        {
            if (timerText == null)
                timerText = transform.Find("TimerText")?.GetComponent<Text>();

            if (timerText == null)
            {
                Transform canvas = transform.Find("GameTimerHUD_Canvas");
                if (canvas != null)
                    timerText = canvas.Find("TimerText")?.GetComponent<Text>();
            }

            return timerText != null;
        }
    }
}
