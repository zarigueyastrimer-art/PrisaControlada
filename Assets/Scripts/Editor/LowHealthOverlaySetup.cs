using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Editor
{
    public static class LowHealthOverlaySetup
    {
        private const string TexturePath = "Assets/VHSEffectURP/health/lowHealth.png";
        private const string BlurShaderPath = "Assets/VHSEffectURP/health/UIBlur.shader";
        private const string BlurMaterialPath = "Assets/VHSEffectURP/health/UIBlur.mat";

        public static void AddLowHealthOverlay()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Add Low Health Overlay", "Save the scene first.", "OK");
                return;
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Add Low Health Overlay", "No Player found in the scene.", "OK");
                return;
            }

            Transform canvasT = player.transform.Find("LowHealth_Canvas");
            RawImage overlayImage;
            if (canvasT == null)
            {
                RectTransform canvasRect = CreateRect("LowHealth_Canvas", player.transform);
                canvasT = canvasRect;
                Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 300;

                CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                canvasRect.gameObject.AddComponent<GraphicRaycaster>();

                RectTransform imageRect = CreateRect("Overlay", canvasRect);
                imageRect.anchorMin = Vector2.zero;
                imageRect.anchorMax = Vector2.one;
                imageRect.offsetMin = Vector2.zero;
                imageRect.offsetMax = Vector2.zero;

                overlayImage = imageRect.gameObject.AddComponent<RawImage>();
                overlayImage.raycastTarget = false;
                overlayImage.color = new Color(1f, 1f, 1f, 0f);
            }
            else
            {
                overlayImage = canvasT.Find("Overlay")?.GetComponent<RawImage>();
            }

            if (overlayImage == null)
            {
                EditorUtility.DisplayDialog("Add Low Health Overlay", "Could not find the Overlay RawImage.", "OK");
                return;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture != null)
                overlayImage.texture = texture;

            TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer != null && importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            Shader blurShader = AssetDatabase.LoadAssetAtPath<Shader>(BlurShaderPath);
            if (blurShader == null)
                blurShader = Shader.Find("PrisaControlada/UI/Blur");

            Material blurMat = AssetDatabase.LoadAssetAtPath<Material>(BlurMaterialPath);
            if (blurMat == null && blurShader != null)
            {
                blurMat = new Material(blurShader);
                AssetDatabase.CreateAsset(blurMat, BlurMaterialPath);
            }

            if (blurMat != null)
                overlayImage.material = blurMat;

            LowHealthOverlay overlayComp = player.GetComponent<LowHealthOverlay>();
            if (overlayComp == null) overlayComp = player.AddComponent<LowHealthOverlay>();

            SerializedObject so = new SerializedObject(overlayComp);
            so.FindProperty("overlay").objectReferenceValue = overlayImage;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LowHealthOverlaySetup] Low health overlay added (fully visible at 25 HP or less).");
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}