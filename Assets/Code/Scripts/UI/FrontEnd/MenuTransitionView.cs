using System;
using HeliSim.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HeliSim.FrontEnd
{
    public sealed class MenuTransitionView : MonoBehaviour
    {
        private TMP_Text message;
        private UnityEngine.UI.Button cancelButton;

        public void Initialize(Action cancel)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(transform, false);
            var rect = background.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            MenuPalette palette = MenuPalette.Default;
            background.GetComponent<Image>().color = palette.bg0;
            message = MakeText("Status", new Vector2(0, 35), new Vector2(1200, 160), 32);
            var buttonObject = new GameObject("ReturnToMenu", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0, -120); buttonRect.sizeDelta = new Vector2(330, 66);
            buttonObject.GetComponent<Image>().color = palette.bg3;
            var label = MakeText("ReturnLabel", Vector2.zero, new Vector2(330, 66), 24);
            label.transform.SetParent(buttonObject.transform, false);
            label.text = "Return to menu";
            cancelButton = buttonObject.GetComponent<Button>();
            // Same state colours as the menus: hover/focus brighten instead of ColorTint darkening.
            cancelButton.transition = Selectable.Transition.None;
            buttonObject.AddComponent<MenuSelectableStyle>().Configure(null, MenuSelectableVariant.Default,
                cancelButton, buttonObject.GetComponent<Image>(), new[] { label }, null);
            cancelButton.onClick.AddListener(() => cancel());
            // Each loaded scene owns its EventSystem. Reuse it, including across transitions.
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(buttonObject);
            foreach (var child in GetComponentsInChildren<Transform>(true)) child.gameObject.layer = LayerMask.NameToLayer("UI");
            var cameraRoot = new GameObject("MenuTransitionCamera", typeof(Camera));
            cameraRoot.transform.SetParent(transform.parent, false);
            var camera = cameraRoot.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Depth;
            camera.depth = 1000;
            gameObject.AddComponent<MenuVrPresentation>().ConfigureCamera(camera, true);
        }

        private TMP_Text MakeText(string name, Vector2 position, Vector2 size, int fontSize)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            item.transform.SetParent(transform, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = item.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize; text.color = MenuPalette.Default.text1; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return text;
        }

        public void SetMessage(string text)
        {
            message.text = text;
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events != null && (events.currentSelectedGameObject == null || !events.currentSelectedGameObject.activeInHierarchy))
                events.SetSelectedGameObject(cancelButton.gameObject);
        }
    }
}
