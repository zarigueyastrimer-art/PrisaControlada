using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PrisaControlada.Player
{
    //TP2 - Ian Rosenberg
    /// <summary>
    /// Reproduce el video de ruido VHS (vhsNoise.mp4, con fondo verde) sobre
    /// el RawImage del canvas de la escena y deja el verde transparente con el
    /// shader de chroma key, para que solo se vea el ruido sobre el juego.
    ///
    /// La jerarquia (VHSNoise_Canvas / VhsNoiseImage) la crea y cablea la tool
    /// de editor "PrisaControlada/Configurar VHS Noise (HUD)". Asi el canvas,
    /// el material y el video se editan en el Inspector, no desde el codigo.
    /// </summary>
    [DisallowMultipleComponent]
    public class VhsNoiseOverlay : MonoBehaviour
    {
        private const float PrepareTimeout = 5f;

        [Header("Video")]
        [Tooltip("RawImage que muestra el video (dentro del canvas de la escena).")]
        [SerializeField] private RawImage vhsImage;
        [Tooltip("Video con el ruido. Trae fondo verde (chroma key).")]
        [SerializeField] private VideoClip noiseClip;
        [Tooltip("Material con el shader de chroma key. Si se deja vacio se usa el del RawImage.")]
        [SerializeField] private Material chromaKeyMaterial;

        [Header("Chroma key (fondo verde)")]
        [SerializeField] private Color keyColor = new Color(0f, 1f, 0f, 1f);
        [SerializeField, Range(0f, 1f)] private float threshold = 0.25f;
        [SerializeField, Range(0f, 0.5f)] private float smoothness = 0.15f;
        [SerializeField, Range(0f, 1f)] private float spill = 0.45f;
        [Tooltip("0 = distancia al color clave, 1 = canal dominante (mas robusto ante el decoder).")]
        [SerializeField] private int chromaMode = 1;

        [Header("Aspecto")]
        [Tooltip("Intensidad del ruido sobre la imagen del juego. 0 = invisible.")]
        [SerializeField, Range(0f, 1f)] private float intensity = 0.5f;
        [SerializeField, Min(0f)] private float fadeInDuration = 0.5f;
        [SerializeField] private Vector2Int textureSize = new Vector2Int(1920, 1080);

        private VideoPlayer _video;
        private RenderTexture _renderTexture;
        private Material _materialInstance;

        private void Awake()
        {
            if (!SetupOverlay())
                enabled = false;
        }

        private void Start()
        {
            if (_video == null || noiseClip == null) return;

            StartCoroutine(Play());
        }

        // Prepara el RawImage de la escena: material de chroma key, render
        // texture y reproductor. La jerarquia ya existe en la escena, esto solo
        // cablea lo que necesita el video.
        private bool SetupOverlay()
        {
            if (vhsImage == null)
                vhsImage = GetComponentInChildren<RawImage>(true);

            if (vhsImage == null)
            {
                Debug.LogError($"[VHSNoise] {name}: falta el RawImage que muestra el video. " +
                               "Ejecuta PrisaControlada/Configurar VHS Noise (HUD).", this);
                return false;
            }

            if (noiseClip == null)
            {
                Debug.LogError($"[VHSNoise] {name}: falta el VideoClip con el ruido. " +
                               "Ejecuta PrisaControlada/Configurar VHS Noise (HUD).", this);
                return false;
            }

            Material template = chromaKeyMaterial != null ? chromaKeyMaterial : vhsImage.material;
            if (template == null)
            {
                Debug.LogError($"[VHSNoise] {name}: falta el material de chroma key. " +
                               "Ejecuta PrisaControlada/Configurar VHS Noise (HUD).", this);
                return false;
            }

            // Instancia propia: el asset compartido nunca se modifica, asi este
            // overlay puede usar verde sin tocar el material azul.
            _materialInstance = Instantiate(template);
            _materialInstance.SetColor("_KeyColor", keyColor);
            _materialInstance.SetFloat("_Threshold", threshold);
            _materialInstance.SetFloat("_Smoothness", smoothness);
            _materialInstance.SetFloat("_Spill", spill);
            // El shader de chroma key no lee el color del RawImage, asi que la
            // intensidad y el fade van por _Opacity.
            _materialInstance.SetFloat("_Opacity", 0f);
            _materialInstance.SetFloat("_Mode", chromaMode);
            vhsImage.material = _materialInstance;

            _renderTexture = new RenderTexture(textureSize.x, textureSize.y, 0, RenderTextureFormat.ARGB32);
            _renderTexture.Create();

            // El VideoPlayer puede venir puesto en la escena (lo agrega la tool)
            // o se agrega aqui si no esta.
            _video = vhsImage.gameObject.GetComponent<VideoPlayer>();
            if (_video == null)
                _video = vhsImage.gameObject.AddComponent<VideoPlayer>();

            _video.playOnAwake = false;
            _video.waitForFirstFrame = true;
            _video.isLooping = true;
            _video.skipOnDrop = true;
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.targetTexture = _renderTexture;
            // El video trae audio pero solo interesa el ruido visual.
            _video.audioOutputMode = VideoAudioOutputMode.None;
            if (_video.clip == null)
                _video.clip = noiseClip;

            vhsImage.texture = _renderTexture;
            return true;
        }

        private IEnumerator Play()
        {
            _video.Prepare();

            // Si el codec no se puede preparar no se queda esperando siempre.
            float timeout = Time.realtimeSinceStartup + PrepareTimeout;
            while (!_video.isPrepared && Time.realtimeSinceStartup < timeout)
                yield return null;

            _video.frame = 0;
            _video.Play();

            float start = Time.time;
            while (Time.time - start < fadeInDuration)
            {
                float u = fadeInDuration > 0f ? (Time.time - start) / fadeInDuration : 1f;
                ApplyOpacity(u * intensity);
                yield return null;
            }

            ApplyOpacity(intensity);
        }

        // Aplica la intensidad del ruido. El material de chroma key es el que
        // controla el alpha final (el RawImage no alcanza, porque el shader no
        // lee el color de los vertices).
        private void ApplyOpacity(float value)
        {
            if (_materialInstance != null)
                _materialInstance.SetFloat("_Opacity", value);

            if (vhsImage != null)
                vhsImage.color = new Color(1f, 1f, 1f, value);
        }

        private void OnDestroy()
        {
            if (_video != null)
                _video.Stop();

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }

            if (_materialInstance != null)
                Destroy(_materialInstance);
        }
    }
}
