using UnityEngine;

namespace PrisaControlada.Player
{
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(Camera))]
    public class BodycamShake : MonoBehaviour
    {
        [Header("Handheld Noise")]
        [SerializeField, Range(0f, 3f)] private float handheldAngular = 0.12f;
        [SerializeField, Range(0f, 0.05f)] private float handheldPositional = 0.002f;
        [SerializeField, Range(1f, 30f)] private float handheldFrequency = 7f;

        [Header("Movement Shake")]
        [SerializeField, Range(0f, 5f)] private float walkAngular = 0.7f;
        [SerializeField, Range(0f, 5f)] private float runAngular = 1.9f;
        [SerializeField, Range(0f, 0.2f)] private float walkPositional = 0.012f;
        [SerializeField, Range(0f, 0.2f)] private float runPositional = 0.035f;
        [SerializeField, Range(1f, 30f)] private float movementFrequency = 11f;
        [SerializeField, Range(0f, 1f)] private float rollFactor = 0.5f;
        [SerializeField, Range(0f, 3f)] private float speedBlend = 0.7f;

        [Header("Crouch Shake")]
        [SerializeField, Range(0f, 5f)] private float crouchAngular = 0.45f;
        [SerializeField, Range(0f, 0.2f)] private float crouchPositional = 0.008f;
        [SerializeField, Range(1f, 30f)] private float crouchFrequency = 8f;
        [SerializeField, Range(0f, 3f)] private float crouchMoveThreshold = 0.5f;

        [Header("Impact Shake")]
        [SerializeField, Range(0f, 20f)] private float impactAngular = 5f;
        [SerializeField, Range(0f, 0.5f)] private float impactPositional = 0.08f;
        [SerializeField, Range(1f, 30f)] private float impactFrequency = 22f;
        [SerializeField, Range(0.5f, 4f)] private float traumaExponent = 2.2f;
        [SerializeField, Range(0.1f, 5f)] private float recoverySpeed = 1.2f;

        [Header("Smoothing")]
        [SerializeField, Range(1f, 60f)] private float smoothing = 24f;

        private Transform _cam;
        private PlayerController _player;
        private float _seed;
        private float _trauma;
        private Vector3 _angularOffset;
        private Vector3 _positionalOffset;

        public float Trauma => _trauma;

        private void Awake()
        {
            _cam = transform;
            _player = GetComponentInParent<PlayerController>();
            _seed = Random.value * 1000f;
        }

        public void InduceStress(float stress)
        {
            _trauma = Mathf.Clamp01(_trauma + stress);
        }

        private void LateUpdate()
        {
            float move01 = GetMoveFactor();
            float trauma = Mathf.Pow(_trauma, traumaExponent);
            _trauma = Mathf.Clamp01(_trauma - recoverySpeed * Time.deltaTime);

            Vector3 targetAngular = SampleHandheld() + SampleMovement(move01) + SampleImpact(trauma, true);
            Vector3 targetPositional = SampleHandheld(false) + SampleMovement(move01, false) + SampleImpact(trauma, false);

            float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            _angularOffset = Vector3.Lerp(_angularOffset, targetAngular, blend);
            _positionalOffset = Vector3.Lerp(_positionalOffset, targetPositional, blend);

            Vector3 basePos = _cam.localPosition - _positionalOffset;
            _cam.localRotation *= Quaternion.Euler(_angularOffset);
            _cam.localPosition = basePos + _positionalOffset;
        }

        private Vector3 SampleHandheld(bool angular = true)
        {
            return new Vector3(
                Noise(0, handheldFrequency) * (angular ? handheldAngular : handheldPositional),
                Noise(1, handheldFrequency) * (angular ? handheldAngular : handheldPositional),
                Noise(2, handheldFrequency) * (angular ? handheldAngular : handheldPositional) * (angular ? rollFactor : 0.6f)
            );
        }

        private Vector3 SampleMovement(float move01, bool angular = true)
        {
            bool crouchMoving = _player != null
                && _player.IsCrouching
                && _player.HorizontalSpeed > crouchMoveThreshold;

            if (crouchMoving)
            {
                float amp = angular ? crouchAngular : crouchPositional;
                float freq = crouchFrequency * (0.5f + move01 * 0.5f);

                return new Vector3(
                    Noise(30, freq) * amp,
                    Noise(31, freq) * amp * 0.8f,
                    Noise(32, freq) * amp * rollFactor
                );
            }

            float amp2 = Mathf.Lerp(angular ? walkAngular : walkPositional, angular ? runAngular : runPositional, Mathf.Pow(move01, speedBlend));
            float freq2 = movementFrequency * (0.5f + move01 * 0.5f);

            return new Vector3(
                Noise(10, freq2) * amp2,
                Noise(11, freq2) * amp2 * 0.8f,
                Noise(12, freq2) * amp2 * rollFactor
            );
        }

        private Vector3 SampleImpact(float trauma, bool angular)
        {
            float amp = trauma * (angular ? impactAngular : impactPositional);

            return new Vector3(
                Noise(20, impactFrequency) * amp,
                Noise(21, impactFrequency) * amp * 0.8f,
                Noise(22, impactFrequency) * amp * 0.5f
            );
        }

        private float GetMoveFactor()
        {
            if (_player == null) return 0f;
            float runSpeed = Mathf.Max(_player.RunSpeed, 0.01f);
            return Mathf.Clamp01(_player.HorizontalSpeed / runSpeed);
        }

        private float Noise(int axis, float frequency)
        {
            return Mathf.PerlinNoise(_seed + axis * 20f + Time.time * frequency, axis) * 2f - 1f;
        }
    }
}