using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    //TP2 - Francisco Larrondo
    // Cargador expulsado de la Glock al recargar: al agarrarlo devuelve sus balas
    // a la reserva (igual que las balas de la recamara del revolver). No va al
    // inventario ni a la mano; es un item fisico que rebota y se puede recoger.
    [RequireComponent(typeof(Collider))]
    public class GlockMagazinePickup : MonoBehaviour, IInteractable
    {
        [Header("Glock Magazine")]
        [Tooltip("Balas que tiene el cargador al ser expulsado (lo setea GlockToggle al recargar).")]
        [SerializeField] public int bullets = 20;

        public int Amount => bullets;

        public bool TryCollect()
        {
            GlockToggle gt = FindObjectOfType<GlockToggle>();
            if (gt == null)
            {
                Debug.LogWarning("<color=#ff7777>[GlockMagazinePickup] No hay GlockToggle en la escena; no se sumaron balas.</color>");
                return false;
            }

            gt.AddReserveAmmo(bullets);
            return true;
        }

        // ---- IInteractable ----
        public bool IsGrabbable => true;
        public bool IsValidTarget(Collider hitCollider) => true;

        public bool TryInteract(InventoryController inventory, Collider hitCollider)
        {
            bool collected = TryCollect();
            if (collected)
            {
                Debug.Log($"<color=#77ff77>[GlockMagazinePickup] Cargador de glock agarrado: +{Amount} balas a la reserva. Item destruido.</color>");
                Destroy(gameObject);
                inventory?.NotifyAmmoCollected();
            }

            return collected;
        }
    }
}
