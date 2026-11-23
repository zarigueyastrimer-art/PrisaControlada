using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    //TP2 - Francisco Larrondo
    // Puerta cerrada con llave. Solo se interactua si la llave esta en la mano;
    // al usarla se consume y la puerta queda libre para abrir/cerrar.
    public class LockedDoor : DoorController
    {
        [Header("Key Lock")]
        [SerializeField] private int requiredKeyID = 1;
        [SerializeField] private bool startLocked = true;

        private bool _unlocked;
        private bool _unlockPending;

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
                return Toggle();

            if (_unlockPending)
            {
                InteractionPrompt.Show("La puerta ya se esta abriendo.");
                return false;
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

            if (!CanToggle)
            {
                InteractionPrompt.Show("La puerta esta rota y no se puede abrir.");
                Debug.LogError($"[LockedDoor] '{name}' no se puede abrir; la llave no se consumira.", this);
                return false;
            }

            // Primero se solicita la apertura. La llave y el desbloqueo se
            // confirman cuando la hoja ya se aparto de la boca.
            if (!RequestOpen())
                return false;

            _unlockPending = true;
            StartCoroutine(ConsumeKeyAfterOpen(inventory, key));
            return true;
        }

        private System.Collections.IEnumerator ConsumeKeyAfterOpen(InventoryController inventory, KeyItem key)
        {
            const float timeout = 3f;
            float deadline = Time.time + timeout;

            while (Time.time < deadline)
            {
                if (IsOpen)
                    break;

                // Si el jugador cancelo la apertura antes de completarse, la
                // puerta conserva la cerradura y la llave no se consume.
                if (!TargetOpen)
                {
                    _unlockPending = false;
                    InteractionPrompt.Show("Apertura cancelada. La llave no se consumio.");
                    yield break;
                }

                yield return null;
            }

            if (!IsOpen)
            {
                _unlockPending = false;
                InteractionPrompt.Show("La puerta no se abrio. La llave no se consumio.");
                yield break;
            }

            _unlocked = true;
            _unlockPending = false;

            if (inventory != null && inventory.HeldItem == key)
            {
                inventory.ConsumeHeldItem();
                InteractionPrompt.Show("Llave usada. Puerta desbloqueada.");
            }
            else
            {
                // La puerta se abrio con una llave valida, pero el jugador
                // cambio el objeto en mano durante la animacion. No se destruye
                // ningun item que ya no sea la llave validada.
                InteractionPrompt.Show("Puerta desbloqueada.");
            }
        }
    }
}
