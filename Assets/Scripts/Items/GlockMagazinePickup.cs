using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    // Cargador expulsado de la Glock al recargar: al agarrarlo devuelve sus balas
    // a la reserva (igual que las balas de la recamara del revolver). No va al
    // inventario ni a la mano; es un item fisico que rebota y se puede recoger.
    [RequireComponent(typeof(Collider))]
    public class GlockMagazinePickup : MonoBehaviour
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
    }
}
