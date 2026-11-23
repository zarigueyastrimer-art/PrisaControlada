using System;
using TMPro;
using UnityEngine;

namespace PrisaControlada.Map
{
    //TP2 - Agustín Labarque
    // Controla el puzzle de codigo de una sala. Al instanciarse elige al azar
    // una entrada de la tabla y muestra su identificador en el texto "Code".
    // La tabla de la nota convierte ese identificador en el codigo de cuatro
    // digitos que abre la puerta de salida.
    [DisallowMultipleComponent]
    public class PuzzleCodeController : MonoBehaviour
    {
        // Struct serializable: en el inspector se ve como una fila de la tabla
        // (id + codigo) y al asignarlo el valor se copia entero.
        [Serializable]
        private struct CodeEntry
        {
            public string id;
            public string code;

            public CodeEntry(string id, string code)
            {
                this.id = id;
                this.code = code;
            }
        }

        [Header("Texto de la pista")]
        [Tooltip("Texto TMP donde se muestra el identificador que debe consultar el jugador.")]
        [SerializeField] private TMP_Text codeText;

        [Header("Tabla de codigos")]
        [Tooltip("Identificador visible y codigo equivalente que abre la puerta.")]
        [SerializeField] private CodeEntry[] table =
        {
            new CodeEntry("181", "0729"),
            new CodeEntry("841", "1411"),
            new CodeEntry("876", "3005"),
            new CodeEntry("615", "2811"),
            new CodeEntry("972", "1809"),
        };

        private int _selectedIndex = -1;

        public bool HasSelection =>
            _selectedIndex >= 0 && table != null && _selectedIndex < table.Length;

        public string DisplayedId =>
            HasSelection ? table[_selectedIndex].id : string.Empty;

        public string DoorCode
        {
            get
            {
                TryGetDoorCode(out string code);
                return code;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        // Permite que el generador obtenga el codigo sin depender del orden
        // exacto de los callbacks de Unity.
        public bool TryGetDoorCode(out string code)
        {
            EnsureInitialized();
            code = HasSelection ? table[_selectedIndex].code : string.Empty;
            return !string.IsNullOrEmpty(code);
        }

        private void EnsureInitialized()
        {
            if (HasSelection)
                return;

            if (codeText == null)
                codeText = GetComponentInChildren<TMP_Text>(true);

            if (table == null || table.Length == 0)
            {
                Debug.LogError($"[PuzzleCode] '{name}' no tiene una tabla de codigos.", this);
                return;
            }

            int validCount = 0;
            for (int i = 0; i < table.Length; i++)
            {
                if (IsValidId(table[i].id) && IsValidCode(table[i].code))
                    validCount++;
            }

            if (validCount == 0)
            {
                Debug.LogError($"[PuzzleCode] '{name}' no tiene entradas validas en la tabla.", this);
                return;
            }

            // Solo elige entre entradas validas, aunque la tabla tenga algun
            // dato mal configurado en el inspector.
            int start = UnityEngine.Random.Range(0, table.Length);
            for (int offset = 0; offset < table.Length; offset++)
            {
                int index = (start + offset) % table.Length;
                if (!IsValidId(table[index].id) || !IsValidCode(table[index].code))
                    continue;

                _selectedIndex = index;
                if (codeText != null)
                    codeText.text = table[index].id;
                else
                    Debug.LogWarning($"[PuzzleCode] '{name}' no encontro el texto TMP de la pista.", this);

                return;
            }
        }

        private static bool IsValidId(string value)
        {
            return value != null && value.Length == 3 && ContainsOnlyDigits(value);
        }

        private static bool IsValidCode(string value)
        {
            return value != null && value.Length == 4 && ContainsOnlyDigits(value);
        }

        private static bool ContainsOnlyDigits(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] < '0' || value[i] > '9')
                    return false;
            }

            return true;
        }
    }
}
