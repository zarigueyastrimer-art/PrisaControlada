using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PrisaControlada.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class DeathFXController : MonoBehaviour
    {
        [Header("Broken Camera")]
        [SerializeField] private RawImage brokenImage;
        [SerializeField] private Texture brokenTexture;
        [SerializeField] private float brokenShakeDuration = 0.5f;
        [SerializeField] private float brokenShakeAmount = 14f;
        [SerializeField] private float brokenFadeDuration = 0.15f;

        [Header("Break Flash")]
        [SerializeField] private RawImage flashImage;
        [SerializeField] private float flashDuration = 0.08f;

        [Header("Glitch Video")]
        [SerializeField] private RawImage glitchImage;
        [SerializeField] private VideoClip glitchClip;
        [SerializeField] private Material chromaKeyMaterial;
        [SerializeField] private float glitchDelay = 0.35f;
        [SerializeField] private float glitchFadeDuration = 0.25f;

        [Header("Game Over")]
        [SerializeField] private GameOverController gameOver;

        public event System.Action OnGlitchFinished;

        private VideoPlayer _video;
        private RenderTexture _renderTexture;
        private RectTransform _brokenRect;
        private Vector2 _brokenOrigin;

        private void Awake()
        {
            if (brokenImage != null)
            {
                _brokenRect = brokenImage.rectTransform;
                _brokenOrigin = _brokenRect.anchoredPosition;
                brokenImage.color = new Color(1f, 1f, 1f, 0f);
                if (brokenTexture != null)
                    brokenImage.texture = brokenTexture;

                // If the RawImage has a texture assigned, use it instead of the procedural cracks.
                if (brokenImage.texture != null && brokenImage.material != null && brokenImage.material.HasProperty("_UseTexture"))
                    brokenImage.material.SetFloat("_UseTexture", 1f);

                // Keep the broken camera above the glitch and the game over menu.
                brokenImage.transform.SetAsLastSibling();

                // Guarantee its canvas renders on top of everything, no matter where it lives.
                Canvas brokenCanvas = brokenImage.GetComponentInParent<Canvas>();
                if (brokenCanvas != null)
                {
                    brokenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    if (brokenCanvas.sortingOrder < 900)
                        brokenCanvas.sortingOrder = 900;
                }
            }

            if (flashImage != null)
                flashImage.color = new Color(1f, 1f, 1f, 0f);

            if (glitchImage != null)
            {
                glitchImage.color = new Color(1f, 1f, 1f, 0f);
                if (chromaKeyMaterial != null)
                    glitchImage.material = chromaKeyMaterial;

                _video = glitchImage.gameObject.GetComponent<VideoPlayer>();
                if (_video == null)
                    _video = glitchImage.gameObject.AddComponent<VideoPlayer>();

                _video.playOnAwake = false;
                _video.renderMode = VideoRenderMode.RenderTexture;
                _video.isLooping = false;
                _video.skipOnDrop = true;
                _video.clip = glitchClip;

                _renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
                _renderTexture.Create();
                _video.targetTexture = _renderTexture;
                glitchImage.texture = _renderTexture;
            }
        }

        private void OnDestroy()
        {
            if (_renderTexture != null)
                _renderTexture.Release();
        }

        public void PlaySequence()
        {
            StopAllCoroutines();
            StartCoroutine(Sequence());
        }

        private IEnumerator Sequence()
        {
            yield return StartCoroutine(BreakFlash());
            yield return StartCoroutine(BrokenCameraIn());
            yield return new WaitForSeconds(glitchDelay);
            yield return StartCoroutine(PlayGlitch());
        }

        private IEnumerator BreakFlash()
        {
            if (flashImage == null) yield break;

            flashImage.color = new Color(1f, 1f, 1f, 0.9f);
            float start = Time.time;
            while (Time.time - start < flashDuration)
            {
                float u = (Time.time - start) / flashDuration;
                flashImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.9f, 0f, u));
                yield return null;
            }
            flashImage.color = new Color(1f, 1f, 1f, 0f);
        }

        private IEnumerator BrokenCameraIn()
        {
            if (brokenImage == null) yield break;

            float start = Time.time;
            while (Time.time - start < brokenShakeDuration)
            {
                float u = (Time.time - start) / brokenShakeDuration;
                float decay = 1f - u;

                if (_brokenRect != null)
                    _brokenRect.anchoredPosition = _brokenOrigin + Random.insideUnitCircle * brokenShakeAmount * decay;

                // Appear immediately at the impact while the shake is strongest.
                float a = Mathf.Clamp01((Time.time - start) / brokenFadeDuration);
                brokenImage.color = new Color(1f, 1f, 1f, a);

                yield return null;
            }

            brokenImage.color = new Color(1f, 1f, 1f, 1f);
            if (_brokenRect != null)
                _brokenRect.anchoredPosition = _brokenOrigin;
        }

        private IEnumerator PlayGlitch()
        {
            if (_video == null || glitchClip == null) yield break;

            _video.Prepare();
            while (!_video.isPrepared)
                yield return null;

            _video.frame = 0;
            _video.Play();

            float fadeStart = Time.time;
            while (Time.time - fadeStart < glitchFadeDuration)
            {
                float u = (Time.time - fadeStart) / glitchFadeDuration;
                glitchImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 1f, u));
                yield return null;
            }
            glitchImage.color = new Color(1f, 1f, 1f, 1f);

            while (_video.isPlaying)
                yield return null;

            float fadeEnd = Time.time;
            while (Time.time - fadeEnd < 0.3f)
            {
                float u = (Time.time - fadeEnd) / 0.3f;
                glitchImage.color = new Color(1f, 1f, 1f, 1f - u);
                yield return null;
            }
            glitchImage.color = new Color(1f, 1f, 1f, 0f);

            OnGlitchFinished?.Invoke();
            if (gameOver != null)
                gameOver.Show();
        }
    }
}