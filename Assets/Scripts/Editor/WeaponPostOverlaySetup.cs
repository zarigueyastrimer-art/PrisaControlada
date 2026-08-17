using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PrisaControlada.Player;

namespace PrisaControlada.EditorTools
{
    public static class WeaponPostOverlaySetup
    {
        private const string WeaponLayerName = "Weapon";
        private const string FeatureName = "RenderWeaponAfterPost";
        private const string WeaponCameraName = "WeaponCamera";
        private static readonly string[] RendererPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset"
        };
        private const string ReportPath = "Assets/MyAssets/guns/revolver/WEAPON_OVERLAY.txt";

        [MenuItem("Tools/PrisaControlada/Weapons/Setup Arma ENCIMA del post (sin motion blur)")]
        public static void Setup()
        {
            var sb = new StringBuilder();
            try
            {
                int layer = EnsureWeaponLayer(sb);
                if (layer < 0) { sb.AppendLine("ERROR: no hay slot de layer libre para " + WeaponLayerName); return; }
                sb.AppendLine("Layer " + WeaponLayerName + " = " + layer);

                var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindObjectOfType<Camera>();
                if (cam == null) { sb.AppendLine("ERROR: no hay camara principal"); return; }

                Transform weapon = FindWeaponRoot(cam);
                if (weapon != null)
                    SetLayerRecursive(weapon, layer, sb);
                else
                    sb.AppendLine("AVISO: no se encontro RevolverWeapon bajo la camara.");

                RevertRendererState(sb);
                SetupCameras(cam, layer, sb);
                WireWeaponController(cam, sb);

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                sb.AppendLine("Escena guardada.");
                sb.AppendLine("\nLISTO. Proba Play y gira la camara: el arma debe quedar nitida (sin motion blur) por encima de la escena desenfocada.");
                sb.AppendLine("Si el arma se ve algo 'lavada' (sin tonemapping), avisame y lo ajustamos.");
            }
            catch (Exception e)
            {
                sb.AppendLine("\n== EXCEPCION ==");
                sb.AppendLine(e.ToString());
            }
            WriteReport(sb);
        }

        [MenuItem("Tools/PrisaControlada/Weapons/Revert Arma encima del post")]
        public static void Revert()
        {
            var sb = new StringBuilder();
            try
            {
                var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindObjectOfType<Camera>();
                if (cam != null)
                {
                    Transform weapon = FindWeaponRoot(cam);
                    if (weapon != null)
                        SetLayerRecursive(weapon, 0, sb);

                    var baseData = cam.GetComponent<UniversalAdditionalCameraData>();
                    if (baseData != null)
                    {
                        for (int i = baseData.cameraStack.Count - 1; i >= 0; i--)
                        {
                            if (baseData.cameraStack[i] != null && baseData.cameraStack[i].name == WeaponCameraName)
                                baseData.cameraStack.RemoveAt(i);
                        }
                        cam.cullingMask |= 1 << 8;
                        sb.AppendLine("Base camera: cullingMask restaurado, stack limpio.");
                    }

                    var overlayCam = cam.transform.Find(WeaponCameraName);
                    if (overlayCam != null)
                    {
                        UnityEngine.Object.DestroyImmediate(overlayCam.gameObject);
                        sb.AppendLine("WeaponCamera eliminada.");
                    }

                    var wc = cam.GetComponent<WeaponController>();
                    if (wc != null)
                    {
                        var so = new SerializedObject(wc);
                        so.FindProperty("weaponCamera").objectReferenceValue = null;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                else
                {
                    sb.AppendLine("No se encontro camara principal.");
                }

                RevertRendererState(sb);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                sb.AppendLine("Escena guardada.");
                sb.AppendLine("Revert OK. El arma vuelve al render normal (con motion blur).");
            }
            catch (Exception e)
            {
                sb.AppendLine("\n== EXCEPCION ==");
                sb.AppendLine(e.ToString());
            }
            WriteReport(sb);
        }

        private static int EnsureWeaponLayer(StringBuilder sb)
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManager == null || tagManager.Length == 0)
            {
                sb.AppendLine("ERROR: no se pudo cargar TagManager.asset");
                return -1;
            }
            var so = new SerializedObject(tagManager[0]);
            var layers = so.FindProperty("layers");
            int found = -1;
            int free = -1;
            for (int i = 8; i < 32; i++)
            {
                string name = layers.GetArrayElementAtIndex(i).stringValue;
                if (name == WeaponLayerName) { found = i; break; }
                if (string.IsNullOrEmpty(name) && free < 0) free = i;
            }
            if (found < 0)
            {
                if (free < 0) return -1;
                layers.GetArrayElementAtIndex(free).stringValue = WeaponLayerName;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                found = free;
            }
            return found;
        }

        private static Transform FindWeaponRoot(Camera cam)
        {
            var child = cam.transform.Find("RevolverWeapon");
            if (child != null) return child;
            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
                if (go.name == "RevolverWeapon") return go.transform;
            return null;
        }

