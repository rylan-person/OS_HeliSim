using System;
using UnityEngine;
using UnityEngine.UI;

namespace HeliSim.InFlightMenu
{
    /// <summary>Unity-owned display state. Assign the in-flight menu CanvasScaler explicitly.</summary>
    public sealed class UnityDisplaySettings : IDisplaySettingsRuntime
    {
        private readonly CanvasScaler menuScaler;
        private readonly Vector2 baselineReferenceResolution;

        public UnityDisplaySettings(CanvasScaler menuScaler)
        {
            this.menuScaler = menuScaler;
            baselineReferenceResolution = menuScaler != null ? menuScaler.referenceResolution : Vector2.zero;
        }

        public bool SupportsUiScale => menuScaler != null &&
            menuScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
            baselineReferenceResolution.x > 0f && baselineReferenceResolution.y > 0f;

        public int QualityLevelCount => QualitySettings.names.Length;

        public DisplaySettingsValue Capture()
        {
            return new DisplaySettingsValue
            {
                WindowMode = Screen.fullScreenMode,
                Width = Screen.width,
                Height = Screen.height,
                VSync = QualitySettings.vSyncCount > 0,
                FrameRateCap = Application.targetFrameRate,
                QualityLevel = QualitySettings.GetQualityLevel(),
                UiScale = SupportsUiScale ? baselineReferenceResolution.x / menuScaler.referenceResolution.x : 1f,
            };
        }

        public bool SupportsResolution(int width, int height)
        {
            if (width < 640 || height < 480) return false;
            if (width == Screen.width && height == Screen.height) return true;
            foreach (Resolution resolution in Screen.resolutions)
            {
                if (resolution.width == width && resolution.height == height) return true;
            }
            return false;
        }

        public bool TryApply(DisplaySettingsValue value, out string error)
        {
            DisplaySettingsValue before = Capture();
            try
            {
                QualitySettings.SetQualityLevel(value.QualityLevel, true);
                QualitySettings.vSyncCount = value.VSync ? 1 : 0;
                Application.targetFrameRate = value.FrameRateCap;
                if (SupportsUiScale)
                {
                    menuScaler.referenceResolution = baselineReferenceResolution / value.UiScale;
                }
                if (Screen.width != value.Width || Screen.height != value.Height || Screen.fullScreenMode != value.WindowMode)
                {
                    Screen.SetResolution(value.Width, value.Height, value.WindowMode);
                }
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                QualitySettings.SetQualityLevel(before.QualityLevel, true);
                QualitySettings.vSyncCount = before.VSync ? 1 : 0;
                Application.targetFrameRate = before.FrameRateCap;
                if (SupportsUiScale) menuScaler.referenceResolution = baselineReferenceResolution / before.UiScale;
                Screen.SetResolution(before.Width, before.Height, before.WindowMode);
                error = "Could not apply display settings: " + exception.Message;
                return false;
            }
        }
    }

    public sealed class PlayerPrefsDisplaySettingsStore : IDisplaySettingsStore
    {
        private const string StateKey = "HeliSim.Display.v1.State";

        public bool TryLoad(out DisplaySettingsValue value)
        {
            value = default;
            if (!PlayerPrefs.HasKey(StateKey)) return false;
            try
            {
                value = JsonUtility.FromJson<DisplaySettingsValue>(PlayerPrefs.GetString(StateKey));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Save(DisplaySettingsValue value)
        {
            PlayerPrefs.SetString(StateKey, JsonUtility.ToJson(value));
            PlayerPrefs.Save();
        }
    }
}
