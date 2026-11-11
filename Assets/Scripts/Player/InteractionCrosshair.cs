using PrisaControlada.Items;
using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    public class InteractionCrosshair : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Image crosshairImage;

        [Header("Settings")]
        [SerializeField] private float range = 2f;
        [SerializeField] private float fadeSpeed = 8f;
        [SerializeField] private LayerMask rayMask = -1;

        private CanvasGroup _canvasGroup;
        private float _alpha;

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            _alpha = 0f;
            ApplyAlpha(0f);
        }

        private void Update()
        {
            if (viewCamera == null)
            {
                _alpha = 0f;
                ApplyAlpha(0f);
                return;
            }

            float target = IsLookingAtInteractable() ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, target, fadeSpeed * Time.deltaTime);
            ApplyAlpha(_alpha);
        }

        private bool IsLookingAtInteractable()
        {
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, range, rayMask, QueryTriggerInteraction.Collide))
                return false;

            DrawerController drawer = hit.collider.GetComponentInParent<DrawerController>();
            if (drawer != null)
                return drawer.IsDrawerHit(hit.collider);

            DoorController door = hit.collider.GetComponentInParent<DoorController>();
            if (door != null)
                return door.IsDoorHit(hit.collider);

            if (hit.collider.GetComponentInParent<AmmoPickup>() != null)
                return true;

            if (hit.collider.GetComponentInParent<ShotgunAmmoPickup>() != null)
                return true;

            if (hit.collider.GetComponentInParent<GlockAmmoPickup>() != null)
                return true;

            if (hit.collider.GetComponentInParent<GlockMagazinePickup>() != null)
                return true;

            if (hit.collider.GetComponentInParent<NoteItem>() != null)
                return true;

            if (hit.collider.GetComponentInParent<AudioTrigger>() != null)
                return true;

            return hit.collider.GetComponentInParent<PickupItem>() != null;
        }

        private void ApplyAlpha(float alpha)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = alpha;
            }
            else if (crosshairImage != null)
            {
                Color color = crosshairImage.color;
                color.a = alpha;
                crosshairImage.color = color;
            }
        }
    }
}