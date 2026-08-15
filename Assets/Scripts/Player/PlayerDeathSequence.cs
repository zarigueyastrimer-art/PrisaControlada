using System.Collections;
using UnityEngine;

namespace PrisaControlada.Player
{
    [RequireComponent(typeof(PlayerHealth))]
    [DefaultExecutionOrder(500)]
    public class PlayerDeathSequence : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private BodycamShake bodycamShake;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Transform cam;
        [SerializeField] private DeathFXController deathFX;

        [Header("Timing")]
        [SerializeField] private float staticDuration = 0.4f;
        [SerializeField] private float kneelDuration = 0.6f;
        [SerializeField] private float kneelHoldDuration = 0.3f;
        [SerializeField] private float fallDuration = 0.9f;
        [SerializeField] private float bounceDuration = 0.9f;
        [SerializeField, Min(1f)] private float bounceCount = 3f;
        [SerializeField, Min(0f)] private float bounceHeight = 0.09f;

        [Header("Kneel Impact")]
        [SerializeField] private float kneelBounceDuration = 0.3f;
        [SerializeField, Min(0f)] private float kneelBounceHeight = 0.05f;
        [SerializeField, Min(1f)] private float kneelBounceCount = 1f;
        [SerializeField, Min(0f)] private float kneelShakeAmount = 2.5f;

        [Header("Poses")]
        [SerializeField] private float standingEyeHeight = 1.6f;
        [SerializeField] private float kneelHeight = 0.85f;
        [SerializeField] private float groundHeight = 0.15f;
        [SerializeField] private float kneelForwardOffset = 0.667f;
        [SerializeField] private float groundForwardOffset = 0.714f;
        [SerializeField] private float kneelPitch = -25f;
        [SerializeField] private float faceDownPitch = -90f;
        [SerializeField] private float fallRoll = 0f;
        [SerializeField] private bool fallSideRight = true;

        public event System.Action OnCameraBroken;

        public bool IsDying { get; private set; }

        private PlayerHealth _health;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();

            if (playerController == null)
                playerController = GetComponent<PlayerController>();

            if (cameraPivot == null)
            {
                Transform found = transform.Find("CameraPivot");
                if (found != null)
                    cameraPivot = found;
            }

            if (cam == null && cameraPivot != null)
            {
                for (int i = 0; i < cameraPivot.childCount; i++)
                {
                    Transform child = cameraPivot.GetChild(i);
                    if (child.GetComponent<Camera>() != null)
                    {
                        cam = child;
                        break;
                    }
                }
            }

