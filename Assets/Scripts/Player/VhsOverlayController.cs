using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PrisaControlada.Player
{
    [DisallowMultipleComponent]
    public class VhsOverlayController : MonoBehaviour
    {
        [Header("Video")]
        [SerializeField] private RawImage vhsImage;
        [SerializeField] private VideoClip vhsClip;
        [SerializeField] private Material chromaKeyMaterial;
        [SerializeField] private float fadeInDuration = 0.5f;

        [Header("Chroma Key (default: blue screen)")]
        [SerializeField] private Color keyColor = new Color(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float threshold = 0.25f;
        [SerializeField, Range(0f, 0.5f)] private float smoothness = 0.15f;
        [SerializeField, Range(0f, 1f)] private float spill = 0.45f;
        [SerializeField, Range(0f, 1f)] private float opacity = 1f;
        [SerializeField] private int chromaMode = 1;

        private VideoPlayer _video;
        private RenderTexture _renderTexture;

        private void Awake()
        {
            if (vhsImage == null) return;

            vhsImage.color = new Color(1f, 1f, 1f, 0f);
            if (chromaKeyMaterial != null)
            {
                // Own instance so we never mutate the shared asset, and so the
                // key color below always applies no matter what the asset holds.
                Material mat = Instantiate(chromaKeyMaterial);
                mat.SetColor("_KeyColor", keyColor);
                mat.SetFloat("_Threshold", threshold);
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Spill", spill);
                mat.SetFloat("_Opacity", opacity);
                mat.SetFloat("_Mode", chromaMode);
                vhsImage.material = mat;

                Debug.Log($"[VHS] Chroma key aplicado: color {keyColor}, mode {chromaMode}, threshold {threshold}, smoothness {smoothness}");
            }

            _video = vhsImage.gameObject.GetComponent<VideoPlayer>();
            if (_video == null)
                _video = vhsImage.gameObject.AddComponent<VideoPlayer>();

            _video.playOnAwake = false;
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.isLooping = true;
            _video.skipOnDrop = true;
            _video.clip = vhsClip;

            _renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
            _renderTexture.Create();
            _video.targetTexture = _renderTexture;
            vhsImage.texture = _renderTexture;
        }

        private void Start()
        {
            StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            if (_video == null || vhsClip == null) yield break;

            _video.Prepare();
            while (!_video.isPrepared)
                yield return null;

            _video.frame = 0;
            _video.Play();

            float start = Time.time;
            while (Time.time - start < fadeInDuration)
            {
                float u = (Time.time - start) / fadeInDuration;
                vhsImage.color = new Color(1f, 1f, 1f, u);
                yield return null;
            }
            vhsImage.color = Color.white;
        }

        private void OnDestroy()
        {
            if (_renderTexture != null)
                _renderTexture.Release();
        }
    }
}