using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.Player
{
    // HUD de municion general: mientras se MANTIENE apretada la tecla Tab se
    // muestra en pantalla un panel con las balas de TODAS las armas (revolver,
    // escopeta, glock, etc). Al soltar Tab desaparece. No requiere referencias
    // ni UI en escena: arma las armas en runtime y dibuja con OnGUI.
    [DisallowMultipleComponent]
    public class AmmoOverviewHUD : MonoBehaviour
    {
        [Header("Tecla")]
        [SerializeField] private Key toggleKey = Key.Tab;

        [Header("Estilo")]
        [SerializeField, Range(0f, 1f)] private float panelAlpha = 0.85f;

        private WeaponController[] _revolvers;
        private ShotgunToggle[] _shotguns;
        private GlockToggle[] _glocks;
        private GUIStyle _titleStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _boxStyle;
        private bool _stylesReady;

        private void Start()
        {
            RefreshWeapons();
        }

        // Busca todas las armas presentes en la escena (por si hay mas de una).
        private void RefreshWeapons()
        {
            _revolvers = FindObjectsOfType<WeaponController>();
            _shotguns = FindObjectsOfType<ShotgunToggle>();
            _glocks = FindObjectsOfType<GlockToggle>();
        }

        private bool IsHeld()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            return kb[toggleKey].isPressed;
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(14, 14, 12, 12)
            };

            _titleStyle = new GUIStyle
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(1f, 0.9f, 0.6f);

            _rowStyle = new GUIStyle
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };
            _rowStyle.normal.textColor = Color.white;

            _stylesReady = true;
        }

        private void OnGUI()
        {
            if (!IsHeld()) return;

            EnsureStyles();

            // Reconecta armas por si aparecieron despues del Start.
            if ((_revolvers == null || _revolvers.Length == 0) &&
                (_shotguns == null || _shotguns.Length == 0) &&
                (_glocks == null || _glocks.Length == 0))
            {
                RefreshWeapons();
            }

            string body = BuildBody();
            if (string.IsNullOrEmpty(body)) return;

            float width = 420f;
            float height = 60f + CountLines(body) * 24f;
            Rect rect = new Rect(20f, 20f, width, height);

            GUI.color = new Color(1f, 1f, 1f, panelAlpha);
            GUI.Box(rect, string.Empty, _boxStyle);
            GUI.color = Color.white;

            GUI.Label(new Rect(rect.x, rect.y + 12f, width, 28f), "MUNICION", _titleStyle);

            GUI.Label(new Rect(rect.x + 20f, rect.y + 52f, width - 40f, height - 60f), body, _rowStyle);
        }

        private int CountLines(string text)
        {
            int lines = 1;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') lines++;
            return lines;
        }

        private string BuildBody()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (_revolvers != null)
            {
                foreach (WeaponController wc in _revolvers)
                {
                    if (wc == null) continue;
                    sb.AppendLine($"Revolver        reserva {wc.ReserveAmmo}");
                }
            }

            if (_shotguns != null)
            {
                foreach (ShotgunToggle st in _shotguns)
                {
                    if (st == null) continue;
                    sb.AppendLine($"Escopeta        reserva {st.ReserveShells}");
                }
            }

            if (_glocks != null)
            {
                foreach (GlockToggle gt in _glocks)
                {
                    if (gt == null) continue;
                    sb.AppendLine($"Glock           reserva {gt.Reserve}");
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
