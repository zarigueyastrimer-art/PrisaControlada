using UnityEngine;

namespace PrisaControlada.Player
{
    public class RedFilterController : MonoBehaviour
    {
        [SerializeField] private int lowHealthThreshold = 25;
        [SerializeField, Range(0f, 1f)] private float maxIntensity = 1f;
        [SerializeField, Min(0.1f)] private float exponent = 1.8f;
        [SerializeField, Range(0.1f, 10f)] private float redSensitivity = 4f;
        [SerializeField, Range(0f, 1f)] private float redStrength = 1f;
        [SerializeField, Min(1f)] private float minGamma = 2f;
        [SerializeField, Min(0f)] private float maxBlur = 0.55f;
        [SerializeField, Min(0f)] private float blurRadius = 7f;

        private PlayerHealth _health;
        private bool _wasActive;
        private bool _forceNeutral;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _forceNeutral = false;
        }

        public void ForceNeutralLook(bool force = true)
        {
            _forceNeutral = force;
            if (_forceNeutral)
                RedFilterVolumeComponent.SetNeutral();
        }

        private void Update()
        {
            if (_forceNeutral)
            {
                RedFilterVolumeComponent.SetNeutral();
                return;
            }

            if (_health == null) return;

            float max = _health.MaxHealth;
            float intensity;

            if (max <= lowHealthThreshold)
            {
                intensity = maxIntensity;
            }
            else
            {
                float t = Mathf.Clamp01(1f - (float)(_health.CurrentHealth - lowHealthThreshold) / (max - lowHealthThreshold));
                intensity = maxIntensity * Mathf.Pow(t, exponent);
            }

            RedFilterVolumeComponent.s_Intensity = intensity;
            RedFilterVolumeComponent.s_RedSensitivity = redSensitivity;
            RedFilterVolumeComponent.s_RedStrength = redStrength;

            float hpT = Mathf.Clamp01(1f - (float)_health.CurrentHealth / max);
            float gamma = Mathf.Lerp(1f, minGamma, Mathf.Pow(hpT, exponent));
            RedFilterVolumeComponent.s_Gamma = gamma;

            float blurAmount = maxBlur * Mathf.Pow(hpT, exponent);
            RedFilterVolumeComponent.s_BlurAmount = blurAmount;
            RedFilterVolumeComponent.s_BlurRadius = blurRadius;

            if (RedFilterVolumeComponent.IsEffectActive() && !_wasActive)
            {
                Debug.Log($"[RedFilter] activado, intensidad {intensity:F2}, gamma {gamma:F2}, blur {blurAmount:F2}");
                _wasActive = true;
            }
            else if (!RedFilterVolumeComponent.IsEffectActive())
            {
                _wasActive = false;
            }
        }
    }
}