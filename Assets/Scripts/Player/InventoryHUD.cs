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

        [SerializeField] private InventoryController inventory;

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
        }

        private void Start()
        {
            if (inventory == null) return;

            ExcludePreviewLayersFromMainCamera();
            CreateCanvas();

            for (int i = 0; i < SlotCount; i++)
                _slots[i] = CreateSlot(i);

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
                PickupItem item = inventory.GetSlotItem(i);
                if (item != _slots[i].currentItem)
                    RebuildVisual(i, item);
            }
        }

        private void RebuildVisual(int index, PickupItem item)
        {
            SlotPreview slot = _slots[index];

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

        private void CreateCanvas()
        {
            GameObject canvasGo = new GameObject("InventoryHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject bar = new GameObject("SlotsBar");
            RectTransform barRect = bar.AddComponent<RectTransform>();
            bar.transform.SetParent(canvasGo.transform, false);
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 20f);

            HorizontalLayoutGroup layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            barRect.sizeDelta = new Vector2(SlotCount * 150f + (SlotCount - 1) * layout.spacing, 150f);
        }

        private SlotPreview CreateSlot(int index)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform barRect = transform.Find("InventoryHUD_Canvas/SlotsBar") as RectTransform;

            GameObject slotGo = new GameObject($"Slot{index + 1}");
            RectTransform slotRect = slotGo.AddComponent<RectTransform>();
            slotGo.transform.SetParent(barRect, false);
            slotRect.sizeDelta = new Vector2(150f, 150f);

            Image bg = slotGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);

            if (font != null)
            {
                GameObject keyGo = new GameObject("Key");
                RectTransform keyRect = keyGo.AddComponent<RectTransform>();
                keyGo.transform.SetParent(slotRect, false);
                keyRect.anchorMin = new Vector2(0.5f, 1f);
                keyRect.anchorMax = new Vector2(0.5f, 1f);
                keyRect.pivot = new Vector2(0.5f, 1f);
                keyRect.anchoredPosition = new Vector2(0f, -6f);
                keyRect.sizeDelta = new Vector2(60f, 24f);

                Text key = keyGo.AddComponent<Text>();
                key.text = index == 0 ? "Q" : "E";
                key.font = font;
                key.fontSize = 20;
                key.color = new Color(1f, 1f, 1f, 0.85f);
                key.alignment = TextAnchor.MiddleCenter;
                key.raycastTarget = false;
            }

            GameObject imgGo = new GameObject("Preview");
            RectTransform imgRect = imgGo.AddComponent<RectTransform>();
            imgGo.transform.SetParent(slotRect, false);
            imgRect.anchorMin = new Vector2(0.5f, 0f);
            imgRect.anchorMax = new Vector2(0.5f, 0f);
            imgRect.pivot = new Vector2(0.5f, 0f);
            imgRect.anchoredPosition = new Vector2(0f, 10f);
            imgRect.sizeDelta = new Vector2(140f, 120f);

            RawImage raw = imgGo.AddComponent<RawImage>();
            raw.raycastTarget = false;

            RenderTexture rt = new RenderTexture(PreviewWidth, PreviewHeight, 24, RenderTextureFormat.ARGB32);
            raw.texture = rt;

            GameObject rigGo = new GameObject($"PreviewCamera{index}");
            rigGo.transform.SetParent(transform, false);
            rigGo.transform.localPosition = new Vector3(index * 0.5f, 1.6f, -1f);

            Camera cam = rigGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.orthographic = true;
            cam.orthographicSize = 1f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 20f;
            cam.cullingMask = 1 << (index == 0 ? PreviewLayerA : PreviewLayerB);
            cam.targetTexture = rt;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.enabled = false;

            GameObject visualRootGo = new GameObject("VisualRoot");
            visualRootGo.transform.SetParent(rigGo.transform, false);
            visualRootGo.transform.localPosition = new Vector3(0f, 0f, PreviewDistance);

            return new SlotPreview
            {
                image = raw,
                texture = rt,
                camera = cam,
                visualRoot = visualRootGo.transform,
                layer = index == 0 ? PreviewLayerA : PreviewLayerB
            };
        }
    }
}