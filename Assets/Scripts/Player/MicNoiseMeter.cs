using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    public class MicNoiseMeter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Mic Settings")]
        [SerializeField] private int sampleRate = 44100;
        [SerializeField] private float sampleWindowSeconds = 0.1f;

        [Header("Thresholds")]
        [SerializeField] private float loudThreshold = 0.08f;
        [SerializeField] private float minVolume = 0.002f;
        [SerializeField] private float maxVolume = 0.2f;

        [Header("UI")]
        [SerializeField] private Color normalColor = new Color(0.4f, 0.9f, 0.4f, 1f);
        [SerializeField] private Color loudColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField] private Vector2 barSize = new Vector2(500f, 36f);
        [SerializeField] private float barYOffset = 200f;

        [Header("Post FX")]
        [SerializeField] private Color postFxColor = new Color(1f, 0.15f, 0.15f, 1f);
        [SerializeField] private float postFxIntensity = 0.35f;
        [SerializeField] private float postFxSmoothTime = 0.2f;
        [SerializeField] private float vignetteBaseIntensity = 0.4f;
        [SerializeField] private float vignetteMaxIntensity = 0.6f;
        [SerializeField] private float vignetteBaseSmoothness = 0.271f;
        [SerializeField] private float vignetteMaxSmoothness = 0.755f;

        private InputAction _micMeterAction;
        private AudioClip _micClip;
        private float[] _samples;
        private bool _isActive;

        private GameObject _root;
        private Image _fill;
        private float _level;

        private Volume _volume;
        private ColorAdjustments _colorAdjustments;
        private Vignette _vignette;
        private float _postFxAmount;
        private float _postFxVelocity;

        private void Awake()
        {
            if (inputActions == null)
            {
                PlayerController controller = FindObjectOfType<PlayerController>();
                if (controller != null)
                    inputActions = controller.InputActions;
            }

            if (inputActions != null)
            {
                _micMeterAction = inputActions.FindAction("MicMeter");
                _micMeterAction?.Enable();
            }
        }

        private void OnDestroy()
        {
            StopMic();
            TeardownPostFX();
        }

        private void Update()
        {
            if (_micMeterAction != null && _micMeterAction.WasPressedThisFrame())
                Toggle();

            if (_isActive)
            {
                UpdateLevel();
                UpdateBar();
            }
        }

        private void Toggle()
        {
            _isActive = !_isActive;

            if (_isActive)
            {
                StartMic();
                CreateUI();
                SetupPostFX();
            }
            else
            {
                StopMic();
                DestroyUI();
                TeardownPostFX();
            }
        }

        private void StartMic()
        {
            if (Microphone.devices.Length == 0) return;

            _micClip = Microphone.Start(null, true, 1, sampleRate);
            if (_micClip == null) return;

            int windowSamples = Mathf.CeilToInt(sampleRate * sampleWindowSeconds);
            _samples = new float[windowSamples];
        }

        private void StopMic()
        {
            if (_micClip != null)
            {
                Microphone.End(null);
                _micClip = null;
                _samples = null;
            }
        }

        private void UpdateLevel()
        {
            if (_micClip == null || _samples == null) return;

            int position = Microphone.GetPosition(null);
            if (position <= 0) return;

            int start = Mathf.Max(0, position - _samples.Length);
            _micClip.GetData(_samples, start);

            float sum = 0f;
            foreach (float sample in _samples)
                sum += sample * sample;

            float rms = Mathf.Sqrt(sum / _samples.Length);
            float target = Mathf.InverseLerp(minVolume, maxVolume, rms);
            _level = Mathf.MoveTowards(_level, target, Time.deltaTime * 8f);
        }

        private void CreateUI()
        {
            if (_root != null) return;

            GameObject canvasGo = new GameObject("MicNoiseMeter_Canvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject barGo = new GameObject("Bar");
            RectTransform barRect = barGo.AddComponent<RectTransform>();
            barGo.transform.SetParent(canvasGo.transform, false);
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, barYOffset);
            barRect.sizeDelta = barSize;

            Image bg = barGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);

            GameObject fillGo = new GameObject("Fill");
            RectTransform fillRect = fillGo.AddComponent<RectTransform>();
            fillGo.transform.SetParent(barRect, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = new Vector2(barSize.x, barSize.y);

            _fill = fillGo.AddComponent<Image>();
            _fill.color = normalColor;

            _root = canvasGo;
        }

        private void UpdateBar()
        {
            if (_fill == null) return;

            _fill.rectTransform.sizeDelta = new Vector2(barSize.x * _level, barSize.y);
            float thresholdLevel = Mathf.InverseLerp(minVolume, maxVolume, loudThreshold);
            _fill.color = _level >= thresholdLevel ? loudColor : normalColor;

            float targetFx = Mathf.InverseLerp(minVolume, maxVolume, loudThreshold);
            float amount = _level >= targetFx ? Mathf.InverseLerp(targetFx, 1f, _level) : 0f;
            _postFxAmount = Mathf.SmoothDamp(_postFxAmount, amount, ref _postFxVelocity, postFxSmoothTime);

            if (_colorAdjustments != null)
            {
                _colorAdjustments.colorFilter.value = Color.Lerp(Color.white, postFxColor, _postFxAmount * postFxIntensity);
            }

            if (_vignette != null)
            {
                _vignette.intensity.value = Mathf.Lerp(vignetteBaseIntensity, vignetteMaxIntensity, _postFxAmount);
                _vignette.smoothness.value = Mathf.Lerp(vignetteBaseSmoothness, vignetteMaxSmoothness, _postFxAmount);
            }
        }

        private void SetupPostFX()
        {
            if (_volume != null) return;

            Camera cam = GetComponentInChildren<Camera>();
            if (cam == null) return;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _colorAdjustments = profile.Add<ColorAdjustments>(true);
            _colorAdjustments.colorFilter.value = Color.white;
            _colorAdjustments.saturation.value = 0f;

            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.value = vignetteBaseIntensity;
            _vignette.smoothness.value = vignetteBaseSmoothness;
            _vignette.color.value = postFxColor;

            _volume = cam.gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.profile = profile;
        }

        private void TeardownPostFX()
        {
            if (_volume != null)
            {
                VolumeProfile profile = _volume.profile;
                if (profile != null)
                    Destroy(profile);

                Destroy(_volume);
                _volume = null;
                _colorAdjustments = null;
                _vignette = null;
                _postFxAmount = 0f;
            }
        }

        private void DestroyUI()
        {
            if (_root != null)
            {
                Destroy(_root);
                _root = null;
            }

            _fill = null;
        }
    }
}