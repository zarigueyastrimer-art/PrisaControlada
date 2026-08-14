using PrisaControlada.Core;
using PrisaControlada.Items;
using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    public static class PrototypeSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Prototype.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string MaterialsFolder = "Assets/Prototype/Materials";
        private const string UIFolder = "Assets/Prototype/UI";
        private const string PostFxProfilePath = "Assets/Prototype/UI/NoisePostFX.asset";

        private const int SlotCount = 2;
        private const int PreviewLayerA = 8;
        private const int PreviewLayerB = 9;
        private const float PreviewDistance = 4f;

        [MenuItem("Tools/PrisaControlada/Setup Prototype Scene")]
        public static void CreatePrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Prototype";

            EnsureFolder(MaterialsFolder);

            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            CreateGround();
            CreateReferenceCubes();
            CreatePlayer(inputActions);
            CreatePickupTestCubes();
            CreateLight();

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.18f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.04f, 0.04f, 0.05f);
            RenderSettings.fogDensity = 0.006f;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            SetColliderLayers(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Ground", new Color(0.12f, 0.12f, 0.14f));
        }

        private static void CreateReferenceCubes()
        {
            CreatePillar("RefRed", new Vector3(24f, 0f, 24f), new Color(0.8f, 0.1f, 0.1f));
            CreatePillar("RefGreen", new Vector3(24f, 0f, -24f), new Color(0.1f, 0.7f, 0.2f));
            CreatePillar("RefBlue", new Vector3(-24f, 0f, 24f), new Color(0.1f, 0.3f, 0.8f));
            CreatePillar("RefYellow", new Vector3(-24f, 0f, -24f), new Color(0.8f, 0.7f, 0.1f));
        }

        private static void CreatePillar(string name, Vector3 position, Color color)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = name;
            pillar.transform.position = position + Vector3.up * 1f;
            pillar.transform.localScale = new Vector3(0.4f, 2f, 0.4f);
            SetColliderLayers(pillar.GetComponent<Collider>());
            pillar.GetComponent<Renderer>().sharedMaterial = CreateMaterial(name, color);
        }

        private static void CreatePlayer(InputActionAsset inputActions)
        {
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.05f, 0f);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.includeLayers = Physics.AllLayers;
            controller.excludeLayers = 0;

            PlayerController playerController = player.AddComponent<PlayerController>();

            GameObject pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(player.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            GameObject cameraGo = new GameObject("PlayerCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(pivot.transform, false);

            Camera cam = cameraGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<PlayerCamera>();
            cameraGo.AddComponent<BodycamShake>();

            AudioSource audioSource = player.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;

            player.AddComponent<QuitOnEscape>();

            SerializedObject so = new SerializedObject(playerController);
            so.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
            so.FindProperty("inputActions").objectReferenceValue = inputActions;
            so.FindProperty("footstepAudioSource").objectReferenceValue = audioSource;
            so.ApplyModifiedProperties();

            InventoryController inventory = player.AddComponent<InventoryController>();
            SerializedObject soInv = new SerializedObject(inventory);
            soInv.FindProperty("viewCamera").objectReferenceValue = cam;
            soInv.FindProperty("inputActions").objectReferenceValue = inputActions;
            soInv.ApplyModifiedProperties();

            EnsurePlayerUI(player);
        }

        private static void SetColliderLayers(Collider collider)
        {
            if (collider == null) return;
            collider.includeLayers = Physics.AllLayers;
            collider.excludeLayers = 0;
        }

        private static void CreateLight()
        {
            GameObject lightGo = new GameObject("Sun");
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.86f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
        }

        [MenuItem("Tools/PrisaControlada/Add Pickup Test Cubes")]
        public static void AddPickupTestCubesToScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Pickup Test Cubes", "Save the scene first.", "OK");
                return;
            }

            EnsurePlayerHasInventory(scene);
            CreatePickupTestCubes();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsurePlayerHasInventory(Scene scene)
        {
            if (scene == null || !scene.IsValid()) return;

            GameObject player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Player")
                {
                    player = root;
                    break;
                }
            }

            if (player == null)
                return;

            if (player.GetComponent<InventoryController>() == null)
            {
                InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
                Camera cam = player.GetComponentInChildren<Camera>();

                InventoryController inventory = player.AddComponent<InventoryController>();
                SerializedObject so = new SerializedObject(inventory);
                so.FindProperty("viewCamera").objectReferenceValue = cam;
                so.FindProperty("inputActions").objectReferenceValue = inputActions;
                so.ApplyModifiedProperties();
            }

            EnsurePlayerUI(player);
        }

        private static void EnsurePlayerUI(GameObject player)
        {
            EnsureInventoryHUDUI(player);
            EnsureMicNoiseMeterUI(player);
        }

        private static void EnsureInventoryHUDUI(GameObject player)
        {
            Transform playerT = player.transform;

            Transform canvasT = playerT.Find("InventoryHUD_Canvas");
            if (canvasT == null)
            {
                RectTransform canvasRect = CreateRect("InventoryHUD_Canvas", playerT);
                canvasT = canvasRect;
                Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            }

            Transform barT = canvasT.Find("SlotsBar");
            if (barT == null)
            {
                RectTransform barRect = CreateRect("SlotsBar", canvasT);
                barRect.anchorMin = new Vector2(0.5f, 0f);
                barRect.anchorMax = new Vector2(0.5f, 0f);
                barRect.pivot = new Vector2(0.5f, 0f);
                barRect.anchoredPosition = new Vector2(0f, 20f);
                barRect.sizeDelta = new Vector2(340f, 150f);

                HorizontalLayoutGroup layout = barRect.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 40f;
                layout.childAlignment = TextAnchor.MiddleCenter;

                barT = barRect;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RawImage[] slotImages = new RawImage[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                string slotName = $"Slot{i + 1}";
                Transform slotT = barT.Find(slotName);
                if (slotT == null)
                {
                    RectTransform slotRect = CreateRect(slotName, barT);
                    slotRect.sizeDelta = new Vector2(150f, 150f);
                    slotRect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

                    RectTransform keyRect = CreateRect("Key", slotRect);
                    keyRect.anchorMin = new Vector2(0.5f, 1f);
                    keyRect.anchorMax = new Vector2(0.5f, 1f);
                    keyRect.pivot = new Vector2(0.5f, 1f);
                    keyRect.anchoredPosition = new Vector2(0f, -4f);
                    keyRect.sizeDelta = new Vector2(60f, 22f);

                    Text keyText = keyRect.gameObject.AddComponent<Text>();
                    keyText.text = i == 0 ? "Q" : "E";
                    keyText.font = font;
                    keyText.fontSize = 14;
                    keyText.fontStyle = FontStyle.Bold;
                    keyText.alignment = TextAnchor.MiddleCenter;
                    keyText.color = Color.white;
                    keyText.raycastTarget = false;

                    RectTransform previewRect = CreateRect("Preview", slotRect);
                    previewRect.anchorMin = new Vector2(0f, 0f);
                    previewRect.anchorMax = new Vector2(1f, 1f);
                    previewRect.offsetMin = new Vector2(8f, 8f);
                    previewRect.offsetMax = new Vector2(-8f, -8f);

                    RawImage raw = previewRect.gameObject.AddComponent<RawImage>();
                    raw.color = Color.white;
                    raw.raycastTarget = false;
                }

                slotImages[i] = barT.Find($"{slotName}/Preview")?.GetComponent<RawImage>();
            }

            Camera[] previewCameras = new Camera[SlotCount];
            Transform[] visualRoots = new Transform[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                string rigName = $"PreviewCamera{i}";
                Transform rigT = playerT.Find(rigName);
                if (rigT == null)
                {
                    GameObject rig = new GameObject(rigName);
                    rig.transform.SetParent(playerT, false);
                    rig.transform.localPosition = new Vector3(i * 0.5f, 1.6f, -1f);

                    Camera cam = rig.AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                    cam.orthographic = true;
                    cam.orthographicSize = 1f;
                    cam.nearClipPlane = 0.3f;
                    cam.farClipPlane = 20f;
                    cam.cullingMask = 1 << (i == 0 ? PreviewLayerA : PreviewLayerB);
                    cam.allowHDR = false;
                    cam.allowMSAA = false;
                    cam.enabled = false;

                    Transform rootT = new GameObject("VisualRoot").transform;
                    rootT.SetParent(rig.transform, false);
                    rootT.localPosition = new Vector3(0f, 0f, PreviewDistance);

                    rigT = rig.transform;
                }

                previewCameras[i] = rigT.GetComponent<Camera>();
                visualRoots[i] = rigT.Find("VisualRoot");
            }

            InventoryHUD hud = player.GetComponent<InventoryHUD>();
            if (hud == null) hud = player.AddComponent<InventoryHUD>();

            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("inventory").objectReferenceValue = player.GetComponent<InventoryController>();
            so.FindProperty("canvas").objectReferenceValue = canvasT.GetComponent<Canvas>();
            so.FindProperty("slotsBar").objectReferenceValue = barT as RectTransform;

            SerializedProperty imgs = so.FindProperty("slotImages");
            imgs.arraySize = SlotCount;
            SerializedProperty cams = so.FindProperty("previewCameras");
            cams.arraySize = SlotCount;
            SerializedProperty roots = so.FindProperty("visualRoots");
            roots.arraySize = SlotCount;
            for (int i = 0; i < SlotCount; i++)
            {
                imgs.GetArrayElementAtIndex(i).objectReferenceValue = slotImages[i];
                cams.GetArrayElementAtIndex(i).objectReferenceValue = previewCameras[i];
                roots.GetArrayElementAtIndex(i).objectReferenceValue = visualRoots[i];
            }

            so.ApplyModifiedProperties();
        }

        private static void EnsureMicNoiseMeterUI(GameObject player)
        {
            Transform playerT = player.transform;

            Transform canvasT = playerT.Find("MicNoiseMeter_Canvas");
            if (canvasT == null)
            {
                RectTransform canvasRect = CreateRect("MicNoiseMeter_Canvas", playerT);
                canvasT = canvasRect;
                Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;

                CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            }

            Image fill = null;
            Transform barT = canvasT.Find("Bar");
            if (barT == null)
            {
                RectTransform barRect = CreateRect("Bar", canvasT);
                barRect.anchorMin = new Vector2(0.5f, 0f);
                barRect.anchorMax = new Vector2(0.5f, 0f);
                barRect.pivot = new Vector2(0.5f, 0f);
                barRect.anchoredPosition = new Vector2(0f, 200f);
                barRect.sizeDelta = new Vector2(500f, 36f);

                barRect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

                RectTransform fillRect = CreateRect("Fill", barRect);
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.sizeDelta = new Vector2(500f, 0f);

                fill = fillRect.gameObject.AddComponent<Image>();
                fill.color = new Color(0.4f, 0.9f, 0.4f, 1f);
                fill.raycastTarget = false;

                barT = barRect;
            }
            else
            {
                fill = barT.Find("Fill")?.GetComponent<Image>();
            }

            Volume volume = null;
            Camera cam = player.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                volume = cam.GetComponent<Volume>();
                if (volume == null)
                    volume = cam.gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 100f;

                EnsureFolder(UIFolder);

                VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxProfilePath);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile, PostFxProfilePath);
                }
                else
                {
                    profile.components.RemoveAll(c => c == null);
                }

                bool changed = false;

                if (!profile.TryGet<ColorAdjustments>(out ColorAdjustments _))
                {
                    ColorAdjustments ca = profile.Add<ColorAdjustments>(true);
                    ca.colorFilter.value = Color.white;
                    ca.saturation.value = 0f;
                    AssetDatabase.AddObjectToAsset(ca, profile);
                    changed = true;
                }

                if (!profile.TryGet<Vignette>(out Vignette _))
                {
                    Vignette vg = profile.Add<Vignette>(true);
                    vg.intensity.value = 0f;
                    vg.smoothness.value = 0.271f;
                    vg.color.value = new Color(1f, 0.15f, 0.15f, 1f);
                    AssetDatabase.AddObjectToAsset(vg, profile);
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssets();
                }

                volume.sharedProfile = profile;
            }

            MicNoiseMeter meter = player.GetComponent<MicNoiseMeter>();
            if (meter == null) meter = player.AddComponent<MicNoiseMeter>();

            SerializedObject so = new SerializedObject(meter);
            so.FindProperty("canvas").objectReferenceValue = canvasT.GetComponent<Canvas>();
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("postFxVolume").objectReferenceValue = volume;
            so.ApplyModifiedProperties();

            canvasT.gameObject.SetActive(false);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void CreatePickupTestCubes()
        {
            Material[] materials =
            {
                CreateMaterial("RefRed", new Color(0.8f, 0.1f, 0.1f)),
                CreateMaterial("RefGreen", new Color(0.1f, 0.7f, 0.2f)),
                CreateMaterial("RefBlue", new Color(0.1f, 0.3f, 0.8f)),
                CreateMaterial("RefYellow", new Color(0.8f, 0.7f, 0.1f))
            };

            for (int i = 0; i < materials.Length; i++)
            {
                string cubeName = $"PickupCube_{i + 1}";
                if (GameObject.Find(cubeName) != null)
                    continue;

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = cubeName;
                cube.transform.position = new Vector3(1.5f + i * 1.2f, 0.5f, 3f);
                cube.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                cube.GetComponent<Renderer>().sharedMaterial = materials[i];

                Rigidbody rb = cube.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                cube.AddComponent<PickupItem>();
            }
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader);
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = slash > 0 ? path.Substring(0, slash) : "Assets";
            string leaf = path.Substring(slash + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
