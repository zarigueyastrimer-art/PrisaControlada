using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    // El arma como objeto recogible: en el suelo es el modelo del arma sola (sin manos).
    // Al agarrarla equipa el viewmodel del jugador y se oculta. Al tirarla o guardarla
    // en un slot se desequipa el viewmodel y el objeto vuelve a ser fisico/visible.
    [RequireComponent(typeof(Rigidbody))]
    public class WeaponPickup : PickupItem
    {
        [Header("Weapon Pickup")]
        [SerializeField] private WeaponController weapon;

        public WeaponController Weapon => weapon;

        public override bool IsBusy
        {
            get
            {
                WeaponController w = ResolveWeapon();
                return w != null && w.IsBusy;
            }
        }

        private WeaponController ResolveWeapon()
        {
            if (weapon != null) return weapon;
            weapon = FindObjectOfType<WeaponController>();
            return weapon;
        }

        public override void PickUp(Transform holder, Vector3 holdOffset)
        {
            WeaponController w = ResolveWeapon();
            if (w != null) w.Equip();
            base.PickUp(holder, holdOffset);
            gameObject.SetActive(false);
            Debug.Log($"<color=#cc99ff>[WeaponPickup] Agarrada -> equipada. viewmodel equipado={(w != null && w.IsEquipped)}</color>");
        }

        public override void Throw(Vector3 velocity)
        {
            WeaponController w = ResolveWeapon();
            if (w != null) w.Unequip();

            Vector3 pos = transform.position;
            Vector3 fwd = Vector3.forward;
            if (w != null && w.ViewCamera != null)
            {
                pos = w.ViewCamera.transform.position;
                fwd = w.ViewCamera.transform.forward;
            }

            gameObject.SetActive(true);
            transform.SetPositionAndRotation(pos + fwd * 0.4f, Quaternion.identity);
            base.Throw(velocity);
            Debug.Log($"<color=#cc99ff>[WeaponPickup] Tirada -> desequipada. pos={transform.position}</color>");
        }

        public override void Drop()
        {
            WeaponController w = ResolveWeapon();
            if (w != null) w.Unequip();
            gameObject.SetActive(true);
            base.Drop();
            Debug.Log($"<color=#cc99ff>[WeaponPickup] Soltada -> desequipada</color>");
        }

        public override void OnStoredInSlot()
        {
            WeaponController w = ResolveWeapon();
            if (w != null) w.Unequip();
            Debug.Log($"<color=#cc99ff>[WeaponPickup] Guardada en slot -> desequipada</color>");
        }
    }
}