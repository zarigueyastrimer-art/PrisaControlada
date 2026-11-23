using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PrisaControlada.Editor
{
    //TP2 - Matías Morilla
    /// <summary>
    /// Crea el overlay de ruido VHS en la escena activa: un canvas con un
    /// RawImage a pantalla completa que reproduce vhsNoise.mp4 con chroma key
    /// verde. El canvas queda por debajo del HUD (sortingOrder bajo) y todo
    /// queda editable en el Inspector.
    /// </summary>
    public static class VhsNoiseOverlaySetup
    {
        private const string CanvasName = "VHSNoise_Canvas";
        private const string ImageName = "VhsNoiseImage";
        private const string ClipPath = "Assets/VHSEffectURP/vhsNoise.mp4";
        private const string MaterialPath = "Assets/VHSEffectURP/VhsNoiseChromaKey.mat";
        private const string ShaderPath = "Assets/VHSEffectURP/dead/ChromaKey.shader";
        private const int SortingOrder = 50;

        [MenuItem("PrisaControlada/Configurar VHS Noise (HUD)")]
        public static void SetupVhsNoiseOverlay()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("VHS Noise", "Guarda la escena primero.", "OK");
                return;
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("VHS Noise", "No se encontro el Player en la escena.", "OK");
                return;
            }

            RectTransform canvasRect = GetOrCreateCanvas(player.transform);
            RawImage image = GetOrCreateImage(canvasRect);
            Material material = LoadOrCreateMaterial();
            VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(ClipPath);

            // El material queda en el RawImage para poder editarlo en el
            // Inspector. En runtime el script lo instancia y le pisa la clave.
            if (material != null)
                image.material = material;

            // El VideoPlayer vive en la escena: el clip y el modo se ven desde
            // el Inspector sin entrar al play.
            VideoPlayer video = image.gameObject.GetComponent<VideoPlayer>();
            if (video == null)
                video = image.gameObject.AddComponent<VideoPlayer>();

            video.playOnAwake = false;
            video.waitForFirstFrame = true;
            video.isLooping = true;
            video.skipOnDrop = true;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.audioOutputMode = VideoAudioOutputMode.None;
            video.clip = clip;

            VhsNoiseOverlay controller = canvasRect.gameObject.GetComponent<VhsNoiseOverlay>();
            if (controller == null)
                controller = canvasRect.gameObject.AddComponent<VhsNoiseOverlay>();

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("vhsImage").objectReferenceValue = image;
            so.FindProperty("noiseClip").objectReferenceValue = clip;
            so.FindProperty("chromaKeyMaterial").objectReferenceValue = material;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[VhsNoise] Listo: overlay de ruido VHS creado en " + scene.name + ".");
        }

        private static RectTransform GetOrCreateCanvas(Transform player)
        {
            RectTransform canvasRect = player.Find(CanvasName) as RectTransform;
            if (canvasRect != null)
            {
                Canvas existing = canvasRect.GetComponent<Canvas>();
                if (existing != null)
                    existing.sortingOrder = SortingOrder;

                return canvasRect;
            }

            canvasRect = CreateRect(CanvasName, player);

            Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvasRect;
        }

        private static RawImage GetOrCreateImage(RectTransform parent)
        {
            Transform existing = parent.Find(ImageName);
            if (existing != null)
                return existing.GetComponent<RawImage>();

            RectTransform rect = CreateRect(ImageName, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            return image;
        }

        private static Material LoadOrCreateMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                shader = Shader.Find("PrisaControlada/UI/ChromaKey");

            if (shader == null)
                return null;

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}
