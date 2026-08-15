using PrisaControlada.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace PrisaControlada.Editor
{
    public static class DeathSequenceSetup
    {
        private const string BrokenShaderPath = "Assets/VHSEffectURP/dead/CameraBroken.shader";
        private const string ChromaShaderPath = "Assets/VHSEffectURP/dead/ChromaKey.shader";
        private const string BrokenMaterialPath = "Assets/VHSEffectURP/dead/CameraBroken.mat";
        private const string ChromaMaterialPath = "Assets/VHSEffectURP/dead/ChromaKey.mat";
        private const string GlitchClipPath = "Assets/VHSEffectURP/dead/glitchMuerte.mp4";
        private const string GameOverBgPath = "Assets/VHSEffectURP/dead/FondoPantallaDeDerrota.png";
        private const string ButtonSpritePath = "Assets/VHSEffectURP/dead/boton.png";

        [MenuItem("Tools/PrisaControlada/Setup Death Sequence")]
        public static void SetupDeathSequence()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Setup Death Sequence", "Save the scene first.", "OK");
                return;
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Setup Death Sequence", "No Player found in the scene.", "OK");
                return;
            }

            PlayerDeathSequence seq = player.GetComponent<PlayerDeathSequence>();
            if (seq == null)
                seq = player.AddComponent<PlayerDeathSequence>();

            DeathFXController fx = EnsureDeathFX(player, seq);
            EnsureGameOver(player, fx);

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[DeathSequence] Listo: animacion de muerte + camara rota + glitch + menu de derrota conectados.");
        }

        private static DeathFXController EnsureDeathFX(GameObject player, PlayerDeathSequence seq)
        {
            RectTransform canvasRect = player.transform.Find("DeathFX_Canvas") as RectTransform;
            if (canvasRect == null)
            {
                canvasRect = CreateRect("DeathFX_Canvas", player.transform);

                Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;

                CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            }

            RawImage broken = GetOrCreateBrokenImage(player, canvasRect);
            RawImage flash = GetOrCreateImage(canvasRect, "FlashImage");
            RawImage glitch = GetOrCreateImage(canvasRect, "GlitchImage");

            Shader brokenShader = AssetDatabase.LoadAssetAtPath<Shader>(BrokenShaderPath);
            if (brokenShader == null)
                brokenShader = Shader.Find("PrisaControlada/UI/CameraBroken");

            Material brokenMat = AssetDatabase.LoadAssetAtPath<Material>(BrokenMaterialPath);
            if (brokenMat == null && brokenShader != null)
            {
                brokenMat = new Material(brokenShader);
                AssetDatabase.CreateAsset(brokenMat, BrokenMaterialPath);
            }
            if (brokenMat != null)
                broken.material = brokenMat;

            Shader chromaShader = AssetDatabase.LoadAssetAtPath<Shader>(ChromaShaderPath);
            if (chromaShader == null)
                chromaShader = Shader.Find("PrisaControlada/UI/ChromaKey");

            Material chromaMat = AssetDatabase.LoadAssetAtPath<Material>(ChromaMaterialPath);
            if (chromaMat == null && chromaShader != null)
            {
                chromaMat = new Material(chromaShader);
                AssetDatabase.CreateAsset(chromaMat, ChromaMaterialPath);
            }
            if (chromaMat != null)
                glitch.material = chromaMat;

            VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(GlitchClipPath);

            DeathFXController fx = canvasRect.gameObject.GetComponent<DeathFXController>();
            if (fx == null)
                fx = canvasRect.gameObject.AddComponent<DeathFXController>();

            SerializedObject so = new SerializedObject(fx);
            so.FindProperty("brokenImage").objectReferenceValue = broken;
            so.FindProperty("flashImage").objectReferenceValue = flash;
            so.FindProperty("glitchImage").objectReferenceValue = glitch;
            so.FindProperty("glitchClip").objectReferenceValue = clip;
            so.FindProperty("chromaKeyMaterial").objectReferenceValue = chromaMat;
            so.ApplyModifiedProperties();

            SerializedObject soSeq = new SerializedObject(seq);
            soSeq.FindProperty("deathFX").objectReferenceValue = fx;
            soSeq.ApplyModifiedProperties();

            return fx;
        }

        private static void EnsureGameOver(GameObject player, DeathFXController fx)
        {
            RectTransform canvasRect = player.transform.Find("GameOver_Canvas") as RectTransform;
            if (canvasRect == null)
            {
                canvasRect = CreateRect("GameOver_Canvas", player.transform);

                Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 600;

                CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            }

            RawImage bg = GetOrCreateImage(canvasRect, "Background");
            Texture2D bgTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GameOverBgPath);
            if (bgTexture != null)
                bg.texture = bgTexture;

            Sprite buttonSprite = EnsureSprite(ButtonSpritePath);

            Button retry = CreateButton(canvasRect, "Reanudar_Button", "REANUDAR", new Vector2(-420f, -40f), new Vector2(240f, 72f), buttonSprite);
            Button menu = CreateButton(canvasRect, "Menu_Button", "MENU", new Vector2(420f, -40f), new Vector2(240f, 72f), buttonSprite);
            Button quit = CreateButton(canvasRect, "Salir_Button", "SALIR", new Vector2(0f, -300f), new Vector2(170f, 52f), buttonSprite);

            GameOverController controller = canvasRect.gameObject.GetComponent<GameOverController>();
            if (controller == null)
                controller = canvasRect.gameObject.AddComponent<GameOverController>();

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("retryButton").objectReferenceValue = retry;
            so.FindProperty("menuButton").objectReferenceValue = menu;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.ApplyModifiedProperties();

            SerializedObject soFx = new SerializedObject(fx);
            soFx.FindProperty("gameOver").objectReferenceValue = controller;
            soFx.ApplyModifiedProperties();
        }

        private static RawImage GetOrCreateBrokenImage(GameObject player, RectTransform fxCanvas)
        {
            // The broken camera lives on its own canvas with the highest order so it
            // persists over the glitch and the game over menu.
            RectTransform brokenCanvas = player.transform.Find("BrokenCamera_Canvas") as RectTransform;
            if (brokenCanvas == null)
            {
                brokenCanvas = CreateRect("BrokenCamera_Canvas", player.transform);

                Canvas canvas = brokenCanvas.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 700;

                CanvasScaler scaler = brokenCanvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                brokenCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            Transform existing = brokenCanvas.Find("BrokenImage");
            if (existing != null)
                return existing.GetComponent<RawImage>();

            // Migrate the old location (child of the DeathFX canvas) if present.
            Transform oldBroken = fxCanvas.Find("BrokenImage");
            if (oldBroken != null)
            {
                oldBroken.SetParent(brokenCanvas, false);
                return oldBroken.GetComponent<RawImage>();
            }

            return GetOrCreateImage(brokenCanvas, "BrokenImage");
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
            img.color = new Color(1f, 1f, 1f, 1f);
            return img;
        }

        private static Button CreateButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, Sprite sprite)
        {
            RectTransform r = CreateRect(name, parent);
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = position;
            r.sizeDelta = size;

            Image img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = true;

            Button btn = r.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            RectTransform textRect = CreateRect("Text", r);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textRect.gameObject.AddComponent<Text>();
            text.text = label;
            text.font = GetDefaultFont();
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            return btn;
        }

        private static Sprite EnsureSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}