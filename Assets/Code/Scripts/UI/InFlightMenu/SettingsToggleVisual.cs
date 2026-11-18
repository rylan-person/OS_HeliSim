using HeliSim.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace HeliSim.InFlightMenu
{
    /// <summary>
    /// Presents a standard Toggle as a two-sided OFF / ON segmented control. Selected On is
    /// teal and selected Off is neutral, so the two states are distinguishable at a glance.
    /// The Toggle remains the only interactive control so existing setting bindings continue to work.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsToggleVisual : MonoBehaviour
    {
        [SerializeField] private Toggle settingToggle;
        [SerializeField] private TMP_Text offLabel;
        [SerializeField] private TMP_Text onLabel;
        [SerializeField] private Image offBackground;
        [SerializeField] private Image onBackground;
        [Tooltip("When assigned, colours come from the theme palette instead of the fields below.")]
        [SerializeField] private MenuTheme theme;
        [Tooltip("Shows the selected On segment like Off (neutral). Use for two-choice selectors such as Pilot / Spectator.")]
        [SerializeField] private bool neutralSelection;
        [FormerlySerializedAs("selectedBackgroundColor")]
        [SerializeField] private Color selectedOnBackgroundColor = new Color32(0x3C, 0xC8, 0xBA, 255);
        [SerializeField] private Color selectedOffBackgroundColor = new Color32(0x1D, 0x2D, 0x3A, 255);
        [SerializeField] private Color unselectedBackgroundColor = new Color32(0x0B, 0x13, 0x1A, 0);
        [FormerlySerializedAs("selectedLabelColor")]
        [SerializeField] private Color selectedOnLabelColor = new Color32(0x03, 0x21, 0x1E, 255);
        [SerializeField] private Color selectedOffLabelColor = new Color32(0xE6, 0xED, 0xF2, 255);
        [SerializeField] private Color unselectedLabelColor = new Color32(0x63, 0x76, 0x84, 255);
        private bool lastKnownToggleValue;
        private bool hasKnownToggleValue;
        private bool lastKnownInteractable = true;

        private void Awake()
        {
            Refresh();
        }

        private void OnEnable()
        {
            if (settingToggle != null)
            {
                settingToggle.onValueChanged.AddListener(HandleValueChanged);
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (settingToggle != null)
            {
                settingToggle.onValueChanged.RemoveListener(HandleValueChanged);
            }
        }

        private void LateUpdate()
        {
            if (settingToggle != null &&
                (!hasKnownToggleValue || settingToggle.isOn != lastKnownToggleValue ||
                 settingToggle.IsInteractable() != lastKnownInteractable))
            {
                Refresh();
            }
        }

        /// <summary>Refreshes both segment visuals from the Toggle's current value.</summary>
        public void Refresh()
        {
            bool isOn = settingToggle != null && settingToggle.isOn;
            lastKnownToggleValue = isOn;
            hasKnownToggleValue = settingToggle != null;
            bool interactable = settingToggle == null || settingToggle.IsInteractable();
            lastKnownInteractable = interactable;
            ResolveColors(out Color onBg, out Color offBg, out Color unselectedBg,
                out Color onText, out Color offText, out Color unselectedText);
            SetVisual(offBackground, offLabel, !isOn ? offBg : unselectedBg, !isOn ? offText : unselectedText, interactable);
            SetVisual(onBackground, onLabel, isOn ? onBg : unselectedBg, isOn ? onText : unselectedText, interactable);
        }

        /// <summary>Assigns the theme and refreshes; used by the Editor theme tool.</summary>
        public void SetTheme(MenuTheme menuTheme)
        {
            theme = menuTheme;
            Refresh();
        }

        private void ResolveColors(out Color onBg, out Color offBg, out Color unselectedBg,
            out Color onText, out Color offText, out Color unselectedText)
        {
            if (theme == null)
            {
                onBg = selectedOnBackgroundColor;
                offBg = selectedOffBackgroundColor;
                unselectedBg = unselectedBackgroundColor;
                onText = selectedOnLabelColor;
                offText = selectedOffLabelColor;
                unselectedText = unselectedLabelColor;
                return;
            }

            MenuPalette p = theme.Palette;
            onBg = neutralSelection ? p.bg4 : p.accent;
            offBg = p.bg4;
            unselectedBg = MenuPalette.WithAlpha(p.bg1, 0f);
            onText = neutralSelection ? p.text1 : p.accentInk;
            offText = p.text1;
            unselectedText = p.text3;
        }

        private void HandleValueChanged(bool _)
        {
            Refresh();
        }

        private static void SetVisual(Image background, TMP_Text label, Color backgroundColor, Color labelColor, bool interactable)
        {
            if (background != null)
            {
                background.color = interactable ? backgroundColor : MenuPalette.Disabled(backgroundColor);
            }

            if (label != null)
            {
                label.color = interactable ? labelColor : MenuPalette.Disabled(labelColor);
            }
        }
    }
}
