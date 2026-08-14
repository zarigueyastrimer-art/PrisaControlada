using PrisaControlada.Core;
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
