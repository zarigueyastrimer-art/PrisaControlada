using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PrisaControlada.Editor
{
    /// <summary>
    /// Configura el efecto de visión escotópica (ScotopicVision) en el proyecto.
    /// - Añade el Render Feature al URP Renderer
    /// - Añade DarkAdaptation + ScotopicVisionController al PlayerCamera
    /// - Configura Volume global en la escena
    /// NO añade FlashlightController (el juego ya tiene su propia linterna).
    /// </summary>
    public static class SetupScotopicVision
    {
        private const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string PlayerPrefabPath = "Assets/Prefabs/player/Player.prefab";
        private const string VolumeProfilePath = "Assets/Settings/SampleSceneProfile.asset";

        [MenuItem("Tools/PrisaControlada/Setup Scotopic Vision (Player)", false, 300)]
        public static void Setup()
        {
            Debug.Log("[ScotopicVisionSetup] Iniciando configuración...");

            // 1. Añadir Render Feature al URP Renderer
            AddRenderFeatureToRenderer();

            // 2. Configurar PlayerCamera en el prefab Player
            SetupPlayerCamera();

            // 3. Asegurar Volume global en escenas abiertas
            SetupGlobalVolume();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ScotopicVisionSetup] Configuración completada. Revisa el PlayerCamera en el prefab Player.");
        }

        // ------------------------------------------------------------------ Render Feature
        private static void AddRenderFeatureToRenderer()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                Debug.LogError($"[ScotopicVisionSetup] No se encontró el renderer en {RendererPath}");
                return;
            }

            // Buscar si ya existe la feature
            var existingFeature = renderer.rendererFeatures.Find(f => f != null && f.name == "ScotopicVisionFeature");
            if (existingFeature != null)
            {
                Debug.Log("[ScotopicVisionSetup] ScotopicVisionFeature ya existe en el renderer.");
                return;
            }

            // Buscar el shader
            var shader = Shader.Find("Hidden/PCHZ/ScotopicVision");
            if (shader == null)
            {
                Debug.LogError("[ScotopicVisionSetup] Shader Hidden/PCHZ/ScotopicVision no encontrado. Revisa que el archivo ScotopicVision.shader esté en el proyecto.");
                return;
            }

            // Buscar o crear la feature como asset persistente
            var featurePath = "Assets/Settings/ScotopicVisionFeature.asset";
            var feature = AssetDatabase.LoadAssetAtPath<ScotopicVisionFeature>(featurePath);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<ScotopicVisionFeature>();
                feature.name = "ScotopicVisionFeature";
                AssetDatabase.CreateAsset(feature, featurePath);
                AssetDatabase.SaveAssets();
            }

            // Asignar shader via reflection (settings es private)
            var settingsField = typeof(ScotopicVisionFeature).GetField("settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (settingsField != null)
            {
                var settings = settingsField.GetValue(feature);
                var shaderField = settings.GetType().GetField("shader");
                if (shaderField != null)
                {
                    shaderField.SetValue(settings, shader);
                    EditorUtility.SetDirty(feature);
                }
            }

            // Añadir al renderer usando serializedObject (m_RendererFeatures es el nombre serializado)
            var so = new SerializedObject(renderer);
            var featuresProp = so.FindProperty("m_RendererFeatures");
            if (featuresProp != null)
            {
                int idx = featuresProp.arraySize;
                featuresProp.arraySize++;
                featuresProp.GetArrayElementAtIndex(idx).objectReferenceValue = feature;
                so.ApplyModifiedProperties();
                Debug.Log($"[ScotopicVisionSetup] ScotopicVisionFeature añadida al renderer PC_Renderer (índice {idx}).");
            }
            else
            {
                Debug.LogError("[ScotopicVisionSetup] No se encontró m_RendererFeatures en el renderer.");
            }

            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(); // Importante para que Unity registre el cambio
        }

        // ------------------------------------------------------------------ Player Camera
        private static void SetupPlayerCamera()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[ScotopicVisionSetup] No se encontró el prefab Player en {PlayerPrefabPath}");
                return;
            }

            // Buscar PlayerCamera (hijo del Player root, tag MainCamera)
            Transform playerCameraTransform = null;
            foreach (Transform child in prefab.transform)
            {
                if (child.name == "PlayerCamera" || child.CompareTag("MainCamera"))
                {
                    playerCameraTransform = child;
                    break;
                }
            }

            if (playerCameraTransform == null)
            {
                Debug.LogError("[ScotopicVisionSetup] No se encontró PlayerCamera en el prefab Player.");
                return;
            }

            var cameraObj = playerCameraTransform.gameObject;

            // Añadir/obtener DarkAdaptation
            var adaptation = cameraObj.GetComponent<DarkAdaptation>();
            if (adaptation == null)
            {
                adaptation = cameraObj.AddComponent<DarkAdaptation>();
                Debug.Log("[ScotopicVisionSetup] DarkAdaptation añadido a PlayerCamera.");
            }

            // Configurar DarkAdaptation
            adaptation.targetCamera = cameraObj.GetComponent<Camera>();
            // El Volume se resuelve solo en Awake, pero podemos asignar uno si existe en la escena
            // adaptation.volume = ... (opcional)

            // Añadir/obtener ScotopicVisionController
            var controller = cameraObj.GetComponent<ScotopicVisionController>();
            if (controller == null)
            {
                controller = cameraObj.AddComponent<ScotopicVisionController>();
                Debug.Log("[ScotopicVisionSetup] ScotopicVisionController añadido a PlayerCamera.");
            }

            // IMPORTANTE: Desactivar todo lo relacionado con la linterna del efecto
            // porque el juego ya tiene su propia linterna
            controller.turnOffWithFlashlight = false;
            controller.darkenAmbientWithFlashlight = false;
            // flashlight es [SerializeField] private, no se puede asignar desde aquí.
            // En Awake() el controlador hace GetComponentInChildren<FlashlightController>() 
            // y no encontrará ninguno (el juego usa su propia linterna).

            // Configuración recomendada para interiores oscuros sin linterna del efecto
            controller.filterEnabled = true;
            controller.filterStrength = 1f;
            controller.filterGamma = 1f;
            controller.minDarknessFloor = 0.35f;
            controller.localLitSensitivity = 6f;
            controller.eigengrauColor = new Color(0.030f, 0.030f, 0.038f, 1f);
            controller.eigengrauAmount = 0.6f;
            controller.scotopicGain = 3f;
            controller.monoAmount = 0.95f;
            controller.noiseMode = NeuralNoiseMode.StaticGrain;
            controller.noiseFlickerRate = 12f;
            controller.noiseIntensity = 0.035f;
            controller.noise1Scale = 900f;
            controller.noise2Scale = 240f;
            controller.peripheralPower = 0.85f;
            controller.centerWeight = 0f;
            controller.blurRadius = 1.5f;
            controller.applyAmbientLight = true; // Aplica luz ambiente tenue para que se vea algo en la oscuridad
            controller.ambientLightColor = new Color(0.15f, 0.15f, 0.18f, 1f);

            EditorUtility.SetDirty(cameraObj);
            PrefabUtility.SavePrefabAsset(prefab);

            Debug.Log("[ScotopicVisionSetup] PlayerCamera configurado con DarkAdaptation + ScotopicVisionController (sin linterna del efecto).");
        }

        // ------------------------------------------------------------------ Volume Global
        private static void SetupGlobalVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                Debug.LogWarning($"[ScotopicVisionSetup] No se encontró VolumeProfile en {VolumeProfilePath}. Se usará el que haya en la escena.");
                return;
            }

            // Buscar Volume global en la escena actual
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            Volume globalVolume = null;
            foreach (var v in volumes)
            {
                if (v.isGlobal && v.profile != null)
                {
                    globalVolume = v;
                    break;
                }
            }

            if (globalVolume == null)
            {
                // Crear uno nuevo
                var go = new GameObject("Global Volume");
                globalVolume = go.AddComponent<Volume>();
                globalVolume.isGlobal = true;
                globalVolume.priority = 0;
                Debug.Log("[ScotopicVisionSetup] Global Volume creado en la escena.");
            }

            globalVolume.profile = profile;
            EditorUtility.SetDirty(globalVolume);
            Debug.Log("[ScotopicVisionSetup] Volume global asignado con profile SampleSceneProfile.");
        }
    }
}