using PrisaControlada.Items;
using UnityEngine;

namespace PrisaControlada.Player
{
    // Mira de 4 esquinas ("mira abierta"): queda siempre en pantalla y, cuando
    // apuntas a algo interactuable, las 4 esquinas se deslizan hacia el centro
    // y se "cierran" de forma suave (el mismo tamano de "mira cerrada").
    //
    // No se achica ni se cambia el sprite: cada esquina es un RectTransform
    // proprio que se mueve 20px por eje hacia el centro (distanciaCierre).
    public class InteractionCrosshair : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera viewCamera;

        // Orden: sup izq, sup der, inf izq, inf der.
        [SerializeField] private RectTransform[] esquinas;

        [Header("Settings")]
        [SerializeField] private float range = 2f;
        [SerializeField] private LayerMask rayMask = -1;

        [Header("Cierre")]
        [SerializeField, Min(0f)] private float duracionCierre = 0.15f;
        [SerializeField, Min(0f)] private float distanciaCierre = 20f;

        private CanvasGroup _canvasGroup;
        private Vector2[] _posicionesBase;
        private float _cierre;

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

            // La mira siempre visible (antes se ocultaba hasta mirar algo).
            _canvasGroup.alpha = 1f;

            CapturarPosicionesBase();
        }

        private void Update()
        {
            // Por las dudas: nada la debe ocultar.
            if (_canvasGroup != null && _canvasGroup.alpha != 1f)
                _canvasGroup.alpha = 1f;

            bool objetivo = viewCamera != null && IsLookingAtInteractable();
            float velocidad = duracionCierre > 0f ? 1f / duracionCierre : 1000f;

            _cierre = Mathf.MoveTowards(_cierre, objetivo ? 1f : 0f, velocidad * Time.deltaTime);

            // Smoothstep: arranca y frena suave, sin rebote.
            AplicarCierre(Mathf.SmoothStep(0f, 1f, _cierre));
        }

        private void CapturarPosicionesBase()
        {
            if (esquinas == null || esquinas.Length == 0)
            {
                _posicionesBase = null;
                return;
            }

            if (_posicionesBase == null || _posicionesBase.Length != esquinas.Length)
                _posicionesBase = new Vector2[esquinas.Length];

            for (int i = 0; i < esquinas.Length; i++)
            {
                if (esquinas[i] != null)
                    _posicionesBase[i] = esquinas[i].anchoredPosition;
            }
        }

        // Cada esquina va en diagonal hacia el centro: -signo(x) en X y
        // -signo(y) en Y (así funciona igual si reubicás una esquina a mano).
        private void AplicarCierre(float k)
        {
            if (esquinas == null || _posicionesBase == null) return;

            float avance = distanciaCierre * k;

            for (int i = 0; i < esquinas.Length; i++)
            {
                RectTransform rt = esquinas[i];
                if (rt == null) continue;

                Vector2 basePos = _posicionesBase[i];
                Vector2 dir = new Vector2(-Mathf.Sign(basePos.x), -Mathf.Sign(basePos.y));
                rt.anchoredPosition = basePos + dir * avance;
            }
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
    }
}
