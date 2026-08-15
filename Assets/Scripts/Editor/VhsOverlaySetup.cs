using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PrisaControlada.Editor
{
    public static class VhsOverlaySetup
    {
        private const string VhsClipPath = "Assets/VHSEffectURP/Vhs insert effect (Update Read Desc Before Commenting).mp4";
        private const string ChromaShaderPath = "Assets/VHSEffectURP/dead/ChromaKey.shader";
        private const string VhsMaterialPath = "Assets/VHSEffectURP/VhsChromaKey.mat";

        [MenuItem("Tools/PrisaControlada/Setup VHS Overlay")]
        public static void SetupVhsOverlay()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Setup VHS Overlay", "Save the scene first.", "OK");
                return;
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Setup VHS Overlay", "No Player found in the scene.", "OK");
                return;
            }

            RectTransform canvasRect = player.transform.Find("VHS_Canvas") as RectTransform;
            if (canvasRect == null)
            {
                canvasRect = CreateRect("VHS_Canvas", player.transform);

                Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 350;

                CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            }

            RawImage image = GetOrCreateImage(canvasRect, "VhsImage");

            Shader chromaShader = AssetDatabase.LoadAssetAtPath<Shader>(ChromaShaderPath);
            if (chromaShader == null)
                chromaShader = Shader.Find("PrisaControlada/UI/ChromaKey");

            Material chromaMat = AssetDatabase.LoadAssetAtPath<Material>(VhsMaterialPath);
            if (chromaMat == null && chromaShader != null)
            {
                chromaMat = new Material(chromaShader);
                AssetDatabase.CreateAsset(chromaMat, VhsMaterialPath);
            }
            if (chromaMat != null)
            {
                // The insert video uses a BLUE screen (mode 1 = dominant channel,
                // robust to the decoder color shift seen in the logs).
                chromaMat.SetColor("_KeyColor", new Color(0f, 0f, 1f, 1f));
                chromaMat.SetFloat("_Threshold", 0.25f);
                chromaMat.SetFloat("_Smoothness", 0.15f);
                chromaMat.SetFloat("_Spill", 0.45f);
                chromaMat.SetFloat("_Mode", 1f);
                EditorUtility.SetDirty(chromaMat);
                image.material = chromaMat;
            }

            VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VhsClipPath);

            VhsOverlayController controller = canvasRect.gameObject.GetComponent<VhsOverlayController>();
            if (controller == null)
                controller = canvasRect.gameObject.AddComponent<VhsOverlayController>();

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("vhsImage").objectReferenceValue = image;
            so.FindProperty("vhsClip").objectReferenceValue = clip;
            so.FindProperty("chromaKeyMaterial").objectReferenceValue = chromaMat;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[VhsOverlay] Listo: filtro VHS siempre activo (video en loop + chroma key).");
        }

        private static RawImage GetOrCreateImage(RectTransform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null)
                return t.GetComponent<RawImage>();

            RectTransform r = CreateRect(name, parent);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            RawImage img = r.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f);
            return img;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}