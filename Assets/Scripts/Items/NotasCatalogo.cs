using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrisaControlada.Items
{
    //TP2 - Matías Morilla
    // Catálogo único de notas de lore: UN solo ScriptableObject con todas las
    // notas (id + título + texto). Para crear notas en masa se abre el asset
    // y se apreta "+" en la lista: no hay que crear un archivo por nota.
    //
    // El mismo asset lo usan los tres lugares del sistema:
    //  - NotaLore (pickup en la escena) para resolver qué nota mostrar;
    //  - NotaLoreViewer para pintarla en pantalla;
    //  - NotasMenu (menú principal) para listar las coleccionadas.
    [CreateAssetMenu(fileName = "NotasCatalogo", menuName = "PrisaControlada/Notas/Catálogo")]
    public class NotasCatalogo : ScriptableObject
    {
        //TP2 - Matías Morilla
        [Serializable]
        public class Nota
        {
            [Tooltip("Id único y estable. Es lo que se guarda en el archivo de partida; no se puede repetir.")]
            public string id;

            public string titulo;

            [TextArea(4, 14)]
            public string texto;
        }

        public List<Nota> notas = new List<Nota>();

        // Devuelve la nota con ese id, o null si no existe.
        public Nota Buscar(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < notas.Count; i++)
            {
                if (notas[i] != null && notas[i].id == id)
                    return notas[i];
            }

            return null;
        }

        // Elige una nota al azar entre las que el jugador todavía no tiene.
        // Devuelve null si ya coleccionó todas.
        public Nota ElegirNoColeccionada(ICollection<string> coleccionadas)
        {
            List<Nota> faltantes = new List<Nota>();

            for (int i = 0; i < notas.Count; i++)
            {
                Nota n = notas[i];
                if (n == null || string.IsNullOrEmpty(n.id)) continue;
                if (coleccionadas != null && coleccionadas.Contains(n.id)) continue;
                faltantes.Add(n);
            }

            if (faltantes.Count == 0) return null;
            return faltantes[UnityEngine.Random.Range(0, faltantes.Count)];
        }

        // Las coleccionadas en el orden del catálogo (para el menú).
        public List<Nota> Coleccionadas(ICollection<string> coleccionadas)
        {
            List<Nota> lista = new List<Nota>();

            for (int i = 0; i < notas.Count; i++)
            {
                Nota n = notas[i];
                if (n == null || string.IsNullOrEmpty(n.id)) continue;
                if (coleccionadas != null && coleccionadas.Contains(n.id))
                    lista.Add(n);
            }

            return lista;
        }
    }
}
