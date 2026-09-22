using UnityEngine;
using UnityEngine.InputSystem;

namespace PrisaControlada.Player
{
    public class FlashlightToggle : MonoBehaviour
    {
        public Key equipKey = Key.None;

        bool _on;
        bool _prev;
        bool _lightOn;
        bool _lightPrev;
        Transform _model;
        Light[] _spots;

        public bool IsBusy => false;
        public bool IsOn => _on && _lightOn;
        public Camera ViewCamera { get; private set; }

        void Awake()
        {
            if (transform.childCount > 0) _model = transform.GetChild(0);
            if (_model != null) _model.gameObject.SetActive(false);
            _on = false;

            _spots = GetComponentsInChildren<Light>(true);
            ViewCamera = GetComponentInParent<Camera>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (_on)
            {
                bool mouseDown = Mouse.current != null && Mouse.current.leftButton.isPressed;
                if (mouseDown && !_lightPrev)
                {
                    _lightOn = !_lightOn;
                    SetLights(_lightOn);
                    Debug.Log("[Flashlight] Luz " + (_lightOn ? "ON" : "OFF"));
                }
                _lightPrev = mouseDown;
            }
            else
            {
                _lightPrev = false;
            }
        }

        public void Equip()
        {
            _on = true;
            if (_model != null) _model.gameObject.SetActive(true);
            UnequipOthers();
            Debug.Log("[Flashlight] EQUIPADA (pickup)");
        }

        public void ToggleLight()
        {
            _lightOn = !_lightOn;
            SetLights(_lightOn);
        }

        public void Unequip()
        {
            if (!_on) return;
            _on = false;
            _lightOn = false;
            if (_model != null) _model.gameObject.SetActive(false);
            SetLights(false);
        }

        public void SilentUnequip()
        {
            _on = false;
            _lightOn = false;
            if (_model != null) _model.gameObject.SetActive(false);
            SetLights(false);
        }

        void SetLights(bool on)
        {
            if (_spots == null) return;
            foreach (var s in _spots)
                if (s != null) s.enabled = on;
        }

        void UnequipOthers()
        {
            var wc = GetComponentInParent<WeaponController>();
            if (wc != null) wc.Unequip();
            var st = FindFirstObjectByType<ShotgunToggle>();
            if (st != null) st.Unequip();
            var gt = FindFirstObjectByType<GlockToggle>();
            if (gt != null) gt.Unequip();
        }
    }
}
