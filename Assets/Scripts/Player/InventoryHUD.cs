using PrisaControlada.Items;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            public CommandBuffer clearCommandBuffer;
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
                {
                    Debug.LogWarning($"[InventoryHUD] Slot {i}: previewCamera={previewCameras[i] != null}, slotImage={slotImages[i] != null}, visualRoot={visualRoots[i] != null}");
                    continue;
                }

                // RenderTexture con alpha (ARGB32 = 8 bits por canal, incluye alpha)
                // En URP: usar depth buffer de 24 bits y formato ARGB32 para transparencia correcta
                RenderTexture rt = new RenderTexture(PreviewWidth, PreviewHeight, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                previewCameras[i].targetTexture = rt;

                // Forzar ortografico para que orthographicSize tenga efecto real.
                previewCameras[i].orthographic = true;
                previewCameras[i].nearClipPlane = 0.01f;
                previewCameras[i].farClipPlane = 100f;

                // Fondo transparente en el preview (alpha = 0)
                // En URP: CameraClearFlags.SolidColor con alpha=0 SÍ funciona en RenderTexture
                previewCameras[i].clearFlags = CameraClearFlags.SolidColor;
                previewCameras[i].backgroundColor = new Color(0f, 0f, 0f, 0f);
                // Desactivar HDR y MSAA en la cámara de preview para evitar problemas de transparencia
                previewCameras[i].allowHDR = false;
                previewCameras[i].allowMSAA = false;

                // En URP 6000+: asegurar que la cámara tenga UniversalAdditionalCameraData para renderizar correctamente
                var urpCameraData = previewCameras[i].GetUniversalAdditionalCameraData();
                if (urpCameraData != null)
                {
                    urpCameraData.renderType = CameraRenderType.Base;
                    urpCameraData.renderPostProcessing = false;
                    urpCameraData.requiresColorOption = CameraOverrideOption.Off;
                    urpCameraData.requiresDepthOption = CameraOverrideOption.Off;
                }

                _slots[i] = new SlotPreview
                {
                    image = slotImages[i],
                    texture = rt,
                    camera = previewCameras[i],
                    visualRoot = visualRoots[i],
                    layer = i == 0 ? PreviewLayerA : PreviewLayerB
                };
                previewCameras[i].cullingMask = 1 << _slots[i].layer;
                Debug.Log($"[InventoryHUD] Slot {i} configurado: layer={_slots[i].layer}, camera={previewCameras[i].name}");

                // En URP: usar CommandBuffer para limpiar el RenderTexture con transparencia ANTES de renderizar
                // Esto es necesario porque Camera.clearFlags no siempre funciona correctamente en URP para RenderTextures
                CommandBuffer cb = new CommandBuffer();
                cb.name = $"ClearPreviewRT_{i}";
                cb.ClearRenderTarget(true, true, new Color(0f, 0f, 0f, 0f));
                _slots[i].clearCommandBuffer = cb;
                previewCameras[i].AddCommandBuffer(CameraEvent.BeforeForwardOpaque, cb);
                // También limpiar en BeforeSkybox por si acaso
                previewCameras[i].AddCommandBuffer(CameraEvent.BeforeSkybox, cb);

                // Configurar RawImage para transparencia
                if (slotImages[i] != null)
                {
                    slotImages[i].color = new Color(1f, 1f, 1f, 1f);
                    
                    // En URP: usar shader UI/Default (compatible con URP) y configurar blending correctamente
                    var mat = new Material(Shader.Find("UI/Default"));
                    if (mat != null)
                    {
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = 3000;
                        slotImages[i].material = mat;
                    }
                    else
                    {
                        Debug.LogWarning("[InventoryHUD] No se encontró shader UI/Default, usando material por defecto");
                    }

                    // Si hay un componente Image padre (fondo del slot), hacerlo transparente
                    var parentImage = slotImages[i].transform.parent?.GetComponent<UnityEngine.UI.Image>();
                    if (parentImage != null)
                    {
                        parentImage.color = new Color(1f, 1f, 1f, 0f); // Fondo transparente
                    }
                    // También el propio slot (el GameObject del RawImage puede tener Image)
                    var selfImage = slotImages[i].GetComponent<UnityEngine.UI.Image>();
                    if (selfImage != null)
                    {
                        selfImage.color = new Color(1f, 1f, 1f, 0f);
                    }
                }
            }

            foreach (SlotPreview slot in _slots)
            {
                if (slot == null) continue;
                slot.image.enabled = false;
                slot.camera.enabled = false;
                EnsurePreviewLight(slot);
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

        // Luz propia por camara de preview: asegura que el objeto del slot se vea
        // iluminado aunque la escena este completamente oscura.
        private static void EnsurePreviewLight(SlotPreview slot)
        {
            if (slot == null || slot.camera == null) return;

            Transform existing = slot.camera.transform.Find("PreviewLight");
            if (existing != null) return;

            GameObject lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(slot.camera.transform, false);
            // Ubicada delante de la camara, apuntando hacia el objeto (en -Z)
            lightGo.transform.localPosition = new Vector3(0f, 0.5f, -2f);
            lightGo.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);

            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 4f; // Más intenso para materiales metálicos
            light.shadows = LightShadows.None;
            // Solo ilumina las capas del preview (11 o 12), nunca la escena
            light.cullingMask = 1 << slot.layer;

            // Segunda luz: fill light desde abajo/atras para evitar caras negras
            GameObject fillGo = new GameObject("PreviewFillLight");
            fillGo.transform.SetParent(slot.camera.transform, false);
            fillGo.transform.localPosition = new Vector3(0f, -0.5f, 2f);
            fillGo.transform.localRotation = Quaternion.Euler(-30f, 180f, 0f);

            Light fillLight = fillGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.8f, 0.8f, 1f); // Ligeramente azulado
            fillLight.intensity = 1.5f;
            fillLight.shadows = LightShadows.None;
            fillLight.cullingMask = 1 << slot.layer;
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.InventoryChanged -= RefreshAll;

            foreach (SlotPreview slot in _slots)
            {
                if (slot != null)
                {
                    if (slot.texture != null)
                        slot.texture.Release();
                    if (slot.clearCommandBuffer != null && slot.camera != null)
                    {
                        slot.camera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, slot.clearCommandBuffer);
                        slot.camera.RemoveCommandBuffer(CameraEvent.BeforeSkybox, slot.clearCommandBuffer);
                        slot.clearCommandBuffer.Dispose();
                    }
                }
            }
        }

        private void RefreshAll()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == null) continue;

                PickupItem item = inventory.GetSlotItem(i);
                if (item != _slots[i].currentItem)
                {
                    Debug.Log($"[InventoryHUD] RefreshAll slot {i}: item cambió a {(item != null ? item.name : "null")}");
                    RebuildVisual(i, item);
                }
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

            Debug.Log($"[InventoryHUD] RebuildVisual slot {index}: item={item.name}, type={item.GetType().Name}");

            slot.image.enabled = true;
            slot.image.texture = slot.texture;
            // Forzar color blanco con alpha completo en la RawImage
            slot.image.color = new Color(1f, 1f, 1f, 1f);
            
            slot.visual = CreateVisualFrom(item, slot.layer);
            slot.visual.transform.SetParent(slot.visualRoot, false);
            slot.visual.transform.localPosition = Vector3.zero;
            slot.visual.transform.localRotation = Quaternion.identity;
            slot.visual.transform.localScale = item.PreviewScale;

            // Centrar el pivot ANTES de crear el TiltPivot, para que las mallas
            // queden centradas respecto al origen y la cámara las encuadre.
            CenterVisualPivot(slot.visual);

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
            Debug.Log($"[InventoryHUD] CreateVisualFrom: item={item.name}, renderers encontrados={renderers.Length}, layer={layer}");
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
                Debug.Log($"[InventoryHUD] Skinned visual creado: {clone.name}, hijos={clone.transform.childCount}");
                return visual;
            }

            int meshCount = 0;
            foreach (MeshFilter sourceFilter in item.GetComponentsInChildren<MeshFilter>())
            {
                if (sourceFilter.sharedMesh == null)
                {
                    Debug.LogWarning($"[InventoryHUD] MeshFilter sin mesh: {sourceFilter.name}");
                    continue;
                }
                meshCount++;
                GameObject child = new GameObject(sourceFilter.name);
                child.transform.SetParent(visual.transform, false);
                child.layer = layer;

                // NO copiar localPosition/rotation/scale del MeshFilter original:
                // el modelo del pickup puede estar desplazado (ej: y=14.39) para
                // aparecer en la mano del jugador. Usamos identity y centramos
                // todo con CenterVisualPivot después.
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale = Vector3.one;

                MeshFilter mf = child.AddComponent<MeshFilter>();
                mf.sharedMesh = sourceFilter.sharedMesh;

                MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
                if (sourceRenderer != null)
                {
                    MeshRenderer mr = child.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = sourceRenderer.sharedMaterials;
                    Debug.Log($"[InventoryHUD] Mesh copiado: {sourceFilter.name}, mesh={sourceFilter.sharedMesh.name}, mats={sourceRenderer.sharedMaterials.Length}");
                }
            }

            if (meshCount == 0)
            {
                Debug.LogError($"[InventoryHUD] NO se copió ninguna malla para {item.name} - el preview será un cuadrado negro");
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

            Debug.Log($"[InventoryHUD] FitCameraToBounds: bounds={bounds}, size={bounds.size}");

            float size = Mathf.Max(bounds.extents.x, bounds.extents.y) * 2f + 0.05f;
            slot.camera.orthographicSize = Mathf.Max(size, 0.15f);
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