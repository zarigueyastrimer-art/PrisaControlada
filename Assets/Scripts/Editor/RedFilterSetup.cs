using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PrisaControlada.Editor
{
    public static class RedFilterSetup
    {
        private const string RendererDataPath = "Assets/Settings/PC_Renderer.asset";
        private const string ProfilePath = "Assets/Prototype/UI/NoisePostFX.asset";

        public static void SetupRedFilter()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Setup Red Filter", "Save the scene first.", "OK");
                return;
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Setup Red Filter", "No Player found in the scene.", "OK");
                return;
            }

            EnsureRendererFeature();
            EnsureVolumeComponent();
            EnsureController(player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RedFilterSetup] Red filter ready (B&W keeping red, driven by health).");
        }

        private static void EnsureRendererFeature()
        {
            ScriptableRendererData data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererDataPath);
            if (data == null)
            {
                Debug.LogWarning("[RedFilterSetup] Renderer data not found at " + RendererDataPath);
                return;
            }

            SerializedObject so = new SerializedObject(data);
            SerializedProperty features = so.FindProperty("m_RendererFeatures");
            if (features == null) return;

            for (int i = 0; i < features.arraySize; i++)
            {
                Object existing = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing != null && existing.GetType() == typeof(RedFilterRendererFeature))
                    return;
            }

            RedFilterRendererFeature feature = ScriptableObject.CreateInstance<RedFilterRendererFeature>();
            feature.name = "RedFilterRendererFeature";
            AssetDatabase.AddObjectToAsset(feature, data);

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private static void EnsureVolumeComponent()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogWarning("[RedFilterSetup] Volume profile not found at " + ProfilePath);
                return;
            }

            if (!profile.TryGet<RedFilterVolumeComponent>(out _))
            {
                RedFilterVolumeComponent component = profile.Add<RedFilterVolumeComponent>(false);
                AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
        }

        private static void EnsureController(GameObject player)
        {
            RedFilterController controller = player.GetComponent<RedFilterController>();
            if (controller == null)
                controller = player.AddComponent<RedFilterController>();

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("minGamma").floatValue = 2f;
            so.FindProperty("maxBlur").floatValue = 0.55f;
            so.FindProperty("blurRadius").floatValue = 7f;
            so.ApplyModifiedProperties();
        }
    }
}