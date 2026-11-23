using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    //TP2 - Francisco Larrondo
    // Item de municion: al agarrarlo desaparece y suma balas externas (reserva)
    // al arma del jugador. No va al inventario ni a la mano.
    [RequireComponent(typeof(Collider))]
    public class AmmoPickup : MonoBehaviour, IInteractable
    {
        [Header("Ammo Pickup")]
        [Tooltip("Balas externas que suma al agarrarlo.")]
        [SerializeField] private int amount = 6;

        public int Amount => amount;

        public bool TryCollect()
        {
            WeaponController weapon = FindObjectOfType<WeaponController>();
            if (weapon == null)
            {
                Debug.LogWarning($"<color=#ff7777>[AmmoPickup] No hay WeaponController en la escena; no se sumaron balas.</color>");
                return false;
            }

            weapon.AddReserveAmmo(amount);
            return true;
        }

        // ---- IInteractable ----

        // La municion se levanta del suelo: lleva flecha de proximidad.
        public bool IsGrabbable => true;

        // La municion es interactiva desde cualquier parte de su collider.
        public bool IsValidTarget(Collider hitCollider)
        {
            return true;
        }

        // Al agarrarla suma balas a la reserva, se destruye y avisa al HUD.
        public bool TryInteract(InventoryController inventory, Collider hitCollider)
        {
            bool collected = TryCollect();
            if (collected)
            {
                Debug.Log($"<color=#77ff77>[AmmoPickup] Municion agarrada: +{amount} balas externas. Item destruido.</color>");
                Destroy(gameObject);
                inventory?.NotifyAmmoCollected();
            }

            return collected;
        }
    }
}