using System;
using PrisaControlada.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.Player
{
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

        private InputAction _interactAction;
        private InputAction _slot1Action;
        private InputAction _slot2Action;
        private InputAction _dropAction;

        private readonly PickupItem[] _slots = new PickupItem[SlotCount];
        private PickupItem _heldItem;
        private Transform _holdPoint;

        public PickupItem HeldItem => _heldItem;

        public event Action InventoryChanged;

        public PickupItem GetSlotItem(int index)
        {
            if (index < 0 || index >= SlotCount) return null;
            return _slots[index];
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
            if (_interactAction != null && _interactAction.WasPressedThisFrame())
                TryGrab();

            if (_slot1Action != null && _slot1Action.WasPressedThisFrame())
                UseSlot(0);

            if (_slot2Action != null && _slot2Action.WasPressedThisFrame())
                UseSlot(1);

            if (_dropAction != null && _dropAction.WasPressedThisFrame())
                ThrowHeldItem();
        }

        private void TryGrab()
        {
            if (viewCamera == null || _holdPoint == null) return;

            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupMask, QueryTriggerInteraction.Collide))
                return;

            DrawerController drawer = hit.collider.GetComponentInParent<DrawerController>();
            if (drawer != null)
            {
                drawer.ToggleDrawer(hit.collider);
                return;
            }

            DoorController door = hit.collider.GetComponentInParent<DoorController>();
            if (door != null)
            {
                if (door.IsDoorHit(hit.collider))
                    door.Toggle();
                return;
            }

            AmmoPickup ammo = hit.collider.GetComponentInParent<AmmoPickup>();
            if (ammo != null)
            {
                if (ammo.TryCollect())
                {
                    Debug.Log($"<color=#77ff77>[AmmoPickup] Municion agarrada: +{ammo.Amount} balas externas. Item destruido.</color>");
                    Destroy(ammo.gameObject);
                }
                InventoryChanged?.Invoke();
                return;
            }

            ShotgunAmmoPickup shellBox = hit.collider.GetComponentInParent<ShotgunAmmoPickup>();
            if (shellBox != null)
            {
                if (shellBox.TryCollect())
                {
                    Debug.Log($"<color=#77ff77>[ShotgunAmmoPickup] Caja de balas agarrada: +{shellBox.Amount} cartuchos. Item destruido.</color>");
                    Destroy(shellBox.gameObject);
                }
                InventoryChanged?.Invoke();
                return;
            }

            if (_heldItem != null) return;

            PickupItem item = hit.collider.GetComponentInParent<PickupItem>();
            if (item != null)
            {
                TakeToHand(item);
                InventoryChanged?.Invoke();
            }
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