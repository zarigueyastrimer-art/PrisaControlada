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

        [Header("Scene References")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Image fill;
        [SerializeField] private Volume postFxVolume;

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

        [Header("Post FX")]
        [SerializeField] private Color postFxColor = new Color(1f, 0.15f, 0.15f, 1f);
        [SerializeField] private float postFxIntensity = 0.35f;
        [SerializeField] private float postFxSmoothTime = 0.2f;
        [SerializeField] private float vignetteBaseIntensity = 0f;
        [SerializeField] private float vignetteMaxIntensity = 0.6f;
        [SerializeField] private float vignetteBaseSmoothness = 0.271f;
        [SerializeField] private float vignetteMaxSmoothness = 0.755f;

        private InputAction _micMeterAction;
        private AudioClip _micClip;
        private float[] _samples;
        private bool _isActive;

        public float CurrentLevel => _level;
        public bool IsMicActive => _isActive;

        private float _barWidth = 500f;
        private float _level;

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

            ResolveSceneReferences();
        }

        private void ResolveSceneReferences()
        {
            if (canvas == null)
                canvas = transform.Find("MicNoiseMeter_Canvas")?.GetComponent<Canvas>();
            if (fill == null)
                fill = transform.Find("MicNoiseMeter_Canvas/Bar/Fill")?.GetComponent<Image>();
            if (postFxVolume == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null)
                    postFxVolume = cam.GetComponent<Volume>();
            }
        }

        private void Start()
        {
            if (fill != null)
            {
                RectTransform barRect = fill.rectTransform.parent as RectTransform;
                if (barRect != null)
                    _barWidth = barRect.sizeDelta.x;
            }

            _colorAdjustments = null;
            _vignette = null;
            if (postFxVolume != null)
            {
                try
                {
                    VolumeProfile profile = postFxVolume.profile;
                    if (profile != null)
                    {
                        profile.TryGet<ColorAdjustments>(out _colorAdjustments);
                        profile.TryGet<Vignette>(out _vignette);
                    }
                }
                catch (MissingReferenceException)
                {
                    _colorAdjustments = null;
                    _vignette = null;
                }
            }

            if (canvas != null)
                canvas.gameObject.SetActive(false);

            ResetPostFX();
        }

        private void OnDestroy()
        {
            StopMic();
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

            if (canvas != null)
                canvas.gameObject.SetActive(_isActive);

            if (_isActive)
            {
                StartMic();
            }
            else
            {
                StopMic();
                ResetPostFX();
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

        private void UpdateBar()
        {
            if (fill == null) return;

            Vector2 sd = fill.rectTransform.sizeDelta;
            sd.x = _barWidth * _level;
            fill.rectTransform.sizeDelta = sd;

            float thresholdLevel = Mathf.InverseLerp(minVolume, maxVolume, loudThreshold);
            fill.color = _level >= thresholdLevel ? loudColor : normalColor;

            float amount = _level >= thresholdLevel ? Mathf.InverseLerp(thresholdLevel, 1f, _level) : 0f;
            _postFxAmount = Mathf.SmoothDamp(_postFxAmount, amount, ref _postFxVelocity, postFxSmoothTime);

            if (_colorAdjustments != null)
                _colorAdjustments.colorFilter.value = Color.Lerp(Color.white, postFxColor, _postFxAmount * postFxIntensity);

            if (_vignette != null)
            {
                _vignette.intensity.value = Mathf.Lerp(vignetteBaseIntensity, vignetteMaxIntensity, _postFxAmount);
                _vignette.smoothness.value = Mathf.Lerp(vignetteBaseSmoothness, vignetteMaxSmoothness, _postFxAmount);
            }
        }

        private void ResetPostFX()
        {
            _postFxAmount = 0f;
            _postFxVelocity = 0f;

            if (_colorAdjustments != null)
                _colorAdjustments.colorFilter.value = Color.white;

            if (_vignette != null)
            {
                _vignette.intensity.value = vignetteBaseIntensity;
                _vignette.smoothness.value = vignetteBaseSmoothness;
            }
        }
    }
}