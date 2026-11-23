using UnityEngine;

namespace PrisaControlada.Enemies
{
    //TP2 - Agustín Labarque
    // Struct de valor usado para recordar un ruido percibido.
    // Se copia al agregarlo a la lista: el sello no referencia objetos del mundo
    // y puede expirar sin modificar el estado de otro ruido.
    [System.Serializable]
    public struct NoiseStamp
    {
        public float time;
        public float level;
        public Vector3 position;
    }
}
