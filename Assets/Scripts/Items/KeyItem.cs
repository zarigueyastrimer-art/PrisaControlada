using UnityEngine;

namespace PrisaControlada.Items
{
    public class KeyItem : PickupItem
    {
        [Header("Key")]
        [SerializeField] private int keyID = 1;

        public int KeyID => keyID;

        // Mas chica en el preview del inventario.
        public override Vector3 PreviewScale => new Vector3(0.55f, 0.55f, 0.55f);

        // Sube la llave en la mano para que se vea (quedaba muy abajo).
        public override Vector3 HandOffset => new Vector3(0f, 0.18f, 0f);
    }
}
