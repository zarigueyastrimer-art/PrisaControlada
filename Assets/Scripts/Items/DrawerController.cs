using System.Collections.Generic;
using UnityEngine;

namespace PrisaControlada.Items
{
    public class DrawerController : MonoBehaviour
    {
        private const int DrawerLayer = 10;

        [System.Serializable]
        public class Drawer
        {
            public Transform drawer;
            public Vector3 openOffset = new Vector3(0f, 0f, 0.4f);
            [HideInInspector] public Vector3 closedLocal;
            [HideInInspector] public bool isOpen;
        }

        [Header("Sliding")]
        [SerializeField] private Drawer[] drawers = new Drawer[0];
        [SerializeField] private float openSpeed = 6f;
        [SerializeField] private float slideDistance = 0.4f;
        [Tooltip("1 = abre hacia afuera, -1 = direccion invertida.")]
        [SerializeField] private float slideSign = -1f;
        [Tooltip("Si esta activo, la direccion de apertura se calcula alejandose del centro del mueble.")]
        [SerializeField] private bool autoSlideDirection = true;

        [Header("Slippery Material")]
        [Tooltip("Friccion del material de los cajones. 0 = muy resbaloso, 1 = muy agarrado.")]
        [SerializeField] private float dynamicFriction = 0.1f;
        [SerializeField] private float staticFriction = 0.1f;
        [SerializeField] private float bounciness = 0.05f;
        [SerializeField] private PhysicsMaterialCombine frictionCombine = PhysicsMaterialCombine.Minimum;
        [SerializeField] private PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Minimum;

        private PhysicsMaterial _slippery;
        private readonly List<Rigidbody> _carryBuffer = new List<Rigidbody>();

        private void Awake()
        {
            if (drawers == null || drawers.Length == 0 || HasNullDrawers())
                AutoDetectDrawers();

            foreach (Drawer drawer in drawers)
            {
                if (drawer != null && drawer.drawer != null)
                    drawer.closedLocal = drawer.drawer.localPosition;
            }
        }

        private void Start()
        {
            if (autoSlideDirection)
            {
                foreach (Drawer drawer in drawers)
                {
                    if (drawer == null || drawer.drawer == null) continue;
                    drawer.openOffset = ComputeOpenOffset(drawer.drawer);
                }
            }

            foreach (Drawer drawer in drawers)
            {
                if (drawer == null || drawer.drawer == null) continue;

                EnsureSlipperyMaterial(drawer.drawer);
            }

            for (int i = 0; i < drawers.Length; i++)
            {
                if (drawers[i] == null || drawers[i].drawer == null)
                    Debug.LogError($"[DrawerController] Drawer {i} no tiene transform asignado.", this);
            }

            if (drawers.Length > 0 && drawers[0] != null)
                Debug.Log($"[DrawerController] openOffset[0] = {drawers[0].openOffset}", this);
        }

        private bool HasNullDrawers()
        {
            foreach (Drawer drawer in drawers)
            {
                if (drawer == null || drawer.drawer == null) return true;
            }

            return false;
        }

        private void AutoDetectDrawers()
        {
            List<Drawer> list = new List<Drawer>();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue;
                if (child.gameObject.layer == DrawerLayer)
                    list.Add(new Drawer { drawer = child });
            }

            if (list.Count > 0)
                drawers = list.ToArray();
        }

        private Vector3 ComputeOpenOffset(Transform drawer)
        {
            Vector3 away = drawer.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = drawer.forward;

            away.Normalize();
            away *= slideSign;

            Bounds bounds = ComputeBounds(drawer);
            Vector3 size = bounds.size;
            size.y = 0f;
            float depth = Mathf.Max(slideDistance, Vector3.Dot(size, away));

            return drawer.InverseTransformDirection(away) * depth;
        }

        private static Bounds ComputeBounds(Transform target)
        {
            Bounds bounds = new Bounds(target.position, Vector3.zero);
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);

            return bounds;
        }

        private void EnsureSlipperyMaterial(Transform drawer)
        {
            if (_slippery == null)
                _slippery = new PhysicsMaterial("DrawerSlippery");

            _slippery.dynamicFriction = dynamicFriction;
            _slippery.staticFriction = staticFriction;
            _slippery.bounciness = bounciness;
            _slippery.frictionCombine = frictionCombine;
            _slippery.bounceCombine = bounceCombine;

            foreach (Collider collider in drawer.GetComponentsInChildren<Collider>())
                collider.material = _slippery;
        }

        public bool IsDrawerHit(Collider hitCollider)
        {
            return FindDrawerIndex(hitCollider) >= 0;
        }

        public void ToggleDrawer(Collider hitCollider)
        {
            int index = FindDrawerIndex(hitCollider);
            if (index >= 0)
                ToggleAt(index);
        }

        private int FindDrawerIndex(Collider hitCollider)
        {
            Transform current = hitCollider.transform;
            while (current != null)
            {
                for (int i = 0; i < drawers.Length; i++)
                {
                    Drawer drawer = drawers[i];
                    if (drawer == null || drawer.drawer == null) continue;
                    if (drawer.drawer != current) continue;

                    return i;
                }

                current = current.parent;
            }

            return -1;
        }

        private void ToggleAt(int index)
        {
            Drawer drawer = drawers[index];
            drawer.isOpen = !drawer.isOpen;
            Debug.Log($"[DrawerController] Toggle -> {(drawer.isOpen ? "OPEN" : "CLOSED")} (Drawer {index + 1} of {drawers.Length})");
        }

        private void Update()
        {
            foreach (Drawer drawer in drawers)
            {
                if (drawer == null || drawer.drawer == null) continue;

                Vector3 worldPrev = drawer.drawer.position;
                Vector3 target = drawer.isOpen ? drawer.closedLocal + drawer.openOffset : drawer.closedLocal;
                drawer.drawer.localPosition = Vector3.Lerp(drawer.drawer.localPosition, target, openSpeed * Time.deltaTime);

                Vector3 worldDelta = drawer.drawer.position - worldPrev;
                if (worldDelta.sqrMagnitude > 0.000001f)
                    CarryObjects(drawer.drawer, worldDelta);
            }
        }

        private void CarryObjects(Transform drawer, Vector3 worldDelta)
        {
            Bounds bounds = ComputeBounds(drawer);
            bounds.Expand(0.05f);

            _carryBuffer.Clear();
            foreach (Rigidbody rb in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                if (rb == null || rb.isKinematic) continue;
                if (bounds.Contains(rb.position))
                    _carryBuffer.Add(rb);
            }

            foreach (Rigidbody rb in _carryBuffer)
                rb.MovePosition(rb.position + worldDelta);
        }
    }
}