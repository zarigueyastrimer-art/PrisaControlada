using System.Text.RegularExpressions;
using PrisaControlada.Core;
using PrisaControlada.Items;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    //TP2 - Ian Rosenberg
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class GameOverController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button quitButton;

        [Header("Teclado (pantalla de derrota sin botones)")]
        [Tooltip("1 = reiniciar, 2 = menu, 3 = salir. Solo se leen mientras esta pantalla esta visible.")]
        [SerializeField] private bool usarTeclasNumericas = true;

        [Header("Metros")]
        [Tooltip("Texto TMP con los metros. Si se deja vacio se busca un hijo llamado 'Metros'.")]
        [SerializeField] private TextMeshProUGUI metrosText;

        [Header("Scene")]
        [SerializeField] private string menuSceneName = "MainMenu";

        // Plantilla original del texto (por ejemplo '"000" METERS TRAVELED')
        // para reemplazar solo el numero y no perder el resto del rótulo.
        private string _metrosPlantilla;

        // Si Show() llega antes de Start() (escena cargada con el canvas
        // desactivado), Start no debe volver a ocultar la pantalla.
        private bool _mostrado;

        private void Awake()
        {
            if (metrosText == null)
                metrosText = BuscarTextoMetros();

            if (metrosText != null)
                _metrosPlantilla = metrosText.text;
        }

        private void Start()
        {
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
            if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

            if (!_mostrado)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!usarTeclasNumericas) return;

            // Si quedo abierto el teclado de codigos o una nota, esos digitos
            // son suyos: no los usamos para manejar el final.
            if (CodeUI.IsOpen || NoteViewer.IsOpen || NotaLoreViewer.IsOpen) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                Retry();
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                GoToMenu();
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                QuitGame();
        }

        public void Show()
        {
            _mostrado = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            RedFilterController redFilter = FindObjectOfType<RedFilterController>();
            if (redFilter != null)
                redFilter.ForceNeutralLook();

            gameObject.SetActive(true);

            // Se actualiza despues de activar: si el canvas arranca desactivado,
            // Awake/Start recien corren en este momento y ya capturan la plantilla.
            ActualizarMetros();
        }

        // Pinta en el texto los metros que avanzo el jugador hasta morir.
        // Busca el RunDistanceTracker de la escena: el lo midio en el mismo
        // instante en que se disparo OnDied, antes de que llegue Show().
        private void ActualizarMetros()
        {
            float metros = 0f;
            RunDistanceTracker tracker = FindFirstObjectByType<RunDistanceTracker>();
            if (tracker != null)
            {
                metros = tracker.MetrosRecorridos > 0f
                    ? tracker.MetrosRecorridos
                    : tracker.MetrosActuales;
            }

            int valor = Mathf.RoundToInt(metros);

            // Parte 3: persistir en el archivo de guardado. Guarda los metros
            // de esta corrida y, si es la mejor de la dificultad actual,
            // actualiza el récord (se hace aunque no haya texto en pantalla).
            Guardado.RegistrarMuerte(valor);

            if (metrosText == null) return;

            string plantilla = string.IsNullOrEmpty(_metrosPlantilla)
                ? metrosText.text
                : _metrosPlantilla;

            // 1) Reemplaza lo que este entre comillas: '"000"' -> '"19815"'.
            string nuevo = Regex.Replace(plantilla, "\"[^\"]*\"", "\"" + valor + "\"");

            // 2) Si no hay comillas, reemplaza el primer numero que encuentre.
            if (nuevo == plantilla)
                nuevo = Regex.Replace(plantilla, "\\d+", valor.ToString());

            // 3) Si la plantilla no tenia numero, arma el rótulo completo.
            if (nuevo == plantilla)
                nuevo = "\"" + valor + "\" METERS TRAVELED";

            metrosText.text = nuevo;
        }

        private TextMeshProUGUI BuscarTextoMetros()
        {
            foreach (TextMeshProUGUI t in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t.name == "Metros")
                    return t;
            }

            foreach (TextMeshProUGUI t in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t.text != null && t.text.Contains("METERS"))
                    return t;
            }

            return null;
        }

        private void Retry()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void GoToMenu()
        {
            // El prefab tiene el campo vacio: si nadie lo configuró, usamos MainMenu.
            string escena = string.IsNullOrEmpty(menuSceneName) ? "MainMenu" : menuSceneName;

            if (!Application.CanStreamedLevelBeLoaded(escena))
            {
                Debug.LogWarning($"[GameOver] No se puede cargar la escena '{escena}'.");
                return;
            }

            SceneManager.LoadScene(escena);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
