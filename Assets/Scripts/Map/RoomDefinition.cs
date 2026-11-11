using UnityEngine;

namespace PrisaControlada.Map
{
    // Datos de una sala para el mapa procedural. Va SIEMPRE en la RAIZ del
    // prefab de sala (no en un hijo), porque el generator lo busca con
    // GetComponent en la instancia viva.
    //
    // Los anclajes son objetos vacios que crea la herramienta
    // Tools > PrisaControlada > Mapa > Preparar Anclajes a partir de las
    // puertas colocadas en Prototype.unity:
    //
    //   Anchor_In      -> por donde ENTRA la sala anterior.
    //   Anchor_Out_N   -> por donde SALEN las salas siguientes.
    //
    // Convencion: el forward (+Z) de un ancla apunta HACIA AFUERA de la sala
    // (la direccion en la que se camina al pasar la puerta). Para conectar dos
    // salas el generator pone el Anchor_In de la nueva sala en el mismo punto
    // que el Anchor_Out de la anterior, con los forward enfrentados (180 grados).
    [DisallowMultipleComponent]
    public class RoomDefinition : MonoBehaviour
    {
        [Header("Tipo y peso")]
        [Tooltip("Tipo de sala. Define las reglas de aparicion en el sorteo.")]
        public RoomType type = RoomType.Pasillo;

        [Tooltip("Peso base en el sorteo de la siguiente sala. Mas peso = mas probable.")]
        [Min(0f)] public float weight = 1f;

        [Header("Anclajes")]
        [Tooltip("Ancla de entrada (vacia 'Anchor_In'). El hub no tiene, es su primera sala.")]
        public Transform anchorIn;

        [Tooltip("Anclas de salida (vacías 'Anchor_Out_N'). El generator usa la primera.")]
        public Transform[] anchorsOut = new Transform[0];

        [Header("Cerradura de salida")]
        [Tooltip("Si esta marcada, la puerta de SALIDA de esta sala se instancia cerrada " +
                 "con llave (lockedDoorPrefab del generador). El jugador necesita el KeyPickup " +
                 "correspondiente en la mano para abrirla.")]
        public bool lockExit = false;

        // Primera salida no nula. Es la que usa el generator para encadenar.
        public Transform PrimaryOut
        {
            get
            {
                if (anchorsOut == null) return null;
                for (int i = 0; i < anchorsOut.Length; i++)
                    if (anchorsOut[i] != null)
                        return anchorsOut[i];
                return null;
            }
        }

        public bool HasIn => anchorIn != null;
        public bool HasOut => PrimaryOut != null;

        // La sala se puede encadenar si tiene salida, y si no es el hub tambien
        // necesita entrada.
        public bool CanBeChained => type == RoomType.Hub ? HasOut : (HasIn && HasOut);
    }
}
