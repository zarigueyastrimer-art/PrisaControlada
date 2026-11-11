using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    // Puerta cerrada con llave. Solo se interactua si la llave esta en la mano;
    // al usarla se consume y la puerta queda libre para abrir/cerrar.
    public class LockedDoor : DoorController
    {
        [Header("Key Lock")]
        [SerializeField] private int requiredKeyID = 1;
        [SerializeField] private bool startLocked = true;

        private bool _unlocked;

        public bool IsLocked => startLocked && !_unlocked;
        public int RequiredKeyID => requiredKeyID;

        protected override void Awake()
        {
            base.Awake();
            _unlocked = !startLocked;
        }

        public override bool TryInteract(InventoryController inventory)
        {
            if (!IsLocked)
            {
                Toggle();
                return true;
            }

            if (inventory == null)
                return false;

            KeyItem key = inventory.HeldItem as KeyItem;
            if (key == null)
            {
                InteractionPrompt.Show("Necesitas una llave en la mano.");
                return false;
            }

            if (key.KeyID != requiredKeyID)
            {
                InteractionPrompt.Show("Esa llave no encaja en esta cerradura.");
                return false;
            }

            _unlocked = true;
            inventory.ConsumeHeldItem();
            InteractionPrompt.Show("Llave usada. Puerta desbloqueada.");
            Toggle();
            return true;
        }
    }
}