            if (playerCamera == null && cam != null)
                playerCamera = cam.GetComponent<PlayerCamera>();
            if (bodycamShake == null && cam != null)
                bodycamShake = cam.GetComponent<BodycamShake>();
        }

        private void Start()
        {
            if (cameraPivot != null && standingEyeHeight <= 0f)
                standingEyeHeight = cameraPivot.localPosition.y;

            _health.OnDied += StartDeath;
        }

        private void OnDestroy()
        {
            if (_health != null)
                _health.OnDied -= StartDeath;
        }

        public void StartDeath()
        {
            if (IsDying) return;
            IsDying = true;
            StartCoroutine(DeathSequence());
        }

        private IEnumerator DeathSequence()
        {
            if (playerController != null) playerController.SetControlEnabled(false);
            if (playerCamera != null) playerCamera.enabled = false;
            if (bodycamShake != null) bodycamShake.enabled = false;

            yield return new WaitForSeconds(staticDuration);

            yield return AnimatePose(kneelHeight, kneelForwardOffset, kneelPitch, 0f, kneelDuration);
            yield return PhaseKneelImpact();
            yield return new WaitForSeconds(kneelHoldDuration);
            yield return AnimatePose(groundHeight, groundForwardOffset, faceDownPitch, fallRoll * (fallSideRight ? 1f : -1f), fallDuration);

            OnCameraBroken?.Invoke();
            if (deathFX != null)
                deathFX.PlaySequence();

            yield return PhaseImpactBounce();
        }

        private IEnumerator AnimatePose(float toHeight, float toForward, float toPitch, float toRoll, float duration)
        {
            float fromHeight = cameraPivot != null ? cameraPivot.localPosition.y : 0f;
            float fromForward = cameraPivot != null ? cameraPivot.localPosition.z : 0f;
            Quaternion fromRot = cam != null ? cam.localRotation : Quaternion.identity;
            Quaternion toRot = Quaternion.Euler(toPitch, 0f, toRoll);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));

                if (cameraPivot != null)
                {
                    Vector3 lp = cameraPivot.localPosition;
                    cameraPivot.localPosition = new Vector3(
                        lp.x,
                        Mathf.Lerp(fromHeight, toHeight, p),
                        Mathf.Lerp(fromForward, toForward, p));
                }

                if (cam != null)
                    cam.localRotation = Quaternion.Slerp(fromRot, toRot, p);

                yield return null;
            }

            if (cameraPivot != null)
            {
                Vector3 lp = cameraPivot.localPosition;
                cameraPivot.localPosition = new Vector3(lp.x, toHeight, toForward);
            }

            if (cam != null)
                cam.localRotation = toRot;
        }

        private IEnumerator PhaseImpactBounce()
        {
            float start = Time.time;
            float baseY = cameraPivot != null ? cameraPivot.localPosition.y : groundHeight;
            Quaternion faceDown = cam != null ? cam.localRotation : Quaternion.Euler(faceDownPitch, 0f, 0f);

            while (Time.time - start < bounceDuration)
            {
                float u = Mathf.Clamp01((Time.time - start) / bounceDuration);
                float decay = 1f - u;
                float y = baseY + Mathf.Abs(Mathf.Sin(u * Mathf.PI * bounceCount)) * bounceHeight * decay;

                if (cameraPivot != null)
                {
                    Vector3 lp = cameraPivot.localPosition;
                    cameraPivot.localPosition = new Vector3(lp.x, y, lp.z);
                }

                if (cam != null)
                {
                    // The camera pops off the ground and flips from face-down to facing forward.
                    float flip = Mathf.Clamp01(u * 2.2f);
                    Quaternion target = Quaternion.Slerp(faceDown, Quaternion.identity, flip);
                    float wobble = Mathf.Sin(u * Mathf.PI * bounceCount) * 6f * decay;
                    cam.localRotation = Quaternion.Euler(target.eulerAngles.x, 0f, target.eulerAngles.z + wobble);
                }

                yield return null;
            }

            if (cameraPivot != null)
            {
                Vector3 lp = cameraPivot.localPosition;
                cameraPivot.localPosition = new Vector3(lp.x, baseY, lp.z);
            }

            if (cam != null)
                cam.localRotation = Quaternion.identity;
        }

        private IEnumerator PhaseKneelImpact()
        {
            float start = Time.time;
            float baseY = cameraPivot != null ? cameraPivot.localPosition.y : kneelHeight;
            Quaternion baseRot = cam != null ? cam.localRotation : Quaternion.identity;

            while (Time.time - start < kneelBounceDuration)
            {
                float u = Mathf.Clamp01((Time.time - start) / kneelBounceDuration);
                float decay = 1f - u;
                float y = baseY + Mathf.Abs(Mathf.Sin(u * Mathf.PI * kneelBounceCount)) * kneelBounceHeight * decay;

                if (cameraPivot != null)
                {
                    Vector3 lp = cameraPivot.localPosition;
                    cameraPivot.localPosition = new Vector3(lp.x, y, lp.z);
                }

                if (cam != null)
                {
                    float wobble = Mathf.Sin(u * Mathf.PI * kneelBounceCount) * kneelShakeAmount * decay;
                    Vector3 e = baseRot.eulerAngles;
                    cam.localRotation = Quaternion.Euler(e.x + wobble * 0.5f, 0f, e.z + wobble);
                }

                yield return null;
            }

            if (cameraPivot != null)
            {
                Vector3 lp = cameraPivot.localPosition;
                cameraPivot.localPosition = new Vector3(lp.x, baseY, lp.z);
            }

            if (cam != null)
                cam.localRotation = baseRot;
        }
    }
}