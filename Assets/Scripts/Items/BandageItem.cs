using PrisaControlada.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.Items
{
    // Venda medica: se agarra con F igual que cualquier otro pickup y queda en la
    // mano. Mientras esta en la mano, un click izquierdo la consume y cura 50 de
    // vida via PlayerHealth.Heal (igual que la tecla debug H, pero consumible).
    [RequireComponent(typeof(Rigidbody))]
    public class BandageItem : PickupItem
    {
        [Header("Bandage")]
        [SerializeField] private int healAmount = 50;

        private PlayerHealth _playerHealth;
        private InventoryController _inventory;

        public override void PickUp(Transform holder, Vector3 holdOffset)
        {
            base.PickUp(holder, holdOffset);

            // El HoldPoint es hijo de la camara del jugador; desde ahi buscamos
            // el PlayerHealth y el InventoryController en la raiz del jugador.
            if (_playerHealth == null)
                _playerHealth = holder.root.GetComponentInParent<PlayerHealth>();
            if (_inventory == null)
                _inventory = holder.root.GetComponentInParent<InventoryController>();
        }

        private void Update()
        {
            if (!IsHeld) return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Use();
        }

        private void Use()
        {
            if (_playerHealth == null)
                _playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (_inventory == null)
                _inventory = FindFirstObjectByType<InventoryController>();

            if (_playerHealth != null)
                _playerHealth.Heal(healAmount);

            Debug.Log($"<color=#77ff77>[BandageItem] Venda usada: +{healAmount} de vida. Item consumido.</color>");

            if (_inventory != null)
                _inventory.ConsumeHeldItem();
            else
                Destroy(gameObject);
        }
    }
}
