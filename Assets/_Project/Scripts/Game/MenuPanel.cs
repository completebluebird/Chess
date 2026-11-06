using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ChessBoss.Game
{
    /// <summary>
    /// A full-screen menu built from code, so no scene or prefab has to hold it.
    /// shortcut: legacy Text with the built-in font and fixed sizes, because TextMeshPro needs a Korean font asset; move to TMP and a designed layout with the M9 art.
    /// </summary>
    public sealed class MenuPanel
    {
        const int TitleSize = 72;
        const int DetailSize = 40;
        const int ButtonFontSize = 36;
        static readonly Vector2 ButtonSize = new Vector2(360, 80);
        static readonly Color Backdrop = new Color(0, 0, 0, 0.75f);
        static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.3f);

        readonly Transform _content;

        public GameObject Root { get; }

        public MenuPanel(string name, string title)
        {
            EnsureEventSystem();
            Root = new GameObject(name, typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            Root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = Root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var backdrop = new GameObject("Backdrop", typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup));
            backdrop.transform.SetParent(Root.transform, false);
            var rect = backdrop.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            backdrop.GetComponent<UnityEngine.UI.Image>().color = Backdrop;

            var layout = backdrop.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 24;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            _content = backdrop.transform;

            AddText("Title", title, TitleSize);
        }

        public void AddDetail(string name, string text) => AddText(name, text, DetailSize);

        public void AddButton(string name, string label, UnityAction onClick)
        {
            var button = new GameObject(name, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            button.transform.SetParent(_content, false);
            button.GetComponent<RectTransform>().sizeDelta = ButtonSize;
            button.GetComponent<UnityEngine.UI.Image>().color = ButtonColor;
            button.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(onClick);

            var text = NewText("Label", label, ButtonFontSize, button.transform);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        void AddText(string name, string content, int size)
        {
            var text = NewText(name, content, size, _content);
            text.rectTransform.sizeDelta = new Vector2(ButtonSize.x * 3, size * 1.5f);
        }

        static UnityEngine.UI.Text NewText(string name, string content, int size, Transform parent)
        {
            var go = new GameObject(name, typeof(UnityEngine.UI.Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
