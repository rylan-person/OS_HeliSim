using System;
using UnityEngine;

namespace HeliSim.InFlightMenu
{
    [Serializable]
    public struct DisplaySettingsValue : IEquatable<DisplaySettingsValue>
    {
        public FullScreenMode WindowMode;
        public int Width;
        public int Height;
        public bool VSync;
        public int FrameRateCap;
        public int QualityLevel;
        public float UiScale;

        public bool Equals(DisplaySettingsValue other)
        {
            return WindowMode == other.WindowMode && Width == other.Width && Height == other.Height &&
                VSync == other.VSync && FrameRateCap == other.FrameRateCap &&
                QualityLevel == other.QualityLevel && Mathf.Approximately(UiScale, other.UiScale);
        }
    }

    public interface IDisplaySettingsRuntime
    {
        DisplaySettingsValue Capture();
        bool SupportsUiScale { get; }
        int QualityLevelCount { get; }
        bool SupportsResolution(int width, int height);
        bool TryApply(DisplaySettingsValue value, out string error);
    }

    public interface IDisplaySettingsStore
    {
        bool TryLoad(out DisplaySettingsValue value);
        void Save(DisplaySettingsValue value);
    }

    /// <summary>Stages changes until Apply. Revert restores the last applied snapshot.</summary>
    public sealed class DisplaySettingsModel
    {
        private readonly IDisplaySettingsRuntime runtime;
        private readonly IDisplaySettingsStore store;

        public DisplaySettingsValue Applied { get; private set; }
        public DisplaySettingsValue Draft { get; private set; }
        public string Error { get; private set; } = string.Empty;
        public bool IsDirty => !Draft.Equals(Applied);
        public bool UiScaleAvailable => runtime.SupportsUiScale;

        public DisplaySettingsModel(IDisplaySettingsRuntime runtime, IDisplaySettingsStore store)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Applied = runtime.Capture();
            Draft = Applied;
        }

        public void Stage(DisplaySettingsValue value)
        {
            Draft = value;
            Error = string.Empty;
        }

        public void Revert()
        {
            Draft = Applied;
            Error = string.Empty;
        }

        public bool TryApply()
        {
            if (!Validate(Draft, out string error))
            {
                Error = error;
                return false;
            }

            DisplaySettingsValue before = runtime.Capture();
            if (!runtime.TryApply(Draft, out error))
            {
                Error = error;
                return false;
            }

            try
            {
                store.Save(Draft);
            }
            catch (Exception exception)
            {
                runtime.TryApply(before, out _);
                Error = "Could not save display settings: " + exception.Message;
                return false;
            }

            Applied = Draft;
            Error = string.Empty;
            return true;
        }

        public bool RestoreSaved()
        {
            if (!store.TryLoad(out DisplaySettingsValue saved)) return false;
            if (!runtime.SupportsUiScale) saved.UiScale = Applied.UiScale;
            if (!Validate(saved, out string error))
            {
                Error = "Saved display settings are unavailable: " + error;
                return false;
            }

            if (!runtime.TryApply(saved, out error))
            {
                Error = error;
                return false;
            }

            Applied = saved;
            Draft = saved;
            Error = string.Empty;
            return true;
        }

        private bool Validate(DisplaySettingsValue value, out string error)
        {
            if (value.WindowMode != FullScreenMode.ExclusiveFullScreen &&
                value.WindowMode != FullScreenMode.FullScreenWindow &&
                value.WindowMode != FullScreenMode.MaximizedWindow &&
                value.WindowMode != FullScreenMode.Windowed)
            {
                error = "Unsupported window mode.";
                return false;
            }
            if (!runtime.SupportsResolution(value.Width, value.Height))
            {
                error = "Resolution is not available on this display.";
                return false;
            }
            if (value.FrameRateCap != -1 && (value.FrameRateCap < 30 || value.FrameRateCap > 240))
            {
                error = "Frame rate cap must be Unlimited or 30–240 FPS.";
                return false;
            }
            if (value.QualityLevel < 0 || value.QualityLevel >= runtime.QualityLevelCount)
            {
                error = "Quality preset is unavailable.";
                return false;
            }
            if (float.IsNaN(value.UiScale) || float.IsInfinity(value.UiScale) || value.UiScale < 0.75f || value.UiScale > 1.5f)
            {
                error = "UI scale must be 75–150%.";
                return false;
            }
            if (!runtime.SupportsUiScale && !Mathf.Approximately(value.UiScale, Applied.UiScale))
            {
                error = "UI scale is unavailable because the menu has no CanvasScaler.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
