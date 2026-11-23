using PrisaControlada.Core;
using PrisaControlada.Player;
using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Menu
{
    //TP2 - Matías Morilla
    // Los 3 botones de dificultad de la página "jugar" (BtnDif0/1/2).
    //
    // Al apretar uno se guarda la dificultad en el archivo de partida y se
    // resalta la elegida. Hoy las 3 dificultades juegan igual, pero los
    // récords de metros se guardan separados por dificultad: si jugaste en
    // fácil, tu mejor run se guarda en "fácil".
    [DisallowMultipleComponent]
    public class SelectorDificultad : MonoBehaviour
    {
        // Orden fijo: 0 = fácil, 1 = estándar (el botón dice "Normal"), 2 = difícil.
        [Tooltip("BtnDif0 (fácil), BtnDif1 (Normal/estándar), BtnDif2 (difícil).")]
        [SerializeField] private Button[] botones;

        private static readonly string[] Ids =
        {
            Guardado.DificultadFacil,
            Guardado.DificultadEstandar,
            Guardado.DificultadDificil
        };

        private static readonly string[] Nombres = { "Fácil", "Estándar", "Difícil" };

        private Image[] _fondos;
        private Color[] _coloresOriginales;

        private void Start()
        {
            if (!BuscarBotones())
            {
                Debug.LogWarning("[Dificultad] No encontré los botones BtnDif0/1/2.", this);
                return;
            }

            _fondos = new Image[botones.Length];
            _coloresOriginales = new Color[botones.Length];

            for (int i = 0; i < botones.Length; i++)
            {
                if (botones[i] == null) continue;

                int idx = i;
                botones[i].onClick.AddListener(() => Elegir(idx));

                _fondos[i] = botones[i].GetComponent<Image>();
                if (_fondos[i] != null)
                    _coloresOriginales[i] = _fondos[i].color;
            }

            // Resaltar la dificultad que quedó guardada (o estándar por defecto).
            Resaltar();
        }

        private void Elegir(int indice)
        {
            if (indice < 0 || indice >= Ids.Length) return;

            Guardado.SetDificultad(Ids[indice]);
            Resaltar();
            InteractionPrompt.Show("Dificultad: " + Nombres[indice]);
        }

        // Pinta de dorado el botón de la dificultad actual y deja el resto
        // con su color original.
        private void Resaltar()
        {
            if (_fondos == null) return;

            string actual = Guardado.CargarDificultad();

            for (int i = 0; i < _fondos.Length && i < Ids.Length; i++)
            {
                if (_fondos[i] == null) continue;

                _fondos[i].color = Ids[i] == actual
                    ? Color.Lerp(_coloresOriginales[i], new Color(1f, 0.85f, 0.45f), 0.65f)
                    : _coloresOriginales[i];
            }
        }

        // Respaldo por si el setup no corrió: busca los botones por nombre.
        private bool BuscarBotones()
        {
            if (botones != null && botones.Length >= 3 &&
                botones[0] != null && botones[1] != null && botones[2] != null)
                return true;

            Button b0 = null, b1 = null, b2 = null;

            foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b.name == "BtnDif0") b0 = b;
                else if (b.name == "BtnDif1") b1 = b;
                else if (b.name == "BtnDif2") b2 = b;
            }

            if (b0 == null || b1 == null || b2 == null) return false;

            botones = new[] { b0, b1, b2 };
            return true;
        }
    }
}
