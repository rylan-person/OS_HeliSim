using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeliSim.InFlightMenu
{
    /// <summary>
    /// Presents a standard Toggle as a two-sided OFF / ON segmented control.
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
        [SerializeField] private Color selectedBackgroundColor = new Color32(218, 24, 75, 255);
        [SerializeField] private Color unselectedBackgroundColor = new Color32(21, 31, 38, 255);
        [SerializeField] private Color selectedLabelColor = Color.white;
        [SerializeField] private Color unselectedLabelColor = new Color32(137, 155, 162, 255);
        private bool lastKnownToggleValue;
        private bool hasKnownToggleValue;

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
                (!hasKnownToggleValue || settingToggle.isOn != lastKnownToggleValue))
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
            SetVisual(offBackground, offLabel, !isOn);
            SetVisual(onBackground, onLabel, isOn);
        }

        private void HandleValueChanged(bool _)
        {
            Refresh();
        }

        private void SetVisual(Image background, TMP_Text label, bool isSelected)
        {
            if (background != null)
            {
                background.color = isSelected ? selectedBackgroundColor : unselectedBackgroundColor;
            }

            if (label != null)
            {
                label.color = isSelected ? selectedLabelColor : unselectedLabelColor;
            }
        }
    }
}
