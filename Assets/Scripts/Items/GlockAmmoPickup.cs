using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    // Caja de balas de glock (9mm): al agarrarla desaparece y suma balas (reserva)
    // a la glock del jugador. No va al inventario ni a la mano.
    [RequireComponent(typeof(Collider))]
    public class GlockAmmoPickup : MonoBehaviour
    {
        [Header("Glock Ammo Box")]
        [Tooltip("Rango de balas que suma a la reserva al agarrarla (aleatorio entre min y max).")]
        [SerializeField] private int minBullets = 8;
        [SerializeField] private int maxBullets = 16;

        private int _lastCollected;

        public int Amount => _lastCollected;

        public bool TryCollect()
        {
            GlockToggle gt = FindObjectOfType<GlockToggle>();
            if (gt == null)
            {
                Debug.LogWarning("<color=#ff7777>[GlockAmmoPickup] No hay GlockToggle en la escena; no se sumaron balas.</color>");
                return false;
            }

            int n = Random.Range(minBullets, maxBullets + 1);
            _lastCollected = n;
            gt.AddReserveAmmo(n);
            Debug.Log($"<color=#77ff77>[GlockAmmoPickup] +{n} balas a la reserva. Ahora -> cargador: {gt.Magazine}/{gt.MagazineSize} | reserva: {gt.Reserve}</color>");
            return true;
        }
    }
}
