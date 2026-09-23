using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface ISubtitlePresentationSource
{
    bool IsAvailable { get; }
    void ApplySubtitlePreferences(bool enabled, float scale);
}

// Flight/session controls use their existing immediate UnityEvents.
public sealed class FlightSessionPage : InFlightMenuPage
{
    [SerializeField] private GameObject advancedControlSettings;
    [SerializeField] private Toggle advancedControlToggle;
    [SerializeField] private Toggle autoTrimToggle;
    [SerializeField] private Toggle subtitlesEnabled;
    [SerializeField] private Slider subtitleScale;
    [SerializeField] private TMP_Text subtitleScaleLabel;
    [SerializeField] private TMP_Text subtitleAvailability;
    [SerializeField] private MonoBehaviour subtitleSource;

    private readonly SubtitlePreferences subtitlePreferences = new SubtitlePreferences();
    private Toggle wiredAdvancedControlToggle;
    private Toggle wiredAutoTrimToggle;
    private Toggle wiredSubtitlesEnabled;
    private Slider wiredSubtitleScale;

    private ISubtitlePresentationSource SubtitleSource => subtitleSource as ISubtitlePresentationSource;

    public void SetAdvancedControlsForTest(Toggle disclosure, GameObject settings)
    {
        advancedControlToggle = disclosure;
        advancedControlSettings = settings;
        WireControls();
        RefreshControls();
    }

    public void SetSubtitleControlsForTest(Toggle enabled, Slider scale, TMP_Text scaleLabel,
        TMP_Text availability, MonoBehaviour source)
    {
        subtitlesEnabled = enabled;
        subtitleScale = scale;
        subtitleScaleLabel = scaleLabel;
        subtitleAvailability = availability;
        subtitleSource = source;
        WireControls();
        RefreshControls();
    }

    private void OnEnable()
    {
        WireControls();
        subtitlePreferences.Load();
        RefreshControls();
    }

    public override void OnPageSelected()
    {
        subtitlePreferences.Load();
        RefreshControls();
        AutoTrim autoTrim = HelicopterComponents.Instance != null ? HelicopterComponents.Instance.autoTrim : null;
        if (autoTrimToggle != null && autoTrim != null)
        {
            autoTrimToggle.SetIsOnWithoutNotify(autoTrim._autoTrim);
        }
    }

    private void WireControls()
    {
        if (wiredAdvancedControlToggle != advancedControlToggle)
        {
            if (wiredAdvancedControlToggle != null)
            {
                wiredAdvancedControlToggle.onValueChanged.RemoveListener(SetAdvancedControlsVisible);
            }
            wiredAdvancedControlToggle = advancedControlToggle;
            if (wiredAdvancedControlToggle != null)
            {
                wiredAdvancedControlToggle.onValueChanged.AddListener(SetAdvancedControlsVisible);
            }
        }

        if (wiredSubtitlesEnabled != subtitlesEnabled)
        {
            if (wiredSubtitlesEnabled != null)
            {
                wiredSubtitlesEnabled.onValueChanged.RemoveListener(OnSubtitleOptionsChanged);
            }
            wiredSubtitlesEnabled = subtitlesEnabled;
            if (wiredSubtitlesEnabled != null)
            {
                wiredSubtitlesEnabled.onValueChanged.AddListener(OnSubtitleOptionsChanged);
            }
        }
        if (wiredAutoTrimToggle != autoTrimToggle)
        {
            if (wiredAutoTrimToggle != null)
            {
                wiredAutoTrimToggle.onValueChanged.RemoveListener(OnAutoTrimChanged);
            }
            wiredAutoTrimToggle = autoTrimToggle;
            if (wiredAutoTrimToggle != null)
            {
                wiredAutoTrimToggle.onValueChanged.AddListener(OnAutoTrimChanged);
            }
        }
        if (wiredSubtitleScale != subtitleScale)
        {
            if (wiredSubtitleScale != null)
            {
                wiredSubtitleScale.onValueChanged.RemoveListener(OnSubtitleScaleChanged);
            }
            wiredSubtitleScale = subtitleScale;
            if (wiredSubtitleScale != null)
            {
                wiredSubtitleScale.onValueChanged.AddListener(OnSubtitleScaleChanged);
            }
        }
    }

    private void RefreshControls()
    {
        if (advancedControlToggle != null)
        {
            advancedControlToggle.SetIsOnWithoutNotify(false);
        }
        SetAdvancedControlsVisible(false);

        ISubtitlePresentationSource source = SubtitleSource;
        bool available = source != null && source.IsAvailable;
        if (subtitlesEnabled != null)
        {
            subtitlesEnabled.SetIsOnWithoutNotify(subtitlePreferences.Enabled);
            subtitlesEnabled.interactable = available;
        }
        if (subtitleScale != null)
        {
            subtitleScale.minValue = SubtitlePreferences.MinScalePercent;
            subtitleScale.maxValue = SubtitlePreferences.MaxScalePercent;
            subtitleScale.wholeNumbers = true;
            subtitleScale.SetValueWithoutNotify(subtitlePreferences.ScalePercent);
            subtitleScale.interactable = available;
        }
        if (subtitleScaleLabel != null)
        {
            subtitleScaleLabel.text = $"Subtitle Scale: {subtitlePreferences.ScalePercent}%";
        }
        if (subtitleAvailability != null)
        {
            subtitleAvailability.text = available ? string.Empty : "Subtitles unavailable: no subtitle source is connected.";
        }
        if (available)
        {
            source.ApplySubtitlePreferences(subtitlePreferences.Enabled, subtitlePreferences.ScalePercent / 100f);
        }
    }

    private void SetAdvancedControlsVisible(bool visible)
    {
        if (advancedControlSettings != null)
        {
            advancedControlSettings.SetActive(visible);
        }
    }

    private void OnAutoTrimChanged(bool enabled)
    {
        AutoTrim autoTrim = HelicopterComponents.Instance != null ? HelicopterComponents.Instance.autoTrim : null;
        if (autoTrim == null)
        {
            return;
        }

        if (enabled)
        {
            autoTrim.AutoTrimOn();
        }
        else
        {
            autoTrim.AutoTrimOff();
        }
    }

    private void OnSubtitleScaleChanged(float value)
    {
        SaveSubtitleOptions();
    }

    private void OnSubtitleOptionsChanged(bool _)
    {
        SaveSubtitleOptions();
    }

    private void SaveSubtitleOptions()
    {
        ISubtitlePresentationSource source = SubtitleSource;
        if (source == null || !source.IsAvailable || subtitlesEnabled == null || subtitleScale == null)
        {
            return;
        }

        subtitlePreferences.Save(subtitlesEnabled.isOn, Mathf.RoundToInt(subtitleScale.value));
        source.ApplySubtitlePreferences(subtitlePreferences.Enabled, subtitlePreferences.ScalePercent / 100f);
        if (subtitleScaleLabel != null)
        {
            subtitleScaleLabel.text = $"Subtitle Scale: {subtitlePreferences.ScalePercent}%";
        }
    }
}
