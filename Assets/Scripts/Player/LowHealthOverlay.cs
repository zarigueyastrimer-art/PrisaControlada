using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    public class LowHealthOverlay : MonoBehaviour
    {
        [SerializeField] private RawImage overlay;
        [SerializeField] private int lowHealthThreshold = 25;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.5f;
        [SerializeField, Min(0.1f)] private float exponent = 1.8f;
        [SerializeField, Range(0f, 32f)] private float blurSize = 8f;

        private PlayerHealth _health;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
        }

        private void Start()
        {
            if (overlay != null)
                overlay.material.SetFloat("_BlurSize", blurSize);
        }

        private void Update()
        {
            if (overlay == null || _health == null) return;

            float max = _health.MaxHealth;
            if (max <= lowHealthThreshold)
            {
                overlay.color = new Color(1f, 1f, 1f, maxAlpha);
                return;
            }

            float t = Mathf.Clamp01(1f - (float)(_health.CurrentHealth - lowHealthThreshold) / (max - lowHealthThreshold));
            float alpha = maxAlpha * Mathf.Pow(t, exponent);
            overlay.color = new Color(1f, 1f, 1f, alpha);
        }
    }
}