using System;
using PrisaControlada.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.Player
{
    //TP2 - Francisco Larrondo
    public class InventoryController : MonoBehaviour
    {
        private const int SlotCount = 2;

        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private InputActionAsset inputActions;

        [Header("Pickup Settings")]
        [SerializeField] private float pickupRange = 2f;
        [SerializeField] private LayerMask pickupMask = -1;
        [SerializeField] private Vector3 holdOffset = new Vector3(0.35f, -0.35f, 0.6f);
        [SerializeField] private float throwForce = 2f;

        [Header("Audio")]
        [SerializeField] private AudioSource pickupAudioSource;
        [SerializeField] private AudioClip pickupClip;
        [SerializeField] private AudioClip pickupGunClip;
        [SerializeField] private AudioClip equipClip;
        [SerializeField, Range(0f, 1f)] private float pickupVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float equipVolume = 1f;

        private InputAction _interactAction;
        private InputAction _slot1Action;
        private InputAction _slot2Action;
        private InputAction _dropAction;

        // Composicion: el inventario "tiene" un array de PickupItem (2 slots),
        // no es un PickupItem. Los objetos guardados son de otra clase.
        private readonly PickupItem[] _slots = new PickupItem[SlotCount];
        private PickupItem _heldItem;
        private Transform _holdPoint;
        private bool _interactBlocked;

        public PickupItem HeldItem => _heldItem;

        // Mismo esquema que PlayerHealth.OnDamaged: el HUD se suscribe y esta
        // clase solo invoca, sin saber quien esta escuchando.
        public event Action InventoryChanged;

        public void SetInteractBlocked(bool blocked)
        {
            _interactBlocked = blocked;
        }

        public void ConsumeHeldItem()
        {
            if (_heldItem == null) return;

            PickupItem item = _heldItem;
            _heldItem = null;
            Destroy(item.gameObject);
            InventoryChanged?.Invoke();
        }

        public PickupItem GetSlotItem(int index)
        {
            if (index < 0 || index >= SlotCount) return null;
            return _slots[index];
        }

        // ---- API usada por los objetos que implementan IInteractable ----

        // Lo llama PickupItem.TryInteract(). Si la mano esta ocupada no agarra
        // nada y devuelve false, para que el jugador sepa que tiene que vaciar
        // la mano antes (el objeto queda en el mundo sin cambiar).
        public bool TryGrabItem(PickupItem item)
        {
            if (item == null || _heldItem != null) return false;

            TakeToHand(item);
            InventoryChanged?.Invoke();
            PlayPickupSound(item is WeaponPickup || item is GlockGroundPickup);
            return true;
        }

        // Lo llaman los pickups de municion. Sumar balas no mueve los slots,
        // pero si cambia el HUD de reserva, por eso se notifica igual.
        public void NotifyAmmoCollected()
        {
            PlayPickupSound(false);
            InventoryChanged?.Invoke();
        }

        private void PlayPickupSound(bool isWeapon)
        {
            if (pickupAudioSource == null) return;

            AudioClip clip = isWeapon ? pickupGunClip : pickupClip;
            if (clip == null) return;

            pickupAudioSource.clip = clip;
            pickupAudioSource.loop = false;
            pickupAudioSource.volume = pickupVolume;
            pickupAudioSource.PlayOneShot(clip, pickupVolume);
        }

        private void PlayEquipSound()
        {
            if (pickupAudioSource == null) return;
            if (equipClip == null) return;

            pickupAudioSource.clip = equipClip;
            pickupAudioSource.loop = false;
            pickupAudioSource.volume = equipVolume;
            pickupAudioSource.PlayOneShot(equipClip, equipVolume);
        }

        private void Awake()
        {
            if (viewCamera == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) viewCamera = cam;
            }

            if (viewCamera != null)
            {
                _holdPoint = new GameObject("HoldPoint").transform;
                _holdPoint.SetParent(viewCamera.transform, false);
                _holdPoint.localPosition = holdOffset;
            }

            if (pickupAudioSource == null)
                pickupAudioSource = GetComponent<AudioSource>();
            if (pickupAudioSource == null)
                pickupAudioSource = gameObject.AddComponent<AudioSource>();

            if (inputActions != null)
            {
                _interactAction = inputActions.FindAction("Interact");
                _slot1Action = inputActions.FindAction("Slot1");
                _slot2Action = inputActions.FindAction("Slot2");
                _dropAction = inputActions.FindAction("Drop");
            }
        }

        private void OnEnable()
        {
            _interactAction?.Enable();
            _slot1Action?.Enable();
            _slot2Action?.Enable();
            _dropAction?.Enable();
        }

        private void OnDisable()
        {
            _interactAction?.Disable();
            _slot1Action?.Disable();
            _slot2Action?.Disable();
            _dropAction?.Disable();
        }

        private void Update()
        {
            if (_interactBlocked) return;

            if (_interactAction != null && _interactAction.WasPressedThisFrame())
                TryGrab();

            if (_slot1Action != null && _slot1Action.WasPressedThisFrame())
                UseSlot(0);

            if (_slot2Action != null && _slot2Action.WasPressedThisFrame())
                UseSlot(1);

            if (_dropAction != null && _dropAction.WasPressedThisFrame())
                ThrowHeldItem();

            float scroll = Mouse.current != null ? Mouse.current.scroll.y.ReadValue() : 0f;
            if (scroll > 0f)
                UseSlot(0);
            else if (scroll < 0f)
                UseSlot(1);
        }

        // Miramos lo que tenemos enfrente y le preguntamos al objeto si es
        // interactuable. Antes aca habia una cadena de 9 if, uno por cada tipo
        // de objeto (cajon, nota, audio, puerta, 4 municiones y pickup); ahora
        // el tipo concreto no se conoce aca, solo la interfaz IInteractable.
        // La usan tres lugares: aca para agarrar, la mira para saber cuando
        // cerrarse y el resaltador para el borde y la flecha.
        // Si aparece un tipo nuevo de objeto este metodo no se toca: alcanza
        // con que la clase implemente la interfaz.
        private void TryGrab()
        {
            if (viewCamera == null || _holdPoint == null) return;

            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupMask, QueryTriggerInteraction.Collide))
                return;

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null) return;

            // El contrato se valida siempre desde un unico punto: primero se
            // pregunta si la parte impactada es interactiva y recien despues
            // se intenta consumar la interaccion.
            if (!interactable.IsValidTarget(hit.collider))
                return;

            interactable.TryInteract(this, hit.collider);
        }

        private void UseSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return;

            PickupItem inSlot = _slots[index];

            if (_heldItem != null)
            {
                if (_heldItem.IsBusy)
                {
                    Debug.Log("<color=#ff7777>[Inventory] No podes guardar el arma mientras disparas/recargas/apuntas.</color>");
                    return;
                }
                // No swapping: a held item can only be stored in an empty slot.
                if (inSlot == null)
                {
                    _slots[index] = _heldItem;
                    _heldItem = null;
                    HideItem(_slots[index]);
                }
            }
            else if (inSlot != null)
            {
                _slots[index] = null;
                TakeToHand(inSlot);
                if (inSlot is WeaponPickup || inSlot is GlockGroundPickup)
                    PlayEquipSound();
            }

            InventoryChanged?.Invoke();
        }

        private void TakeToHand(PickupItem item)
        {
            item.gameObject.SetActive(true);
            _heldItem = item;
            item.PickUp(_holdPoint, Vector3.zero);
        }

        private void HideItem(PickupItem item)
        {
            item.OnStoredInSlot();
            item.gameObject.SetActive(false);
        }

        private void ThrowHeldItem()
        {
            if (_heldItem == null) return;

            if (_heldItem.IsBusy)
            {
                Debug.Log("<color=#ff7777>[Inventory] No podes tirar el arma mientras disparas/recargas/apuntas.</color>");
                return;
            }

            PickupItem item = _heldItem;
            _heldItem = null;
            item.Throw(viewCamera.transform.forward * throwForce + Vector3.up * throwForce * 0.2f);

            InventoryChanged?.Invoke();
        }
    }
}