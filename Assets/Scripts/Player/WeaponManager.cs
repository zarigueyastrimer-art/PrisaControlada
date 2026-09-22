using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.Player
{
    public class WeaponManager : MonoBehaviour
    {
        [SerializeField] private WeaponController[] weapons;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private int startWeaponIndex = 0;

        private InputAction _weaponSwitchAction;
        private int _currentIndex = -1;

        public WeaponController CurrentWeapon =>
            _currentIndex >= 0 && _currentIndex < weapons.Length ? weapons[_currentIndex] : null;

        private void Awake()
        {
            if (inputActions != null)
                _weaponSwitchAction = inputActions.FindAction("Previous");
        }

        private void OnEnable() => _weaponSwitchAction?.Enable();
        private void OnDisable() => _weaponSwitchAction?.Disable();

        private void Start()
        {
            Debug.Log($"[WeaponManager] Start: {weapons?.Length} weapons, startWeaponIndex={startWeaponIndex}");
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null)
                    Debug.Log($"[WeaponManager]   [{i}] {weapons[i].name}");
                weapons[i]?.Unequip();
            }

            if (weapons.Length > 0)
                SwitchWeapon(Mathf.Clamp(startWeaponIndex, 0, weapons.Length - 1));
        }

        private void Update()
        {
            if (_weaponSwitchAction != null && _weaponSwitchAction.WasPressedThisFrame())
            {
                if (weapons.Length < 2) return;
                int next = (_currentIndex + 1) % weapons.Length;
                SwitchWeapon(next);
            }
        }

        private void SwitchWeapon(int newIndex)
        {
            if (newIndex < 0 || newIndex >= weapons.Length) return;
            if (newIndex == _currentIndex) return;

            if (_currentIndex >= 0 && _currentIndex < weapons.Length)
                weapons[_currentIndex]?.Unequip();

            var gt = FindObjectOfType<GlockToggle>();
            if (gt != null) gt.Unequip();

            _currentIndex = newIndex;
            weapons[_currentIndex]?.Equip();

            Debug.Log($"[WeaponManager] Switched to weapon {_currentIndex}: {weapons[_currentIndex]?.name}");
        }

        public void AddWeapon(WeaponController weapon)
        {
            if (weapon == null) return;
            System.Array.Resize(ref weapons, weapons.Length + 1);
            weapons[weapons.Length - 1] = weapon;
            weapon.Unequip();
        }
    }
}
