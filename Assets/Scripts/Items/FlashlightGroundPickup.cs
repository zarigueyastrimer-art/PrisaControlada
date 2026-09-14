using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    [RequireComponent(typeof(Rigidbody))]
    public class FlashlightGroundPickup : PickupItem
    {
        [SerializeField] private float dropForwardSpeed = 4f;

        public override bool IsBusy => false;
        public override Vector3 PreviewScale => new Vector3(0.5f, 0.5f, 0.5f);

        public override void PickUp(Transform holder, Vector3 holdOffset)
        {
            bool wasLit = IsDropLit();
            SetDropLights(false);
            var ft = FindFlashlightToggle();
            if (ft != null)
            {
                ft.Equip();
                if (wasLit) ft.ToggleLight();
                UnequipOthers();
            }
            base.PickUp(holder, holdOffset);
            gameObject.SetActive(false);
        }

        public override void Throw(Vector3 velocity)
        {
            var ft = FindFlashlightToggle();
            bool wasOn = ft != null && ft.IsOn;
            if (ft != null) ft.SilentUnequip();
            SpawnForward();
            base.Throw(velocity);
            ApplyForwardVelocity();
            if (wasOn) SetDropLights(true);
        }

        public override void Drop()
        {
            var ft = FindFlashlightToggle();
            bool wasOn = ft != null && ft.IsOn;
            if (ft != null) ft.SilentUnequip();
            SpawnForward();
            base.Drop();
            ApplyForwardVelocity();
            if (wasOn) SetDropLights(true);
        }

        public override void OnStoredInSlot()
        {
            SetDropLights(false);
            var ft = FindFlashlightToggle();
            if (ft != null) ft.Unequip();
        }

        private void SpawnForward()
        {
            gameObject.SetActive(true);
            Camera cam = Camera.main;
            Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 origin = cam != null ? cam.transform.position : transform.position;
            Quaternion rot = Quaternion.LookRotation(fwd) * Quaternion.Euler(90f, 0f, 0f);
            transform.SetPositionAndRotation(origin + fwd * 0.6f, rot);
        }

        private void ApplyForwardVelocity()
        {
            Camera cam = Camera.main;
            Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
            GetComponent<Rigidbody>().linearVelocity = fwd * dropForwardSpeed;
        }

        void SetDropLights(bool on)
        {
            foreach (var s in GetComponentsInChildren<Light>(true))
                if (s != null) s.enabled = on;
        }

        bool IsDropLit()
        {
            foreach (var s in GetComponentsInChildren<Light>(true))
                if (s != null && s.enabled) return true;
            return false;
        }

        private static FlashlightToggle FindFlashlightToggle()
        {
            foreach (var ft in Resources.FindObjectsOfTypeAll<FlashlightToggle>())
                if (ft.gameObject.activeInHierarchy || ft.GetComponentInParent<Camera>() != null)
                    return ft;
            return FindObjectOfType<FlashlightToggle>();
        }

        private void UnequipOthers()
        {
            foreach (var wc in FindObjectsOfType<WeaponController>())
                wc.Unequip();
            var st = FindObjectOfType<ShotgunToggle>();
            if (st != null) st.Unequip();
            var gt = FindObjectOfType<GlockToggle>();
            if (gt != null) gt.Unequip();
        }
    }
}
