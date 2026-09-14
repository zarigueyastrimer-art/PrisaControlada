using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    // Comportamiento completo del arma Glock como objeto del suelo: se agarra,
    // equipa el GlockToggle del jugador, se guarda en un slot y se suelta/tira,
    // igual que la escopeta o el revolver. Todo esta en este script; el prefab solo
    // necesita este componente + Rigidbody + Collider + el mesh del arma. Al editar
    // este script, Unity lo aplica al prefab automaticamente (no hay que recrearlo).
    [RequireComponent(typeof(Rigidbody))]
    public class GlockGroundPickup : PickupItem
    {
        public override bool IsBusy
        {
            get
            {
                var gt = FindObjectOfType<GlockToggle>();
                return gt != null && gt.IsBusy;
            }
        }

        public override void PickUp(Transform holder, Vector3 holdOffset)
        {
            var gt = FindObjectOfType<GlockToggle>();
            if (gt != null)
            {
                gt.Equip();
                UnequipOthers();
            }
            base.PickUp(holder, holdOffset);
            gameObject.SetActive(false);
        }

        public override void Throw(Vector3 velocity)
        {
            var gt = FindObjectOfType<GlockToggle>();
            if (gt != null) gt.SilentUnequip();
            SpawnInFrontOfPlayer(gt);
            base.Throw(velocity);
        }

        public override void Drop()
        {
            var gt = FindObjectOfType<GlockToggle>();
            if (gt != null) gt.SilentUnequip();
            SpawnInFrontOfPlayer(gt);
            base.Drop();
        }

        public override void OnStoredInSlot()
        {
            // Guardar en slot: aqui si queremos la animacion de guardar.
            var gt = FindObjectOfType<GlockToggle>();
            if (gt != null) gt.Unequip();
        }

        // Coloca el objeto delante del personaje para que salga hacia el frente con
        // fisicas (igual que la escopeta/revolver al tirarlas).
        private void SpawnInFrontOfPlayer(GlockToggle gt)
        {
            gameObject.SetActive(true);
            Camera cam = (gt != null && gt.ViewCamera != null) ? gt.ViewCamera : Camera.main;
            Vector3 origin = transform.position;
            Vector3 fwd = Vector3.forward;
            if (cam != null)
            {
                origin = cam.transform.position;
                fwd = cam.transform.forward;
            }
            transform.SetPositionAndRotation(origin + fwd * 0.5f, Quaternion.identity);
        }

        // Exclusion mutua: al equipar la Glock se desequipan revolver y escopeta.
        private void UnequipOthers()
        {
            foreach (var wc in FindObjectsOfType<WeaponController>())
                wc.Unequip();
            var st = FindObjectOfType<ShotgunToggle>();
            if (st != null) st.Unequip();
        }
    }
}
