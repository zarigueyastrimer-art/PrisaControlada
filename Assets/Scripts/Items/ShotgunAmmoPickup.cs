using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    // Caja de balas de escopeta: al agarrarla desaparece y suma cartuchos (reserva)
    // a la escopeta del jugador. No va al inventario ni a la mano.
    [RequireComponent(typeof(Collider))]
    public class ShotgunAmmoPickup : MonoBehaviour
    {
        [Header("Shotgun Ammo Box")]
        [Tooltip("Rango de cartuchos que suma al agarrarla (aleatorio entre min y max).")]
        [SerializeField] private int minShells = 2;
        [SerializeField] private int maxShells = 5;

        private int _lastCollected;

        public int Amount => _lastCollected;

        public bool TryCollect()
        {
            ShotgunToggle st = FindObjectOfType<ShotgunToggle>();
            if (st == null)
            {
                Debug.LogWarning("<color=#ff7777>[ShotgunAmmoPickup] No hay ShotgunToggle en la escena; no se sumaron cartuchos.</color>");
                return false;
            }

            int n = Random.Range(minShells, maxShells + 1);
            _lastCollected = n;
            st.AddReserveShells(n);
            return true;
        }
    }
}
