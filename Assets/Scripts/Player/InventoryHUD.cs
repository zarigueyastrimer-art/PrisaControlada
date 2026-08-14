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
        private const int PreviewLayerA = 8;
        private const int PreviewLayerB = 9;

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
                    slot.visual.transform.Rotate(0f, RotationSpeed * Time.deltaTime, 0f, Space.Self);
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

            slot.camera.enabled = true;
            FitCameraToBounds(slot);
        }

        private static GameObject CreateVisualFrom(PickupItem item, int layer)
        {
            GameObject visual = new GameObject("PreviewVisual");
            visual.layer = layer;

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