using System;
using UnityEngine;

namespace PrisaControlada.Player
{
    //TP2 - Matías Morilla
    // PARTE 1 del "final": medir cuantos metros avanzo el jugador.
    //
    // Vive en un objeto vacio (OrigenRecorrido) colocado en el spawn del
    // jugador. El mapa se genera en UNA sola direccion (el generador une el
    // Anchor_Out de la sala actual con el Anchor_In de la siguiente), asi que
    // la distancia en linea recta entre ese origen y el jugador es practicamente
    // lo que avanzo: no hace falta ir acumulando el camino recorrido paso a paso.
    //
    // Se engancha al MISMO evento OnDied que usa PlayerDeathSequence, asi que
    // mide en el instante exacto de la muerte (ahi el jugador ya esta quieto:
    // PlayerController quedo sin control y solo se anima la camara).
    //
    // FALTA (partes siguientes): mostrar los metros en la pantalla de game over
    // y escribirlos en un archivo de guardado. Eso se engancha desde afuera al
    // evento OnMedido, sin tener que tocar esta clase.
    [DisallowMultipleComponent]
    public class RunDistanceTracker : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Origen de la medicion. Si se deja vacio se usa la posicion de este objeto (el que va en el spawn).")]
        [SerializeField] private Transform origen;

        [Tooltip("Jugador. Si se deja vacio se busca el que tenga PlayerHealth.")]
        [SerializeField] private Transform jugador;

        [Header("Medicion")]
        [Tooltip("Mide solo en X/Z: ignora escaleras y desniveles para que el numero sea 'metros avanzados' en el plano del mapa.")]
        [SerializeField] private bool ignorarAltura = true;

        // Ultimo valor medido (0 hasta que alguien llama a Medir).
        public float MetrosRecorridos { get; private set; }

        // Distancia en vivo por si mas adelante queremos un HUD de progreso.
        public float MetrosActuales => Calcular();

        // Parte 2 (mostrar en el final) y parte 3 (archivo de guardado) se
        // suscriben aca desde afuera.
        public event Action<float> OnMedido;

        private PlayerHealth _health;

        private void Awake()
        {
            if (origen == null)
                origen = transform;

            if (jugador == null)
            {
                _health = GetComponent<PlayerHealth>();
                if (_health == null)
                    _health = FindFirstObjectByType<PlayerHealth>();

                if (_health != null)
                    jugador = _health.transform;
            }
            else
            {
                _health = jugador.GetComponent<PlayerHealth>();
            }
        }

        private void Start()
        {
            if (_health == null)
            {
                Debug.LogWarning("[Distancia] No encontre PlayerHealth: la distancia NO se va a medir al morir.", this);
                return;
            }

            _health.OnDied += Medir;
        }

        private void OnDestroy()
        {
            if (_health != null)
                _health.OnDied -= Medir;
        }

        // Lo llama OnDied, y tambien se puede disparar a mano desde el
        // Inspector (click derecho en el componente > "Medir ahora (test)")
        // para probar sin tener que morir.
        [ContextMenu("Medir ahora (test)")]
        public void Medir()
        {
            if (origen == null || jugador == null)
            {
                Debug.LogWarning("[Distancia] Falta el origen o el jugador: no hay nada que medir.", this);
                return;
            }

            MetrosRecorridos = Calcular();
            Debug.Log($"[Distancia] Avance desde el spawn: {MetrosRecorridos:0.0} m " +
                      $"(origen {origen.position} -> jugador {jugador.position}).", this);

            OnMedido?.Invoke(MetrosRecorridos);
        }

        private float Calcular()
        {
            if (origen == null || jugador == null)
                return 0f;

            if (!ignorarAltura)
                return Vector3.Distance(origen.position, jugador.position);

            // Distancia en el plano del mapa (X/Z), sin contar el desnivel.
            Vector2 a = new Vector2(origen.position.x, origen.position.z);
            Vector2 b = new Vector2(jugador.position.x, jugador.position.z);
            return Vector2.Distance(a, b);
        }
    }
}
