using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Menu
{
    /// <summary>
    /// Da funcionalidad a los botones del menú-libro en runtime.
    ///
    /// Se agrega a la escena con el menú de editor
    /// "PrisaControlada/Menu Libro: Configurar botones". En Start() busca los
    /// botones por nombre y les asigna la acción correspondiente:
    ///
    ///   - BotonSalir     -> cierra el juego
    ///   - BotonJugar     -> carga la escena "Prototype Old"
    ///   - BotonPrototipo -> carga la escena "Prototype"
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuButtonActions : MonoBehaviour
    {
        [Header("Botones (se autocompletan desde el menú de editor)")]
        [SerializeField] private Button quitButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button prototypeButton;

        [Header("Escenas")]
        [SerializeField] private string playScene = "Prototype Old";
        [SerializeField] private string prototypeScene = "Prototype";

        private void Start()
        {
            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
            else
                Debug.LogWarning("[Menu] BotonSalir no asignado en MenuButtonActions.");

            if (playButton != null)
                playButton.onClick.AddListener(() => Load(playScene));
            else
                Debug.LogWarning("[Menu] BotonJugar no asignado en MenuButtonActions.");

            if (prototypeButton != null)
                prototypeButton.onClick.AddListener(() => Load(prototypeScene));
            else
                Debug.LogWarning("[Menu] BotonPrototipo no asignado en MenuButtonActions.");
        }

        private void Load(string sceneName)
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogError(
                    $"[Menu] No se puede cargar la escena '{sceneName}'. " +
                    "Agregala en File > Build Settings.");
            }
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
