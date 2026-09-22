using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    // HUD del arma: muestra las 2 imagenes (fondo + balas revolver) solo mientras
    // el arma esta equipada, y un contador arriba a la izquierda con la cantidad de
    // balas en la RESERVA (no cuenta la que esta en el arma).
    [DisallowMultipleComponent]
    public class WeaponHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeaponController weapon;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Text counterText;

        private void Awake()
        {
            if (weapon == null)
                weapon = GetComponentInParent<WeaponController>();
            if (weapon == null)
                weapon = GetComponentInChildren<WeaponController>();

            ResolveSceneReferences();
        }

        private void ResolveSceneReferences()
        {
            if (canvas == null)
                canvas = transform.Find("WeaponHUD_Canvas")?.GetComponent<Canvas>();
            if (counterText == null)
                counterText = transform.Find("WeaponHUD_Canvas/ReserveCounter")?.GetComponent<Text>();
        }

        private void Update()
        {
            bool equipped = weapon != null && weapon.IsEquipped;

            if (canvas != null && canvas.gameObject.activeSelf != equipped)
                canvas.gameObject.SetActive(equipped);

            if (equipped && counterText != null)
                counterText.text = weapon.ReserveAmmo.ToString();
        }
    }
}
