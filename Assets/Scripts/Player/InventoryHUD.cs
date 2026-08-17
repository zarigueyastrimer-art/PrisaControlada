using PrisaControlada.Items;
using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    public class InventoryHUD : MonoBehaviour
    {
        private const int SlotCount = 2;
        private const int PreviewWidth = 140;
        private const int PreviewHeight = 120;
        private const float PreviewDistance = 4f;
        private const float RotationSpeed = 25f;
        private const int PreviewLayerA = 11;
        private const int PreviewLayerB = 12;

        [Header("Preview Presentation")]
        [Tooltip("Inclinacion del objeto frente a la camara (grados). 0 = vista frontal; 90 = vista desde arriba.")]
        [SerializeField] private float previewPitch = 75f;
        [Tooltip("Rotacion horizontal del objeto frente a la camara (grados).")]
        [SerializeField] private float previewYaw = 0f;

        [Header("Scene References")]
        [SerializeField] private InventoryController inventory;
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform slotsBar;
        [SerializeField] private RawImage[] slotImages = new RawImage[SlotCount];
        [SerializeField] private Camera[] previewCameras = new Camera[SlotCount];
        [SerializeField] private Transform[] visualRoots = new Transform[SlotCount];

        private readonly SlotPreview[] _slots = new SlotPreview[SlotCount];

        private class SlotPreview
        {
            public RawImage image;
            public RenderTexture texture;
            public Camera camera;
            public Transform visualRoot;
            public GameObject visual;
            public PickupItem currentItem;
            public int layer;
        }

        private void Awake()
        {
            if (inventory == null) inventory = GetComponent<InventoryController>();
            if (inventory == null) inventory = FindObjectOfType<InventoryController>();

            ResolveSceneReferences();
        }

        private void ResolveSceneReferences()
        {
            if (canvas == null)
                canvas = transform.Find("InventoryHUD_Canvas")?.GetComponent<Canvas>();
            if (slotsBar == null)
                slotsBar = transform.Find("InventoryHUD_Canvas/SlotsBar") as RectTransform;

            for (int i = 0; i < SlotCount; i++)
            {
                if (slotImages[i] == null)
                    slotImages[i] = transform.Find($"InventoryHUD_Canvas/SlotsBar/Slot{i + 1}/Preview")?.GetComponent<RawImage>();
                if (previewCameras[i] == null)
                    previewCameras[i] = transform.Find($"PreviewCamera{i}")?.GetComponent<Camera>();
                if (visualRoots[i] == null)
                    visualRoots[i] = transform.Find($"PreviewCamera{i}/VisualRoot");
            }
        }

        private void Start()
        {
            if (inventory == null || canvas == null || slotsBar == null) return;

            ExcludePreviewLayersFromMainCamera();

            for (int i = 0; i < SlotCount; i++)
            {
                if (previewCameras[i] == null || slotImages[i] == null || visualRoots[i] == null)
                    continue;

                RenderTexture rt = new RenderTexture(PreviewWidth, PreviewHeight, 24, RenderTextureFormat.ARGB32);
                previewCameras[i].targetTexture = rt;
                _slots[i] = new SlotPreview
                {
                    image = slotImages[i],
                    texture = rt,
                    camera = previewCameras[i],
                    visualRoot = visualRoots[i],
                    layer = i == 0 ? PreviewLayerA : PreviewLayerB
                };
                previewCameras[i].cullingMask = 1 << _slots[i].layer;
            }

            foreach (SlotPreview slot in _slots)
            {
                if (slot == null) continue;
                slot.image.enabled = false;
                slot.camera.enabled = false;
            }

            RefreshAll();
            inventory.InventoryChanged += RefreshAll;
        }

        private void Update()
        {
            foreach (SlotPreview slot in _slots)
            {
                if (slot != null && slot.visual != null)
                    slot.visual.transform.Rotate(0f, 0f, RotationSpeed * Time.deltaTime, Space.Self);
            }
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.InventoryChanged -= RefreshAll;

            foreach (SlotPreview slot in _slots)
            {
                if (slot != null && slot.texture != null)
                    slot.texture.Release();
            }
        }

        private void RefreshAll()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == null) continue;

                PickupItem item = inventory.GetSlotItem(i);
                if (item != _slots[i].currentItem)
                    RebuildVisual(i, item);
            }
        }

        private void RebuildVisual(int index, PickupItem item)
        {
            SlotPreview slot = _slots[index];

            if (slot == null) return;

            if (slot.visual != null)
            {
                Destroy(slot.visual);
                slot.visual = null;
            }

            slot.currentItem = item;

            if (item == null)
            {
                slot.camera.enabled = false;
                slot.image.enabled = false;
                return;
            }

            slot.image.enabled = true;
            slot.image.texture = slot.texture;
            slot.visual = CreateVisualFrom(item, slot.layer);
            slot.visual.transform.SetParent(slot.visualRoot, false);
            slot.visual.transform.localPosition = Vector3.zero;
            slot.visual.transform.localRotation = Quaternion.identity;

            // Presentacion: el VisualRoot es el punto en frente de la camara donde
            // "vive" el objeto. Rotarlo es equivalente a orbitar la camara alrededor
            // del objeto (ya que el objeto es hijo de la camara). Con previewPitch
            // inclinamos la vista para mirar el arma desde arriba.
            slot.visualRoot.localRotation = Quaternion.Euler(previewPitch, previewYaw, 0f);

            // Tilt del arma: deja el cañon apuntando a la derecha (X) dentro del
            // visual. El giro (Z) en Update se aplica sobre el contenedor y se ve
            // como una moneda girando sobre el centro, ahora desde arriba.
            GameObject tilt = new GameObject("TiltPivot");
            tilt.layer = slot.layer;
            tilt.transform.SetParent(slot.visual.transform, false);
            tilt.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-90f, 0f, 0f);
            for (int i = slot.visual.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = slot.visual.transform.GetChild(i);
                if (child == tilt.transform) continue;
                child.SetParent(tilt.transform, true);
            }

            CenterVisualPivot(slot.visual);

            slot.camera.enabled = true;
            FitCameraToBounds(slot);
        }

        private static void CenterVisualPivot(GameObject visual)
        {
            if (visual == null) return;

            Bounds bounds = new Bounds(visual.transform.position, Vector3.zero);
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
                bounds.Encapsulate(r.bounds);

            Vector3 localCenter = visual.transform.InverseTransformPoint(bounds.center);
            foreach (Transform child in visual.transform)
                child.localPosition -= localCenter;
        }

        private static GameObject CreateVisualFrom(PickupItem item, int layer)
        {
            GameObject visual = new GameObject("PreviewVisual");
            visual.layer = layer;

            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
            bool hasSkinned = false;
            foreach (Renderer r in renderers)
                if (r is SkinnedMeshRenderer) { hasSkinned = true; break; }

            if (hasSkinned)
            {
                // El preview necesita la jerarquia de huesos: se clona el subarbol que
                // contiene renderers + huesos (rootBone) y se deja en pose bind.
                Transform root = renderers[0].transform;
                for (int i = 1; i < renderers.Length; i++)
                    root = FindCommonAncestor(root, renderers[i].transform);
                foreach (Renderer r in renderers)
                    if (r is SkinnedMeshRenderer smr && smr.rootBone != null)
                        root = FindCommonAncestor(root, smr.rootBone);

                GameObject clone = Object.Instantiate(root.gameObject);
                clone.SetActive(true);
                clone.name = root.name;
                clone.transform.SetParent(visual.transform, false);
                SetLayerRecursive(clone, layer);
                foreach (Animator anim in clone.GetComponentsInChildren<Animator>(true))
                    Object.Destroy(anim);
                foreach (Animation anim in clone.GetComponentsInChildren<Animation>(true))
                    Object.Destroy(anim);
                foreach (Rigidbody rb in clone.GetComponentsInChildren<Rigidbody>(true))
                    Object.Destroy(rb);
                foreach (Collider col in clone.GetComponentsInChildren<Collider>(true))
                    Object.Destroy(col);
                return visual;
            }

            foreach (MeshFilter sourceFilter in item.GetComponentsInChildren<MeshFilter>())
            {
                GameObject child = new GameObject(sourceFilter.name);
                child.transform.SetParent(visual.transform, false);
                child.layer = layer;

                MeshFilter mf = child.AddComponent<MeshFilter>();
                mf.sharedMesh = sourceFilter.sharedMesh;

                MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
                if (sourceRenderer != null)
                {
                    MeshRenderer mr = child.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = sourceRenderer.sharedMaterials;
                }
            }

            return visual;
        }

        private static Transform FindCommonAncestor(Transform a, Transform b)
        {
            while (b != null && !IsAncestorOrSelf(a, b)) a = a.parent;
            return a;
        }

        private static bool IsAncestorOrSelf(Transform ancestor, Transform node)
        {
            while (node != null)
            {
                if (node == ancestor) return true;
                node = node.parent;
            }
            return false;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private void FitCameraToBounds(SlotPreview slot)
        {
            if (slot.visual == null) return;

            Bounds bounds = new Bounds(slot.visual.transform.position, Vector3.zero);
            foreach (Renderer renderer in slot.visual.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);

            float size = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.02f + 0.05f;
            slot.camera.orthographicSize = Mathf.Max(size, 0.2f);
        }

        private static void ExcludePreviewLayersFromMainCamera()
        {
            Camera main = Camera.main;
            if (main == null) return;

            int mask = ~((1 << PreviewLayerA) | (1 << PreviewLayerB));
            main.cullingMask &= mask;
        }
    }
}