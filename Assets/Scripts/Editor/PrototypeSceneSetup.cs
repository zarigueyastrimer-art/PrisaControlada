using PrisaControlada.Core;
using PrisaControlada.Items;
using PrisaControlada.Player;
using System.Collections.Generic;
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
        private const string FurnitureFbxPath = "Assets/MyAssets/mueblexample.fbx";
        private const string CrosshairSpritePath = "Assets/VHSEffectURP/hud/Circle.png";
        private const string FondoBalasSpritePath = "Assets/VHSEffectURP/hud/fondoBalas.png";
        private const string BalasRevolverSpritePath = "Assets/VHSEffectURP/hud/balas revolver.png";

        private const int SlotCount = 2;
        private const int PreviewLayerA = 11;
        private const int PreviewLayerB = 12;
        private const float PreviewDistance = 4f;
        private const int DrawerLayer = 10;

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
            CreateFurniture();
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
            controller.excludeLayers = 1 << DrawerLayer;

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
            EnsurePlayerIgnoresDrawerLayer(scene);
        }

        private static void EnsurePlayerUI(GameObject player)
        {
            EnsureInventoryHUDUI(player);
            EnsureMicNoiseMeterUI(player);
            EnsureCrosshairUI(player);
            EnsureWeaponHUDUI(player);
        }

        private static void EnsureWeaponHUDUI(GameObject player)
        {
            Transform playerT = player.transform;

            Transform canvasT = playerT.Find("WeaponHUD_Canvas");
            if (canvasT == null)
            {
                RectTransform canvasRect = CreateRect("WeaponHUD_Canvas", playerT);
                canvasT = canvasRect;
                Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 250;

                CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            }

            Image fondo = EnsureFullScreenImage(canvasT, "Fondo", EnsureSprite(FondoBalasSpritePath));
            Image balas = EnsureFullScreenImage(canvasT, "Balas", EnsureSprite(BalasRevolverSpritePath));

            Transform counterT = canvasT.Find("ReserveCounter");
            if (counterT == null)
            {
                RectTransform counterRect = CreateRect("ReserveCounter", canvasT);
                counterRect.anchorMin = new Vector2(0f, 1f);
                counterRect.anchorMax = new Vector2(0f, 1f);
                counterRect.pivot = new Vector2(0f, 1f);
                counterRect.anchoredPosition = new Vector2(48f, -48f);
                counterRect.sizeDelta = new Vector2(300f, 100f);

                Text counter = counterRect.gameObject.AddComponent<Text>();
                counter.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                counter.fontSize = 72;
                counter.fontStyle = FontStyle.Bold;
                counter.alignment = TextAnchor.MiddleLeft;
                counter.color = Color.white;
                counter.raycastTarget = false;
                counter.text = "0";

                counterT = counterRect;
            }

            WeaponHUD hud = player.GetComponent<WeaponHUD>();
            if (hud == null) hud = player.AddComponent<WeaponHUD>();

            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("weapon").objectReferenceValue = player.GetComponentInChildren<WeaponController>();
            so.FindProperty("canvas").objectReferenceValue = canvasT.GetComponent<Canvas>();
            so.FindProperty("counterText").objectReferenceValue = counterT.GetComponent<Text>();
            so.ApplyModifiedProperties();

            canvasT.gameObject.SetActive(false);
        }

        private static Image EnsureFullScreenImage(Transform parent, string name, Sprite sprite)
        {
            Transform t = parent.Find(name);
            Image image;
            if (t == null)
            {
                RectTransform rect = CreateRect(name, parent);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                image = rect.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
            }
            else
            {
                image = t.GetComponent<Image>();
            }

            if (sprite != null && image != null)
                image.sprite = sprite;

            return image;
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

        public static void AddCrosshairToScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Crosshair", "Save the scene first.", "OK");
                return;
            }

            GameObject player = FindPlayer(scene);
            if (player == null)
            {
                EditorUtility.DisplayDialog("Crosshair", "No Player found in the scene.", "OK");
                return;
            }

            EnsureCrosshairUI(player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Tools/PrisaControlada/UI/Setup Weapon HUD")]
        public static void AddWeaponHudToScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Weapon HUD", "Save the scene first.", "OK");
                return;
            }

            GameObject player = FindPlayer(scene);
            if (player == null)
            {
                EditorUtility.DisplayDialog("Weapon HUD", "No Player found in the scene.", "OK");
                return;
            }

            EnsureWeaponHUDUI(player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureCrosshairUI(GameObject player)
        {
            Transform playerT = player.transform;

            Transform canvasT = playerT.Find("Crosshair_Canvas");
            if (canvasT == null)
            {
                RectTransform canvasRect = CreateRect("Crosshair_Canvas", playerT);
                canvasT = canvasRect;
                Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 300;

                CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                CanvasGroup group = canvasRect.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            Image crosshairImage = null;
            Transform imageT = canvasT.Find("Crosshair");
            if (imageT == null)
            {
                RectTransform imageRect = CreateRect("Crosshair", canvasT);
                imageRect.anchorMin = new Vector2(0.5f, 0.5f);
                imageRect.anchorMax = new Vector2(0.5f, 0.5f);
                imageRect.pivot = new Vector2(0.5f, 0.5f);
                imageRect.anchoredPosition = Vector2.zero;
                imageRect.sizeDelta = new Vector2(64f, 64f);

                crosshairImage = imageRect.gameObject.AddComponent<Image>();
                crosshairImage.preserveAspect = true;
                crosshairImage.raycastTarget = false;

                imageT = imageRect;
            }
            else
            {
                crosshairImage = imageT.GetComponent<Image>();
            }

            Sprite sprite = EnsureCrosshairSprite();
            if (sprite != null && crosshairImage != null)
                crosshairImage.sprite = sprite;

            InteractionCrosshair crosshair = canvasT.GetComponent<InteractionCrosshair>();
            if (crosshair == null)
                crosshair = canvasT.gameObject.AddComponent<InteractionCrosshair>();

            Camera cam = player.GetComponentInChildren<Camera>();

            SerializedObject so = new SerializedObject(crosshair);
            so.FindProperty("viewCamera").objectReferenceValue = cam;
            so.FindProperty("crosshairImage").objectReferenceValue = crosshairImage;
            so.ApplyModifiedProperties();
        }

        private static Sprite EnsureCrosshairSprite()
        {
            return EnsureSprite(CrosshairSpritePath);
        }

        private static Sprite EnsureSprite(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogWarning($"Texture not found at {path}");
                return null;
            }

            string assetPath = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void AddFurnitureToScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Furniture", "Save the scene first.", "OK");
                return;
            }

            EnsurePlayerIgnoresDrawerLayer(scene);
            CreateFurniture();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsurePlayerIgnoresDrawerLayer(Scene scene)
        {
            if (scene == null || !scene.IsValid()) return;

            GameObject player = FindPlayer(scene);
            if (player == null) return;

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller == null) return;

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty prop = so.FindProperty("m_ExcludeLayers");
            if (prop != null)
            {
                prop.intValue |= (1 << DrawerLayer);
                so.ApplyModifiedProperties();
            }
        }

        private static GameObject FindPlayer(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Player")
                    return root;
            }

            return null;
        }

        private static GameObject CreateFurniture()
        {
            GameObject furniture = GameObject.Find("Mueble");

            if (furniture == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FurnitureFbxPath);
                if (prefab == null)
                {
                    EditorUtility.DisplayDialog("Furniture", $"FBX not found at {FurnitureFbxPath}", "OK");
                    return null;
                }

                furniture = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                furniture.name = "Mueble";
                furniture.transform.position = new Vector3(2f, 0f, 4f);
            }

            Transform body = null;
            List<Transform> drawerNodes = new List<Transform>();
            float largestVolume = -1f;

            Transform[] allTransforms = furniture.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allTransforms)
            {
                if (child == furniture.transform) continue;
                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer == null) continue;

                float volume = renderer.bounds.size.x * renderer.bounds.size.y * renderer.bounds.size.z;
                if (volume > largestVolume)
                {
                    largestVolume = volume;
                    body = child;
                }
            }

            foreach (Transform child in allTransforms)
            {
                if (child == body) continue;
                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer == null) continue;
                drawerNodes.Add(child);
            }

            if (body == null || drawerNodes.Count == 0)
            {
                Debug.LogWarning("Furniture: could not detect body/drawer nodes from the FBX.");
                return furniture;
            }

            body.gameObject.layer = 0;
            FitBoxCollider(GetOrAddBoxCollider(body), body);

            DrawerController drawerController = furniture.GetComponent<DrawerController>();
            if (drawerController == null)
                drawerController = furniture.AddComponent<DrawerController>();

            SerializedObject so = new SerializedObject(drawerController);
            SerializedProperty drawersProp = so.FindProperty("drawers");
            drawersProp.arraySize = drawerNodes.Count;

            for (int i = 0; i < drawerNodes.Count; i++)
            {
                Transform node = drawerNodes[i];
                node.gameObject.layer = DrawerLayer;

                BoxCollider box = GetOrAddBoxCollider(node);
                FitBoxCollider(box, node);
                ExtendDrawerCollider(box, body, node);

                SerializedProperty element = drawersProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("drawer").objectReferenceValue = node;
            }

            so.ApplyModifiedProperties();
            return furniture;
        }

        private static BoxCollider GetOrAddBoxCollider(Transform target)
        {
            BoxCollider box = target.GetComponent<BoxCollider>();
            if (box != null)
                return box;

            Collider other = target.GetComponent<Collider>();
            if (other != null)
                Object.DestroyImmediate(other);

            return target.gameObject.AddComponent<BoxCollider>();
        }

        private static Bounds GetRenderBounds(Transform target)
        {
            Bounds bounds = new Bounds(target.position, Vector3.zero);
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);

            return bounds;
        }

        private static float HalfExtentAlong(Vector3 extents, Vector3 direction)
        {
            return extents.x * Mathf.Abs(direction.x)
                 + extents.y * Mathf.Abs(direction.y)
                 + extents.z * Mathf.Abs(direction.z);
        }

        private static void ExtendDrawerCollider(BoxCollider collider, Transform body, Transform drawer)
        {
            Bounds bodyBounds = GetRenderBounds(body);
            Bounds drawerBounds = GetRenderBounds(drawer);

            Vector3 away = drawerBounds.center - bodyBounds.center;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = drawer.forward;

            away.Normalize();

            float bodyFront = Vector3.Dot(bodyBounds.center, away) + HalfExtentAlong(bodyBounds.extents, away);
            float drawerFront = Vector3.Dot(drawerBounds.center, away) + HalfExtentAlong(drawerBounds.extents, away);

            float extension = (bodyFront - drawerFront) + 0.1f;
            if (extension <= 0f)
                return;

            Vector3 localAway = drawer.InverseTransformDirection(away);
            int axis = 0;
            float maxAbs = -1f;
            for (int i = 0; i < 3; i++)
            {
                float abs = Mathf.Abs(localAway[i]);
                if (abs > maxAbs)
                {
                    maxAbs = abs;
                    axis = i;
                }
            }

            float scale = Mathf.Abs(drawer.lossyScale[axis]);
            if (scale < 0.0001f)
                scale = 1f;

            float localExtension = extension / scale;

            Vector3 center = collider.center;
            Vector3 size = collider.size;
            float sign = Mathf.Sign(localAway[axis]);

            center[axis] += sign * localExtension * 0.5f;
            size[axis] += localExtension;

            collider.center = center;
            collider.size = size;
        }

        private static void FitBoxCollider(BoxCollider collider, Transform target)
        {
            Bounds bounds = new Bounds(target.position, Vector3.zero);
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            collider.center = target.InverseTransformPoint(bounds.center);
            collider.size = new Vector3(
                bounds.size.x / Mathf.Max(target.lossyScale.x, 0.0001f),
                bounds.size.y / Mathf.Max(target.lossyScale.y, 0.0001f),
                bounds.size.z / Mathf.Max(target.lossyScale.z, 0.0001f));
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
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

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
