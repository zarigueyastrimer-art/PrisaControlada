using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PrisaControlada.Menu
{
    public static class MainMenuSetup
    {
        [MenuItem("PrisaControlada/Crear Menu Principal")]
        public static void CreateMainMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // --- Camera ---
            Camera cam = Camera.main;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.01f, 0.01f);
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // --- Root ---
            var root = new GameObject("MainMenu");

            // --- Fade Overlay ---
            var fadeGO = CreateCanvas("FadeCanvas", root.transform, 9999);
            var fadeOverlay = CreatePanel(fadeGO.transform, "FadeOverlay");
            var fadeCG = fadeOverlay.GetComponent<CanvasGroup>();
            fadeCG.alpha = 1f;
            fadeCG.blocksRaycasts = false;
            fadeCG.interactable = false;
            fadeCG.ignoreParentGroups = true;
            var fadeImg = fadeOverlay.GetComponent<Image>();
            fadeImg.color = Color.black;
            fadeImg.raycastTarget = false;

            // --- Main Canvas ---
            var mainCanvas = CreateCanvas("MainCanvas", root.transform, 100);
            var mainPanel = CreatePanel(mainCanvas.transform, "MainPanel");
            var mainCG = mainPanel.GetComponent<CanvasGroup>();
            mainCG.interactable = true;
            mainCG.blocksRaycasts = true;

            // --- Fonts ---
            Font titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/fuentes/titulo/coldnightforalligators.ttf");
            Font bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/fuentes/para texto/I Still Know.ttf");
            if (titleFont == null) titleFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (bodyFont == null) bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // --- Title ---
            var titleObj = CreateText(mainPanel.transform, "TitleText", "LimInal NightM\u00e5re",
                new Vector2(0f, 200f), 52, Color.white, FontStyle.Normal, titleFont);
            titleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 80f);

            // --- Buttons ---
            var btnContainer = new GameObject("ButtonContainer", typeof(RectTransform));
            btnContainer.transform.SetParent(mainPanel.transform, false);
            var btnRT = btnContainer.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.anchoredPosition = new Vector2(0f, -20f);
            btnRT.sizeDelta = new Vector2(300f, 250f);
            var btnVLG = btnContainer.AddComponent<VerticalLayoutGroup>();
            btnVLG.spacing = 16f;
            btnVLG.childAlignment = TextAnchor.MiddleCenter;
            btnVLG.childControlWidth = true;
            btnVLG.childControlHeight = true;
            btnVLG.childForceExpandWidth = true;
            btnVLG.childForceExpandHeight = false;
            btnVLG.padding = new RectOffset(20, 20, 20, 20);

            var playBtn = CreateButton(btnContainer.transform, "PlayButton", "PLAY", bodyFont);
            var creditsBtn = CreateButton(btnContainer.transform, "CreditsButton", "CREDITS", bodyFont);
            var quitBtn = CreateButton(btnContainer.transform, "QuitButton", "QUIT", bodyFont);

            // --- Credits Canvas ---
            var creditsCanvas = CreateCanvas("CreditsCanvas", root.transform, 200);
            var creditsPanel = CreatePanel(creditsCanvas.transform, "CreditsPanel");
            var creditsCG = creditsPanel.GetComponent<CanvasGroup>();
            creditsCG.alpha = 0f;
            creditsCG.blocksRaycasts = false;
            creditsCG.interactable = false;

            CreateText(creditsPanel.transform, "CreditsTitle", "CREDITS",
                new Vector2(0f, 220f), 36, Color.white, FontStyle.Normal, titleFont);

            string[] names = {
                "Agustin Labarque",
                "Ian Rosenberg",
                "Matias Morilla",
                "Francisco Larrondo"
            };

            for (int i = 0; i < names.Length; i++)
            {
                float yOffset = 120f - i * 45f;
                CreateText(creditsPanel.transform, "Name" + i, names[i],
                    new Vector2(0f, yOffset), 22, new Color(0.85f, 0.85f, 0.85f), FontStyle.Normal, bodyFont);
            }

            var backBtn = CreateButton(creditsPanel.transform, "BackButton", "BACK", bodyFont,
                new Vector2(0f, -170f), new Vector2(200f, 45f));

            // --- Controller ---
            var controller = root.AddComponent<MainMenuController>();

            // Serialize fields via SerializedObject
            var so = new SerializedObject(controller);
            SetRef(so, "mainPanel", mainCG);
            SetRef(so, "creditsPanel", creditsCG);
            SetRef(so, "playButton", playBtn.GetComponent<Button>());
            SetRef(so, "creditsButton", creditsBtn.GetComponent<Button>());
            SetRef(so, "quitButton", quitBtn.GetComponent<Button>());
            SetRef(so, "creditsBackButton", backBtn.GetComponent<Button>());
            SetRef(so, "titleText", titleObj.GetComponent<Text>());
            SetRef(so, "fadeOverlay", fadeCG);
            so.FindProperty("gameSceneName").stringValue = "Prototype";
            so.ApplyModifiedPropertiesWithoutUndo();

            // --- Ambient ---
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.12f, 0.02f, 0.02f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.05f;
            RenderSettings.ambientLight = new Color(0.05f, 0.02f, 0.02f);
            RenderSettings.ambientIntensity = 0.3f;

            // --- Save ---
            string scenePath = "Assets/Scenes/MainMenu.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            // --- Build Settings ---
            var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool hasMenu = false;
            bool hasPrototype = false;
            foreach (var s in buildScenes)
            {
                if (s.path == scenePath) hasMenu = true;
                if (s.path == "Assets/Scenes/Prototype.unity") hasPrototype = true;
            }
            if (!hasMenu)
                buildScenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            if (!hasPrototype)
                buildScenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Prototype.unity", true));
            EditorBuildSettings.scenes = buildScenes.ToArray();

            Debug.Log($"<color=#77ff77>[MainMenu] Scene created at {scenePath}</color>");
            Debug.Log("<color=#77ff77>[MainMenu] Build Settings updated: MainMenu (0) -> Prototype (1)</color>");
        }

        private static GameObject CreateCanvas(string name, Transform parent, int sort_order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sort_order;
            go.AddComponent<CanvasScaler>();
            if (sort_order < 9999)
                go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            go.AddComponent<CanvasGroup>();
            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 1f);
            return go;
        }

        private static GameObject CreateText(Transform parent, string name, string content,
            Vector2 anchoredPos, float fontSize, Color color, FontStyle style, Font font = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(600f, 60f);

            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = (int)fontSize;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return go;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, Font font = null,
            Vector2? anchoredPos = null, Vector2? size = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();

            if (anchoredPos.HasValue)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = anchoredPos.Value;
                rt.sizeDelta = size ?? new Vector2(260f, 50f);

                var le = go.AddComponent<LayoutElement>();
                le.minWidth = size?.x ?? 260f;
                le.minHeight = size?.y ?? 50f;
                le.preferredWidth = size?.x ?? 260f;
                le.preferredHeight = size?.y ?? 50f;
            }
            else
            {
                rt.sizeDelta = new Vector2(260f, 50f);

                var le = go.AddComponent<LayoutElement>();
                le.minWidth = 260f;
                le.minHeight = 50f;
                le.preferredWidth = 260f;
                le.preferredHeight = 50f;
            }

            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.03f, 0.03f, 0.95f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.18f, 0.03f, 0.03f, 0.95f);
            colors.highlightedColor = new Color(0.4f, 0.06f, 0.06f, 1f);
            colors.pressedColor = new Color(0.55f, 0.1f, 0.1f, 1f);
            colors.selectedColor = new Color(0.3f, 0.05f, 0.05f, 1f);
            btn.colors = colors;

            var txtGO = new GameObject("Label", typeof(RectTransform));
            txtGO.transform.SetParent(go.transform, false);
            var txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;

            var txt = txtGO.AddComponent<Text>();
            txt.text = label;
            txt.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 20;
            txt.color = new Color(0.9f, 0.3f, 0.3f);
            txt.fontStyle = FontStyle.Normal;
            txt.alignment = TextAnchor.MiddleCenter;

            var shadow = txtGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(1f, -1f);

            return go;
        }

        private static void SetRef(SerializedObject so, string name, Object value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.objectReferenceValue = value;
        }
    }
}
