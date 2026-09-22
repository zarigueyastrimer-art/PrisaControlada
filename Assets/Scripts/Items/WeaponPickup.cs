using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    public enum PickupWeaponKind
    {
        Revolver,
        Shotgun,
    }

    // El arma como objeto recogible: en el suelo es el modelo del arma sola (sin manos).
    // Al agarrarla equipa el viewmodel del jugador y se oculta. Al tirarla o guardarla
    // en un slot se desequipa el viewmodel y el objeto vuelve a ser fisico/visible.
    [RequireComponent(typeof(Rigidbody))]
    public class WeaponPickup : PickupItem
    {
        [Header("Weapon Pickup")]
        [SerializeField] private WeaponController weapon;
        [SerializeField] private PickupWeaponKind weaponKind = PickupWeaponKind.Revolver;

        public WeaponController Weapon => _resolved as WeaponController;

        public override bool IsBusy
        {
            get
            {
                Component w = ResolveWeapon();
                if (w is WeaponController wc) return wc.IsBusy;
                if (w is ShotgunToggle st) return st.IsBusy;
                if (w is GlockToggle gt) return gt.IsBusy;
                return false;
            }
        }

        protected virtual Component ResolveWeapon()
        {
            if (_resolved != null) return _resolved;
            if (weapon != null) { _resolved = weapon; return _resolved; }

            // Detectar automaticamente por el modelo del pickup: la escopeta usa la
            // malla "Contender". Asi no depende de ningun campo del prefab ni de la
            // instancia en escena (funciona aunque no se re-instancie el pickup).
            bool isShotgunPickup = HasContenderMesh(gameObject);
            Component result;
            if (isShotgunPickup)
                result = FindObjectOfType<ShotgunToggle>();
            else
            {
                var wcs = FindObjectsOfType<WeaponController>();
                result = wcs.Length > 0 ? wcs[0] : null;
            }

            _resolved = result;
            return result;
        }

        private static bool HasContenderMesh(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name.Contains("Contender")) return true;
            return false;
        }

        private void EquipWeapon()
        {
            Component w = ResolveWeapon();
            if (w is WeaponController wc) { wc.Equip(); UnequipOther(wc); }
            else if (w is ShotgunToggle st) { st.Equip(); UnequipOther(st); }
            else if (w is GlockToggle gt) { gt.Equip(); UnequipOther(gt); }
        }

        private void UnequipWeapon()
        {
            Component w = ResolveWeapon();
            if (w is WeaponController wc) wc.Unequip();
            else if (w is ShotgunToggle st) st.Unequip();
            else if (w is GlockToggle gt) gt.Unequip();
        }

        // Exclusion mutua: al equipar un arma, se desequipa la otra para que no se
        // superpongan (el revolver se controla aparte de la escopeta y la glock).
        private void UnequipOther(Component equipped)
        {
            if (equipped is ShotgunToggle || equipped is GlockToggle)
            {
                foreach (var wc in FindObjectsOfType<WeaponController>())
                    wc.Unequip();
            }
            if (equipped is WeaponController || equipped is GlockToggle)
            {
                ShotgunToggle st = FindObjectOfType<ShotgunToggle>();
                if (st != null) st.Unequip();
            }
            if (equipped is WeaponController || equipped is ShotgunToggle)
            {
                GlockToggle gt = FindObjectOfType<GlockToggle>();
                if (gt != null) gt.Unequip();
            }
        }

        private Camera GetViewCamera()
        {
            Component w = ResolveWeapon();
            if (w is WeaponController wc) return wc.ViewCamera;
            if (w is ShotgunToggle st) return st.ViewCamera;
            if (w is GlockToggle gt) return gt.ViewCamera;
            return null;
        }

        public override void PickUp(Transform holder, Vector3 holdOffset)
        {
            EquipWeapon();
            base.PickUp(holder, holdOffset);
            gameObject.SetActive(false);
            Debug.Log($"<color=#cc99ff>[WeaponPickup] Agarrada -> equipada. tipo={weaponKind}</color>");
        }

        public override void Throw(Vector3 velocity)
        {
            UnequipWeapon();

            Vector3 pos = transform.position;
            Vector3 fwd = Vector3.forward;
            Camera cam = GetViewCamera();
            if (cam != null)
            {
                pos = cam.transform.position;
                fwd = cam.transform.forward;
            }

            gameObject.SetActive(true);
            transform.SetPositionAndRotation(pos + fwd * 0.4f, Quaternion.identity);
            base.Throw(velocity);
            Debug.Log($"<color=#cc99ff>[WeaponPickup] Tirada -> desequipada. pos={transform.position}</color>");
        }

        public override void Drop()
        {
            UnequipWeapon();
            gameObject.SetActive(true);
            base.Drop();
            Debug.Log($"<color=#cc99ff>[WeaponPickup] Soltada -> desequipada</color>");
        }

        public override void OnStoredInSlot()
        {
            UnequipWeapon();
            Debug.Log($"<color=#cc99ff>[WeaponPickup] Guardada en slot -> desequipada</color>");
        }

        protected Component _resolved;
    }

    // Pickup especifico de la escopeta: siempre equipa el ShotgunToggle del jugador,
    // sin depender de ningun campo del prefab (evita que se equipe el revolver).
    public class ShotgunPickup : WeaponPickup
    {
        protected override Component ResolveWeapon()
        {
            if (_resolved != null) return _resolved;
            _resolved = FindObjectOfType<ShotgunToggle>();
            return _resolved;
        }
    }
}
