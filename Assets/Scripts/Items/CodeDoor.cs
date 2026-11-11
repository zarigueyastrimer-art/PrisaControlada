using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    // Puerta con codigo de 4 digitos. Al interactuar abre el HUD numerico;
    // si el codigo es correcto se desbloquea y se abre.
    public class CodeDoor : DoorController
    {
        [Header("Code Lock")]
        [SerializeField] private string correctCode = "1234";
        [SerializeField] private bool startLocked = true;

        private bool _unlocked;

        public bool IsLocked => startLocked && !_unlocked;
        public string CorrectCode => correctCode;

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

            CodeUI ui = CodeUI.Instance;
            if (ui == null)
            {
                InteractionPrompt.Show("No hay teclado numerico disponible.");
                Debug.LogWarning("[CodeDoor] No hay CodeUI en la escena.");
                return false;
            }

            ui.Show(this);
            return false;
        }

        public bool TryUnlock(string code)
        {
            if (IsLocked && code == correctCode)
            {
                _unlocked = true;
                Toggle();
                return true;
            }

            return false;
        }
    }
}
