using PrisaControlada.Core;
using PrisaControlada.Items;
using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PrisaControlada.Editor
{
    public static class PrototypeSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Prototype.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string MaterialsFolder = "Assets/Prototype/Materials";

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

            player.AddComponent<MicNoiseMeter>();
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

            if (player.GetComponent<InventoryHUD>() == null)
                player.AddComponent<InventoryHUD>();

            if (player.GetComponent<MicNoiseMeter>() == null)
                player.AddComponent<MicNoiseMeter>();
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
