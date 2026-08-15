using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class GameOverController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button quitButton;

        [Header("Scene")]
        [SerializeField] private string menuSceneName = "";

        private void Start()
        {
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
            if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

            gameObject.SetActive(false);
        }

        public void Show()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            RedFilterController redFilter = FindObjectOfType<RedFilterController>();
            if (redFilter != null)
                redFilter.ForceNeutralLook();

            gameObject.SetActive(true);
        }

        private void Retry()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void GoToMenu()
        {
            if (string.IsNullOrEmpty(menuSceneName))
            {
                Debug.LogWarning("[GameOver] No hay 'Menu Scene Name' configurado.");
                return;
            }

            SceneManager.LoadScene(menuSceneName);
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