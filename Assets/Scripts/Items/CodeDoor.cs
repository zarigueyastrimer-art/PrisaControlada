using PrisaControlada.Player;
using UnityEngine;

namespace PrisaControlada.Items
{
    //TP2 - Francisco Larrondo
    // Puerta con codigo de 4 digitos. Al interactuar abre el HUD numerico;
    // si el codigo es correcto se desbloquea y se abre.
    public class CodeDoor : DoorController
    {
        public const int CodeLength = 4;

        [Header("Code Lock")]
        [SerializeField] private string correctCode = "";
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
                return Toggle();

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
            if (!IsLocked || code != correctCode)
                return false;

            // No se desbloquea hasta que la puerta acepta la solicitud de
            // apertura. Si sus referencias estan rotas, el estado de cerradura
            // permanece intacto.
            if (!RequestOpen())
                return false;

            _unlocked = true;
            return true;
        }

        // El generador procedural reutiliza el mismo prefab para todas las salas
        // con codigo y asigna aqui el valor calculado por PuzzleCodeController.
        public bool SetCorrectCode(string code)
        {
            string normalized = code != null ? code.Trim() : string.Empty;
            if (!IsValidCode(normalized))
            {
                Debug.LogError($"[CodeDoor] Codigo invalido '{code}'. Se requieren " +
                               $"{CodeLength} digitos.", this);
                return false;
            }

            correctCode = normalized;
            return true;
        }

        private static bool IsValidCode(string code)
        {
            if (code.Length != CodeLength)
                return false;

            for (int i = 0; i < code.Length; i++)
            {
                if (code[i] < '0' || code[i] > '9')
                    return false;
            }

            return true;
        }
    }
}
