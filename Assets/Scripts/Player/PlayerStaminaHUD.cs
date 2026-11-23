using UnityEngine;

namespace PrisaControlada.Player
{
    //TP2 - Ian Rosenberg
    // [OBSOLETO] El HUD de stamina ya no se dibuja en runtime.
    //
    // Antes este componente creaba un canvas en runtime y pintaba una barra
    // verde/red. Ahora la barra se arma en la escena con la tool de Editor
    // "PrisaControlada/Configurar StaminaHUD" (jerarquia + shader de recorte)
    // y la logica la maneja StaminaBarHUD.
    //
    // El componente queda vacio a proposito: asi la tool puede buscar y quitar
    // los componentes viejos que todavia esten en las escenas o en el prefab.
    [DisallowMultipleComponent]
    public class PlayerStaminaHUD : MonoBehaviour
    {
    }
}
