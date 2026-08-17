using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    // Item de municion: al agarrarlo desaparece y suma balas externas (reserva)
    // al arma del jugador. No va al inventario ni a la mano.
    [RequireComponent(typeof(Collider))]
    public class AmmoPickup : MonoBehaviour
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
    }
}