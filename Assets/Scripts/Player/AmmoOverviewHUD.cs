using System;
using PrisaControlada.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    //TP2 - Ian Rosenberg
    // HUD de la informacion del objeto que el jugador tiene en la mano.
    //
    // - Ningun icono de arma/linterna queda visible normalmente.
    // - Mientras se MANTIENE Tab se muestra SOLO el objeto equipado:
    //   Block_Escopeta, Block_Revolver, Block_Glock o el icono de linterna.
    // - Las armas muestran ademas su municion de reserva.
    // - Entrada y salida usan CanvasGroup para hacer un fade suave.
    //
    // La escena puede tener cualquiera de estas dos jerarquias:
    // 1) AmmoOverviewHUD_Canvas/CurrentItem/Icon+Count (HUD unificado).
    // 2) AmmoOverviewHUD_Canvas/Panel/Block_* (HUD anterior por bloques).
    // Se resuelven automaticamente para que el HUD funcione sin superposiciones.
    [DisallowMultipleComponent]
    public class AmmoOverviewHUD : MonoBehaviour
    {
        private const string CanvasName = "AmmoOverviewHUD_Canvas";
        private const string CurrentItemName = "CurrentItem";
        private const float WeaponSearchInterval = 0.25f;

        [Header("UI (asignada por la tool o a mano en la jerarquia)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform currentItem;
        [SerializeField] private RawImage icon;
        [SerializeField] private Text count;

        [Header("Texturas de los iconos")]
        [FormerlySerializedAs("iconEscopeta")]
        [SerializeField] private Texture2D shotgunTexture;
        [FormerlySerializedAs("iconRevolver")]
        [SerializeField] private Texture2D revolverTexture;
        [FormerlySerializedAs("iconGlock")]
        [SerializeField] private Texture2D glockTexture;

        [Header("Tecla")]
        [SerializeField] private Key toggleKey = Key.Tab;

        [Header("Comportamiento")]
        [Tooltip("Velocidad de aparicion/desaparicion del panel (unidades de alfa por segundo).")]
        [SerializeField, Min(0f)] private float fadeSpeed = 14f;

        private enum HeldItem
        {
            None,
            Shotgun,
            Revolver,
            Glock,
            Flashlight,
        }

        private PlayerHealth _health;
        private FlashlightHUD _flashlightHUD;
        private float _nextWeaponScan;
        private HeldItem _displayedItem;

        private WeaponController[] _revolvers;
        private ShotgunToggle[] _shotguns;
        private GlockToggle[] _glocks;
        private FlashlightToggle[] _flashlights;

        // Referencia al HUD anterior (un bloque por arma).
        private bool _usesUnifiedLayout;
        private GameObject _shotgunBlock;
        private GameObject _revolverBlock;
        private GameObject _glockBlock;
        private Text _shotgunCount;
        private Text _revolverCount;
        private Text _glockCount;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _flashlightHUD = GetComponent<FlashlightHUD>();

            if (!ResolveSceneReferences())
            {
                Debug.LogError(
                    "[AmmoOverviewHUD] No se encontro CurrentItem ni los bloques Block_* en " +
                    "AmmoOverviewHUD_Canvas. Ejecuta PrisaControlada/Configurar AmmoOverviewHUD. " +
                    "Componente deshabilitado.", this);
                enabled = false;
                return;
            }

            // El bloque arranca completamente oculto. No se usa SetActive(false) en
            // el Canvas porque debe seguir recibiendo el fade del CanvasGroup.
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            HideAllWeaponBlocks();

            ResolveTextures();
            RefreshHeldItems();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextWeaponScan)
            {
                _nextWeaponScan = Time.unscaledTime + WeaponSearchInterval;
                RefreshHeldItems();
            }

            HeldItem equipped = FindEquippedItem();
            bool overviewHeld = IsHeld() && !Blocked();

            // La linterna y las armas usan canvas distintos. Esperamos a que el
            // canvas que se va a ocultar termine su fade antes de mostrar el
            // otro para que los iconos nunca se superpongan.
            bool flashlightPanelHidden =
                _flashlightHUD == null || _flashlightHUD.PanelAlpha <= 0f;
            bool showWeapon = overviewHeld &&
                              IsWeapon(equipped) &&
                              flashlightPanelHidden;

            if (showWeapon)
            {
                // Durante el fade solo queda activo el bloque del arma equipada.
                if (_displayedItem != equipped)
                {
                    _displayedItem = equipped;
                    ApplyUnifiedIcon(equipped);
                }

                SetWeaponBlockState(equipped);
                UpdateCount(equipped);
            }
            else if (equipped != _displayedItem)
            {
                // Si se cambio/desequipo el arma mientras el panel se desvanecía,
                // el bloque anterior se apaga de inmediato para no mostrar un objeto
                // que ya no esta en la mano.
                _displayedItem = HeldItem.None;
                HideAllWeaponBlocks();
            }

            float targetAlpha = showWeapon ? 1f : 0f;
            if (!Mathf.Approximately(group.alpha, targetAlpha))
            {
                group.alpha = Mathf.MoveTowards(
                    group.alpha,
                    targetAlpha,
                    fadeSpeed * Time.unscaledDeltaTime);
            }

            // Al terminar el fade out, todos los GameObjects quedan apagados de
            // verdad (no solo invisibles por alfa).
            if (!showWeapon && group.alpha <= 0f)
                HideAllWeaponBlocks();
        }

        /// <summary>
        /// True mientras se mantiene Tab y no hay otro overlay bloqueando el HUD.
        /// </summary>
        public bool IsTabHeld => IsHeld() && !Blocked();

        /// <summary>
        /// True solo cuando la linterna es el objeto que esta en la mano y se
        /// mantiene Tab. Lo usa FlashlightHUD para coordinar el fade.
        /// </summary>
        public bool ShouldShowFlashlight =>
            IsTabHeld && FindEquippedItem() == HeldItem.Flashlight;

        /// <summary>
        /// True cuando hay un arma (no una linterna) equipada.
        /// </summary>
        public bool HasEquippedWeapon
        {
            get
            {
                HeldItem item = FindEquippedItem();
                return IsWeapon(item);
            }
        }

        /// <summary>
        /// Alfa actual del bloque de municion (0..1).
        /// </summary>
        public float PanelAlpha => group != null ? group.alpha : 0f;

        private bool IsHeld()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;

            if (toggleKey == Key.Tab)
                return keyboard.tabKey.isPressed;

            return keyboard[toggleKey].isPressed;
        }

        // No tapar la pantalla con municion mientras hay un overlay encima
        // (teclado de codigo, nota, o muerte del jugador).
        private bool Blocked()
        {
            if (CodeUI.IsOpen || NoteViewer.IsOpen) return true;
            if (_health != null && _health.IsDead) return true;
            return false;
        }

        private static bool IsWeapon(HeldItem item)
        {
            return item == HeldItem.Shotgun ||
                   item == HeldItem.Revolver ||
                   item == HeldItem.Glock;
        }

        // Busca todos los objetos, incluyendo los inactivos: un pickup guardado
        // en un slot queda desactivado, pero su referencia sigue siendo la del
        // jugador. El estado real de "en la mano" se lee con IsEquipped.
        private void RefreshHeldItems()
        {
            _revolvers = FindObjectsByType<WeaponController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            _shotguns = FindObjectsByType<ShotgunToggle>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            _glocks = FindObjectsByType<GlockToggle>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            _flashlights = FindObjectsByType<FlashlightToggle>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private HeldItem FindEquippedItem()
        {
            if (FindEquippedRevolver() != null)
                return HeldItem.Revolver;

            if (FindEquippedShotgun() != null)
                return HeldItem.Shotgun;

            if (FindEquippedGlock() != null)
                return HeldItem.Glock;

            if (FindEquippedFlashlight() != null)
                return HeldItem.Flashlight;

            return HeldItem.None;
        }

        private WeaponController FindEquippedRevolver()
        {
            if (_revolvers == null) return null;

            foreach (WeaponController weapon in _revolvers)
                if (weapon != null && weapon.IsEquipped)
                    return weapon;

            return null;
        }

        private ShotgunToggle FindEquippedShotgun()
        {
            if (_shotguns == null) return null;

            foreach (ShotgunToggle weapon in _shotguns)
                if (weapon != null && weapon.IsEquipped)
                    return weapon;

            return null;
        }

        private GlockToggle FindEquippedGlock()
        {
            if (_glocks == null) return null;

            foreach (GlockToggle weapon in _glocks)
                if (weapon != null && weapon.IsEquipped)
                    return weapon;

            return null;
        }

        private FlashlightToggle FindEquippedFlashlight()
        {
            if (_flashlights == null) return null;

            foreach (FlashlightToggle flashlight in _flashlights)
                if (flashlight != null && flashlight.IsEquipped)
                    return flashlight;

            return null;
        }

        private void ApplyUnifiedIcon(HeldItem item)
        {
            if (!_usesUnifiedLayout || icon == null) return;

            switch (item)
            {
                case HeldItem.Shotgun:
                    icon.texture = shotgunTexture;
                    break;
                case HeldItem.Revolver:
                    icon.texture = revolverTexture;
                    break;
                case HeldItem.Glock:
                    icon.texture = glockTexture;
                    break;
            }
        }

        private void UpdateCount(HeldItem item)
        {
            if (_usesUnifiedLayout)
            {
                switch (item)
                {
                    case HeldItem.Shotgun:
                    {
                        ShotgunToggle shotgun = FindEquippedShotgun();
                        SetCount(count, shotgun != null ? shotgun.ReserveShells : 0);
                        break;
                    }
                    case HeldItem.Revolver:
                    {
                        WeaponController revolver = FindEquippedRevolver();
                        SetCount(count, revolver != null ? revolver.ReserveAmmo : 0);
                        break;
                    }
                    case HeldItem.Glock:
                    {
                        GlockToggle glock = FindEquippedGlock();
                        SetCount(count, glock != null ? glock.Reserve : 0);
                        break;
                    }
                }

                return;
            }

            // HUD anterior: cada bloque conserva su propio contador.
            switch (item)
            {
                case HeldItem.Shotgun:
                {
                    ShotgunToggle shotgun = FindEquippedShotgun();
                    SetCount(_shotgunCount, shotgun != null ? shotgun.ReserveShells : 0);
                    break;
                }
                case HeldItem.Revolver:
                {
                    WeaponController revolver = FindEquippedRevolver();
                    SetCount(_revolverCount, revolver != null ? revolver.ReserveAmmo : 0);
                    break;
                }
                case HeldItem.Glock:
                {
                    GlockToggle glock = FindEquippedGlock();
                    SetCount(_glockCount, glock != null ? glock.Reserve : 0);
                    break;
                }
            }
        }

        private static void SetCount(Text countText, int value)
        {
            if (countText != null)
                countText.text = value.ToString();
        }

        private void SetWeaponBlockState(HeldItem item)
        {
            SetBlockActive(_shotgunBlock, item == HeldItem.Shotgun);
            SetBlockActive(_revolverBlock, item == HeldItem.Revolver);
            SetBlockActive(_glockBlock, item == HeldItem.Glock);
        }

        private void HideAllWeaponBlocks()
        {
            SetBlockActive(_shotgunBlock, false);
            SetBlockActive(_revolverBlock, false);
            SetBlockActive(_glockBlock, false);
        }

        private static void SetBlockActive(GameObject block, bool active)
        {
            if (block != null && block.activeSelf != active)
                block.SetActive(active);
        }

        private void ResolveTextures()
        {
            // Las referencias las cablea la tool de Editor. El fallback por
            // Resources evita que el HUD quede sin icono si aun no se guardo la
            // escena migrada.
            if (shotgunTexture == null)
                shotgunTexture = Resources.Load<Texture2D>("HUD/Escopeta");
            if (revolverTexture == null)
                revolverTexture = Resources.Load<Texture2D>("HUD/Revolver");
            if (glockTexture == null)
                glockTexture = Resources.Load<Texture2D>("HUD/Glock");
        }

        // ------------------------------------------------------------------
        // Referencias: primero las serielizadas (Inspector), y si faltan, las
        // resuelve por nombre en la jerarquia real del HUD.
        // ------------------------------------------------------------------
        private bool ResolveSceneReferences()
        {
            if (canvas == null)
            {
                Transform canvasTransform = transform.Find(CanvasName);
                if (canvasTransform != null)
                    canvas = canvasTransform.GetComponent<Canvas>();
            }
            if (canvas == null) return false;

            if (group == null)
                group = canvas.GetComponent<CanvasGroup>();
            if (group == null)
                group = canvas.gameObject.AddComponent<CanvasGroup>();

            // Ocultarlo desde el primer momento, incluso si despues la
            // jerarquia resulta incompleta. Asi nunca quedan iconos viejos
            // superpuestos en pantalla.
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            ResolveUnifiedLayout();

            // Se resuelven tambien los bloques antiguos aunque exista el HUD
            // unificado: si la escena conserva ambos, los antiguos deben
            // apagarse para que no aparezcan detras del icono actual.
            bool hasLegacyBlocks = ResolveLegacyBlocks();

            if (_usesUnifiedLayout)
            {
                HideAllWeaponBlocks();
                return true;
            }

            return hasLegacyBlocks;
        }

        private void ResolveUnifiedLayout()
        {
            if (currentItem == null)
            {
                Transform itemTransform = FindDescendant(canvas.transform, CurrentItemName);
                currentItem = itemTransform as RectTransform;
            }

            if (currentItem == null) return;

            if (icon == null)
                icon = FindRawImage(currentItem);
            if (count == null)
                count = FindText(currentItem);

            // Se necesita icono y contador para mostrar la informacion completa
            // del arma (icono + balas).
            _usesUnifiedLayout = icon != null && count != null;
            if (!_usesUnifiedLayout)
            {
                currentItem = null;
                icon = null;
                count = null;
            }
        }

        private bool ResolveLegacyBlocks()
        {
            Transform shotgun = FindDescendant(
                canvas.transform, "Block_Escopeta", "Block_Shotgun");
            Transform revolver = FindDescendant(
                canvas.transform, "Block_Revolver");
            Transform glock = FindDescendant(
                canvas.transform, "Block_Glock", "Block_Pistola");

            _shotgunBlock = shotgun != null ? shotgun.gameObject : null;
            _revolverBlock = revolver != null ? revolver.gameObject : null;
            _glockBlock = glock != null ? glock.gameObject : null;

            _shotgunCount = shotgun != null ? FindText(shotgun) : null;
            _revolverCount = revolver != null ? FindText(revolver) : null;
            _glockCount = glock != null ? FindText(glock) : null;

            return _shotgunBlock != null ||
                   _revolverBlock != null ||
                   _glockBlock != null;
        }

        private static Transform FindDescendant(Transform root, params string[] names)
        {
            if (root == null) return null;

            foreach (string name in names)
            {
                if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
                    return root;
            }

            foreach (Transform child in root)
            {
                Transform match = FindDescendant(child, names);
                if (match != null) return match;
            }

            return null;
        }

        private static RawImage FindRawImage(Transform root)
        {
            if (root == null) return null;

            Transform iconTransform = root.Find("Icon");
            if (iconTransform != null)
            {
                RawImage image = iconTransform.GetComponent<RawImage>();
                if (image != null) return image;
            }

            RawImage[] images = root.GetComponentsInChildren<RawImage>(true);
            return images.Length > 0 ? images[0] : null;
        }

        private static Text FindText(Transform root)
        {
            if (root == null) return null;

            Transform countTransform = root.Find("Count");
            if (countTransform != null)
            {
                Text text = countTransform.GetComponent<Text>();
                if (text != null) return text;
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            return texts.Length > 0 ? texts[0] : null;
        }
    }
}