        private static void SetLayerRecursive(Transform root, int layer, StringBuilder sb)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
                t.gameObject.layer = layer;
            sb.AppendLine("RevolverWeapon (y descendencia) -> layer " + WeaponLayerName + " (" + layer + "). Objetos: " + all.Length);
        }

        private static void SetupCameras(Camera cam, int weaponLayer, StringBuilder sb)
        {
            var baseData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (baseData == null)
            {
                baseData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                sb.AppendLine("AVISO: la camara no tenia UniversalAdditionalCameraData; se creo uno.");
            }

            for (int i = baseData.cameraStack.Count - 1; i >= 0; i--)
            {
                if (baseData.cameraStack[i] != null && baseData.cameraStack[i].name == WeaponCameraName)
                    baseData.cameraStack.RemoveAt(i);
            }

            var existing = cam.transform.Find(WeaponCameraName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var weaponCamGo = new GameObject(WeaponCameraName);
            weaponCamGo.transform.SetParent(cam.transform, false);
            weaponCamGo.transform.localPosition = Vector3.zero;
            weaponCamGo.transform.localRotation = Quaternion.identity;
            weaponCamGo.transform.localScale = Vector3.one;

            var weaponCam = weaponCamGo.AddComponent<Camera>();
            weaponCam.clearFlags = CameraClearFlags.Depth;
            weaponCam.cullingMask = 1 << weaponLayer;
            weaponCam.fieldOfView = cam.fieldOfView;
            weaponCam.nearClipPlane = cam.nearClipPlane;
            weaponCam.farClipPlane = cam.farClipPlane;
            weaponCam.allowHDR = cam.allowHDR;
            weaponCam.allowMSAA = cam.allowMSAA;
            weaponCam.allowDynamicResolution = cam.allowDynamicResolution;
            weaponCam.useOcclusionCulling = false;
            weaponCam.depth = cam.depth;

            var overlayData = weaponCamGo.AddComponent<UniversalAdditionalCameraData>();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderPostProcessing = false;
            overlayData.renderShadows = false;
            overlayData.volumeLayerMask = baseData.volumeLayerMask;

            var stackSo = new SerializedObject(baseData);
            var camerasProp = stackSo.FindProperty("m_Cameras");
            bool already = false;
            for (int i = 0; i < camerasProp.arraySize; i++)
            {
                if (camerasProp.GetArrayElementAtIndex(i).objectReferenceValue == weaponCam)
                {
                    already = true;
                    break;
                }
            }
            if (!already)
            {
                camerasProp.arraySize += 1;
                camerasProp.GetArrayElementAtIndex(camerasProp.arraySize - 1).objectReferenceValue = weaponCam;
            }
            stackSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(baseData);

            cam.cullingMask = cam.cullingMask & ~(1 << weaponLayer);

            sb.AppendLine("WeaponCamera (Overlay) creada bajo la camara base. Culling de base sin layer " + weaponLayer +
                ". Stack de base: " + (already ? "ya estaba" : "anadida"));
        }

        private static void WireWeaponController(Camera cam, StringBuilder sb)
        {
            var wc = cam.GetComponent<WeaponController>();
            if (wc == null) { sb.AppendLine("AVISO: no hay WeaponController en la camara (no se linkeo weaponCamera)."); return; }
            var overlay = cam.transform.Find(WeaponCameraName);
            var weaponCam = overlay != null ? overlay.GetComponent<Camera>() : null;
            var so = new SerializedObject(wc);
            so.FindProperty("weaponCamera").objectReferenceValue = weaponCam;
            so.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine("WeaponController.weaponCamera -> " + (weaponCam != null ? WeaponCameraName : "null"));
        }

        private static void RevertRendererState(StringBuilder sb)
        {
            foreach (var path in RendererPaths)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (rendererData == null) { sb.AppendLine("  sin renderer: " + path); continue; }

                if (rendererData.opaqueLayerMask != -1)
                {
                    rendererData.opaqueLayerMask = -1;
                    sb.AppendLine("  " + path + ": opaqueLayerMask restaurado a todo.");
                }

                bool changed = false;
                for (int i = rendererData.rendererFeatures.Count - 1; i >= 0; i--)
                {
                    var f = rendererData.rendererFeatures[i];
                    if (f != null && f.name == FeatureName)
                    {
                        rendererData.rendererFeatures.RemoveAt(i);
                        UnityEngine.Object.DestroyImmediate(f, true);
                        changed = true;
                    }
                }
                if (changed) sb.AppendLine("  " + path + ": feature " + FeatureName + " eliminado.");

                if (changed || rendererData.opaqueLayerMask != -1)
                    EditorUtility.SetDirty(rendererData);
            }
            AssetDatabase.SaveAssets();
        }

        private static void WriteReport(StringBuilder sb)
        {
            string folder = Path.GetDirectoryName(ReportPath);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                int idx = folder.LastIndexOf('/');
                if (idx > 0) AssetDatabase.CreateFolder(folder.Substring(0, idx), folder.Substring(idx + 1));
            }
            File.WriteAllText(ReportPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log("[WeaponPostOverlaySetup] Reporte: " + ReportPath + "\n" + sb.ToString());
        }
    }
}