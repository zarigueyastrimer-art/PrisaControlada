using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Menu
{
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private CanvasGroup mainPanel;
        [SerializeField] private CanvasGroup creditsPanel;

        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button creditsBackButton;

        [Header("Scene")]
        [SerializeField] private string gameSceneName = "Prototype";

        [Header("Glitch Effect")]
        [SerializeField] private Text titleText;
        [SerializeField] private float glitchInterval = 4f;
        [SerializeField] private float glitchDuration = 0.15f;

        [Header("Fade")]
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private float fadeDuration = 1f;

        private char[] _glitchChars = { '!', '@', '#', '$', '%', '&', '*', '?', '<', '>', '/', '\\' };
        private string _originalTitle;
        private bool _isTransitioning;

        private void Update()
        {
            if (EventSystem.current == null) return;
            var pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = Mouse.current != null
                ? (Vector2)Mouse.current.position.ReadValue()
                : Vector2.zero;
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);
            if (results.Count > 0)
                Debug.Log($"[Menu] Raycast hit: {results[0].gameObject.name}");
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            if (mainPanel != null) mainPanel.alpha = 1f;
            if (creditsPanel != null) creditsPanel.alpha = 0f;

            if (fadeOverlay != null)
            {
                fadeOverlay.alpha = 1f;
                StartCoroutine(FadeIn());
            }

            SetupButton(playButton, "PLAY");
            SetupButton(creditsButton, "CREDITS");
            SetupButton(quitButton, "QUIT");
            SetupButton(creditsBackButton, "BACK");

            if (titleText != null)
                _originalTitle = titleText.text;

            StartCoroutine(GlitchLoop());
        }

        private void SetupButton(Button btn, string label)
        {
            if (btn == null)
            {
                Debug.LogError($"[Menu] {label} button is NULL");
                return;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                Debug.Log($"[Menu] {label} clicked");
                switch (label)
                {
                    case "PLAY": PlayGame(); break;
                    case "CREDITS": ShowCredits(); break;
                    case "QUIT": QuitGame(); break;
                    case "BACK": HideCredits(); break;
                }
            });
        }

        public void PlayGame()
        {
            if (_isTransitioning) return;
            StartCoroutine(TransitionToScene(gameSceneName));
        }

        public void ShowCredits()
        {
            if (_isTransitioning) return;
            StartCoroutine(CrossFade(mainPanel, creditsPanel));
        }

        public void HideCredits()
        {
            if (_isTransitioning) return;
            StartCoroutine(CrossFade(creditsPanel, mainPanel));
        }

        public void QuitGame()
        {
            if (_isTransitioning) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator CrossFade(CanvasGroup from, CanvasGroup to)
        {
            _isTransitioning = true;

            if (to != null) to.blocksRaycasts = true;
            if (to != null) to.interactable = true;

            float t = 0f;
            float duration = 0.3f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = t / duration;
                if (from != null) from.alpha = 1f - u;
                if (to != null) to.alpha = u;
                yield return null;
            }

            if (from != null) from.alpha = 0f;
            if (from != null) from.blocksRaycasts = false;
            if (from != null) from.interactable = false;
            if (to != null) to.alpha = 1f;
            _isTransitioning = false;
        }

        private IEnumerator TransitionToScene(string sceneName)
        {
            _isTransitioning = true;

            if (fadeOverlay != null)
            {
                float t = 0f;
                while (t < fadeDuration)
                {
                    t += Time.unscaledDeltaTime;
                    fadeOverlay.alpha = t / fadeDuration;
                    yield return null;
                }
                fadeOverlay.alpha = 1f;
            }

            yield return new WaitForSecondsRealtime(0.2f);
            SceneManager.LoadScene(sceneName);
        }

        private IEnumerator FadeIn()
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeOverlay.alpha = 1f - (t / fadeDuration);
                yield return null;
            }
            fadeOverlay.alpha = 0f;
        }

        private IEnumerator GlitchLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(glitchInterval + Random.Range(-1f, 1f));
                if (titleText != null && !string.IsNullOrEmpty(_originalTitle))
                    yield return StartCoroutine(GlitchTitle());
            }
        }

        private IEnumerator GlitchTitle()
        {
            float end = Time.unscaledTime + glitchDuration;
            char[] chars = _originalTitle.ToCharArray();

            while (Time.unscaledTime < end)
            {
                for (int i = 0; i < chars.Length; i++)
                {
                    if (Random.value < 0.15f)
                        chars[i] = _glitchChars[Random.Range(0, _glitchChars.Length)];
                    else
                        chars[i] = _originalTitle[i];
                }
                titleText.text = new string(chars);
                titleText.color = new Color(1f, Random.Range(0.1f, 0.3f), Random.Range(0.1f, 0.3f));
                yield return new WaitForSecondsRealtime(0.05f);
            }

            titleText.text = _originalTitle;
            titleText.color = Color.white;
        }
    }
}
