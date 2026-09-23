using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeliSim.InFlightMenu
{
    public sealed class DisplaySettingsPage : InFlightMenuPage
    {
        [SerializeField] private CanvasScaler menuCanvasScaler;
        [SerializeField] private TMP_Dropdown windowMode;
        [SerializeField] private TMP_Dropdown resolution;
        [SerializeField] private Toggle vSync;
        [SerializeField] private TMP_InputField frameRateCap;
        [SerializeField] private TMP_Dropdown qualityPreset;
        [SerializeField] private Slider uiScale;
        [SerializeField] private TMP_Text uiScaleValue;
        [SerializeField] private TMP_Text diagnostic;

        private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        private DisplaySettingsModel model;
        private bool wired;
        private string pageError = string.Empty;

        public override bool IsDirty => model != null && model.IsDirty;
        public string Error => !string.IsNullOrEmpty(pageError) ? pageError :
            model != null ? model.Error : "Display settings are not initialized.";

        private void Awake()
        {
            CanvasScaler scaler = menuCanvasScaler != null ? menuCanvasScaler : GetComponentInParent<CanvasScaler>();
            model = new DisplaySettingsModel(new UnityDisplaySettings(scaler), new PlayerPrefsDisplaySettingsStore());
            model.RestoreSaved();
        }

        private void OnEnable()
        {
            Wire();
        }

        private void OnDisable()
        {
            Unwire();
        }

        public override void OnPageSelected()
        {
            if (model == null) Awake();
            // TMP_Dropdown.ClearOptions invokes value callbacks while rebuilding choices.
            Unwire();
            BuildOptions();
            ShowDraft();
            Wire();
            UpdateAvailability();
            ShowDiagnostic();
        }

        public override bool TryApplyChanges()
        {
            if (model == null || !UpdateAvailability())
            {
                ShowDiagnostic();
                return false;
            }
            bool applied = model.TryApply();
            ShowDiagnostic();
            return applied;
        }

        public override void DiscardChanges()
        {
            if (model == null) return;
            model.Revert();
            ShowDraft();
            ShowDiagnostic();
        }

        private void BuildOptions()
        {
            if (windowMode != null)
            {
                windowMode.ClearOptions();
                windowMode.AddOptions(new List<string> { "Windowed", "Borderless fullscreen", "Exclusive fullscreen", "Maximized window" });
            }

            resolutions.Clear();
            if (model.Draft.Width > 0 && model.Draft.Height > 0)
                AddResolution(model.Draft.Width, model.Draft.Height);
            foreach (Resolution available in Screen.resolutions) AddResolution(available.width, available.height);
            if (resolution != null)
            {
                List<string> names = new List<string>();
                foreach (Vector2Int size in resolutions) names.Add(size.x + " × " + size.y);
                resolution.ClearOptions();
                resolution.AddOptions(names);
            }

            if (qualityPreset != null)
            {
                qualityPreset.ClearOptions();
                qualityPreset.AddOptions(new List<string>(QualitySettings.names));
            }

            if (uiScale != null)
            {
                uiScale.minValue = 0.75f;
                uiScale.maxValue = 1.5f;
                uiScale.interactable = model.UiScaleAvailable;
            }
        }

        private void AddResolution(int width, int height)
        {
            Vector2Int size = new Vector2Int(width, height);
            if (!resolutions.Contains(size)) resolutions.Add(size);
        }

        private void ShowDraft()
        {
            DisplaySettingsValue value = model.Draft;
            if (windowMode != null) windowMode.SetValueWithoutNotify(ModeToIndex(value.WindowMode));
            if (resolution != null) resolution.SetValueWithoutNotify(resolutions.IndexOf(new Vector2Int(value.Width, value.Height)));
            if (vSync != null) vSync.SetIsOnWithoutNotify(value.VSync);
            if (frameRateCap != null) frameRateCap.SetTextWithoutNotify(value.FrameRateCap == -1 ? "Unlimited" : value.FrameRateCap.ToString());
            if (qualityPreset != null) qualityPreset.SetValueWithoutNotify(value.QualityLevel);
            if (uiScale != null) uiScale.SetValueWithoutNotify(value.UiScale);
            if (uiScaleValue != null) uiScaleValue.text = "UI scale: " + Mathf.RoundToInt(value.UiScale * 100f) + "%";
        }

        private void Wire()
        {
            if (wired) return;
            if (windowMode != null) windowMode.onValueChanged.AddListener(OnWindowMode);
            if (resolution != null) resolution.onValueChanged.AddListener(OnResolution);
            if (vSync != null) vSync.onValueChanged.AddListener(OnVSync);
            if (frameRateCap != null) frameRateCap.onValueChanged.AddListener(OnFrameRateCap);
            if (qualityPreset != null) qualityPreset.onValueChanged.AddListener(OnQuality);
            if (uiScale != null) uiScale.onValueChanged.AddListener(OnUiScale);
            wired = true;
        }

        private void Unwire()
        {
            if (!wired) return;
            if (windowMode != null) windowMode.onValueChanged.RemoveListener(OnWindowMode);
            if (resolution != null) resolution.onValueChanged.RemoveListener(OnResolution);
            if (vSync != null) vSync.onValueChanged.RemoveListener(OnVSync);
            if (frameRateCap != null) frameRateCap.onValueChanged.RemoveListener(OnFrameRateCap);
            if (qualityPreset != null) qualityPreset.onValueChanged.RemoveListener(OnQuality);
            if (uiScale != null) uiScale.onValueChanged.RemoveListener(OnUiScale);
            wired = false;
        }

        private void OnWindowMode(int index) { DisplaySettingsValue value = model.Draft; value.WindowMode = IndexToMode(index); model.Stage(value); }
        private void OnResolution(int index)
        {
            if (index < 0 || index >= resolutions.Count) return;
            DisplaySettingsValue value = model.Draft;
            value.Width = resolutions[index].x;
            value.Height = resolutions[index].y;
            model.Stage(value);
        }
        private void OnVSync(bool enabled) { DisplaySettingsValue value = model.Draft; value.VSync = enabled; model.Stage(value); }
        private void OnFrameRateCap(string text)
        {
            DisplaySettingsValue value = model.Draft;
            if (string.Equals(text, "Unlimited", System.StringComparison.OrdinalIgnoreCase)) value.FrameRateCap = -1;
            else value.FrameRateCap = int.TryParse(text, out int cap) ? cap : 0;
            model.Stage(value);
        }
        private void OnQuality(int index) { DisplaySettingsValue value = model.Draft; value.QualityLevel = index; model.Stage(value); }
        private void OnUiScale(float scale)
        {
            DisplaySettingsValue value = model.Draft;
            value.UiScale = scale;
            model.Stage(value);
            if (uiScaleValue != null) uiScaleValue.text = "UI scale: " + Mathf.RoundToInt(scale * 100f) + "%";
        }

        private void ShowDiagnostic()
        {
            if (diagnostic != null) diagnostic.text = Error;
        }

        private bool UpdateAvailability()
        {
            if (windowMode == null || resolution == null || vSync == null || frameRateCap == null ||
                qualityPreset == null || uiScale == null || diagnostic == null)
            {
                pageError = "Assign all display controls and diagnostic text before applying.";
                return false;
            }
            if (resolutions.Count == 0 || QualitySettings.names.Length == 0)
            {
                pageError = "Display or quality options are unavailable on this device.";
                return false;
            }
            pageError = string.Empty;
            return true;
        }

        private static int ModeToIndex(FullScreenMode mode)
        {
            return mode == FullScreenMode.FullScreenWindow ? 1 :
                mode == FullScreenMode.ExclusiveFullScreen ? 2 :
                mode == FullScreenMode.MaximizedWindow ? 3 : 0;
        }

        private static FullScreenMode IndexToMode(int index)
        {
            return index == 1 ? FullScreenMode.FullScreenWindow :
                index == 2 ? FullScreenMode.ExclusiveFullScreen :
                index == 3 ? FullScreenMode.MaximizedWindow : FullScreenMode.Windowed;
        }
    }
}
