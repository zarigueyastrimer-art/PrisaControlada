using UnityEngine;
using UnityEngine.UI;

namespace PrisaControlada.Player
{
    // Mensaje corto en pantalla (llave incorrecta, codigo invalido, etc).
    [DisallowMultipleComponent]
    public class InteractionPrompt : MonoBehaviour
    {
        private const float DefaultDuration = 2.2f;

        private static InteractionPrompt _instance;

        private CanvasGroup _group;
        private Text _label;
        private float _hideAt;
        private float _alpha;

        public static InteractionPrompt Instance
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = FindObjectOfType<InteractionPrompt>();
                if (_instance == null)
                {
                    GameObject player = GameObject.Find("Player");
                    GameObject go = new GameObject("InteractionPrompt");
                    if (player != null)
                        go.transform.SetParent(player.transform, false);
                    _instance = go.AddComponent<InteractionPrompt>();
                }

                _instance.EnsureUi();
                return _instance;
            }
        }

        public static void Show(string message, float duration = DefaultDuration)
        {
            InteractionPrompt prompt = Instance;
            if (prompt == null) return;
            prompt.EnsureUi();
            prompt._label.text = message;
            prompt._hideAt = Time.unscaledTime + duration;
            prompt._alpha = 1f;
            prompt.ApplyAlpha(1f);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureUi();
            ApplyAlpha(0f);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (_group == null) return;

            if (Time.unscaledTime >= _hideAt && _alpha > 0f)
            {
                _alpha = Mathf.MoveTowards(_alpha, 0f, 4f * Time.unscaledDeltaTime);
                ApplyAlpha(_alpha);
            }
        }

        private void EnsureUi()
        {
            if (_label != null && _group != null) return;

            Transform existing = transform.Find("InteractionPrompt_Canvas/Message");
            if (existing != null)
            {
                _label = existing.GetComponent<Text>();
                _group = transform.Find("InteractionPrompt_Canvas")?.GetComponent<CanvasGroup>();
                if (_label != null && _group != null) return;
            }

            GameObject canvasGo = new GameObject("InteractionPrompt_Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.anchorMin = new Vector2(0.5f, 0.18f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.18f);
            canvasRect.sizeDelta = new Vector2(900f, 60f);
            canvasRect.anchoredPosition = Vector2.zero;

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            GameObject labelGo = new GameObject("Message", typeof(RectTransform));
            labelGo.transform.SetParent(canvasGo.transform, false);

            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            _label = labelGo.AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 28;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = new Color(1f, 1f, 1f, 0.95f);
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
            _label.text = string.Empty;
        }

        private void ApplyAlpha(float alpha)
        {
            if (_group != null)
                _group.alpha = alpha;
        }
    }
}
