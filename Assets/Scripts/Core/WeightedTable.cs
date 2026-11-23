using System.Collections.Generic;
using UnityEngine;

namespace PrisaControlada.Core
{
    //TP2 - Matías Morilla
    // Tabla de sorteo aleatorio ponderada GENERICA.
    //
    // El tipo T es el tipo de lo que se sortea. No hace falta que los elementos
    // compartan herencia ni tener una lista distinta por cada tipo: quien usa la
    // tabla elige T al crearla y el resto del codigo es el mismo.
    //
    // En este proyecto se usa en tres lugares que no tienen nada que ver entre si:
    //  - LootSpawner:            WeightedTable<LootEntry>      (que suelta una caja)
    //  - ProceduralMapGenerator: WeightedTable<RoomDefinition> (que sala viene)
    //  - EnemyLootSpawner:       WeightedTable<EnemyEntry>     (que enemigo sale)
    //
    // Ojo con lo que NO hace: adentro no hay ningun cast ((LootEntry)item) ni
    // downcast a object. La lista interna ya guarda T, asi que el metodo devuelve
    // T directamente y el compilador chequea todo.
    public class WeightedTable<T>
    {
        private readonly List<T> _items = new List<T>();
        private readonly List<float> _weights = new List<float>();
        private float _totalWeight;

        public int Count => _items.Count;
        public float TotalWeight => _totalWeight;

        // Carga un elemento con su peso. Peso 0 o negativo = no compite en el
        // sorteo y ni siquiera se guarda, asi nadie tiene que filtrar despues.
        public void Add(T item, float weight)
        {
            float w = Mathf.Max(0f, weight);
            if (w <= 0f) return;

            _items.Add(item);
            _weights.Add(w);
            _totalWeight += w;
        }

        // Sortea un elemento segun su peso relativo. Si la tabla esta vacia
        // devuelve default(T): null para clases, el valor por defecto para structs.
        //
        // T tambien puede ser null dentro de la tabla: asi LootSpawner carga el
        // peso de "no sale nada" como un elemento mas y compite en el mismo
        // sorteo, sin tener que tratarlo como caso aparte.
        public T Roll()
        {
            if (_items.Count == 0 || _totalWeight <= 0f)
                return default;

            float roll = Random.Range(0f, _totalWeight);

            for (int i = 0; i < _items.Count; i++)
            {
                roll -= _weights[i];
                if (roll < 0f)
                    return _items[i];
            }

            // Redondeo de float en el limite superior: cae afuera de todos los
            // rangos, se devuelve el ultimo elemento cargado.
            return _items[_items.Count - 1];
        }

        // Vacia la tabla para poder recargarla (pesos que cambian en runtime).
        public void Clear()
        {
            _items.Clear();
            _weights.Clear();
            _totalWeight = 0f;
        }
    }
}
