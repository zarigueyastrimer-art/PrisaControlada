using System.Text.RegularExpressions;
using PrisaControlada.Core;
using TMPro;
using UnityEngine;

namespace PrisaControlada.Menu
{
    //TP2 - Matías Morilla
    // Pinta en la página "historia" (lado izquierdo) el mejor recorrido de
    // cada dificultad, en los textos "Distancia Facil/Estandar/Dificil".
    //
    // Cada texto viene con una plantilla como '"X" Metros': se reemplaza lo
    // que esté entre comillas por el récord guardado (el mismo truco que usa
    // GameOverController con '"000" METERS TRAVELED').
    [DisallowMultipleComponent]
    public class HistorialDistancia : MonoBehaviour
    {
        [Tooltip("Texto 'Distancia Facil' (hijo de Izquierda).")]
        [SerializeField] private TextMeshProUGUI facil;

        [Tooltip("Texto 'Distancia Estandar'.")]
        [SerializeField] private TextMeshProUGUI estandar;

        [Tooltip("Texto 'Distancia Dificil'.")]
        [SerializeField] private TextMeshProUGUI dificil;

        // Plantillas originales: se capturan antes de la primera pintada.
        private string _pFacil;
        private string _pEstandar;
        private string _pDificil;

        private void Start()
        {
            Pintar();
        }

        public void Pintar()
        {
            if (facil == null && estandar == null && dificil == null)
            {
                Debug.LogWarning("[Historial] No hay textos de distancia asignados: " +
                                 "corré Tools > PrisaControlada > Notas: Setup en el menú.", this);
                return;
            }

            PintarUno(facil, Guardado.CargarMejor(Guardado.DificultadFacil), ref _pFacil);
            PintarUno(estandar, Guardado.CargarMejor(Guardado.DificultadEstandar), ref _pEstandar);
            PintarUno(dificil, Guardado.CargarMejor(Guardado.DificultadDificil), ref _pDificil);
        }

        private static void PintarUno(TextMeshProUGUI texto, float metros, ref string plantilla)
        {
            if (texto == null) return;

            if (plantilla == null)
                plantilla = texto.text;

            int valor = Mathf.RoundToInt(metros);

            // 1) Reemplaza lo que este entre comillas: '"X"' -> '"19815"'.
            string nuevo = Regex.Replace(plantilla, "\"[^\"]*\"", "\"" + valor + "\"");

            // 2) Si no hay comillas, reemplaza el primer numero que encuentre.
            if (nuevo == plantilla)
                nuevo = Regex.Replace(plantilla, "\\d+", valor.ToString());

            // 3) Si la plantilla no tenia numero, agrega el valor al final.
            if (nuevo == plantilla)
                nuevo = plantilla + " " + valor;

            texto.text = nuevo;
        }
    }
}
