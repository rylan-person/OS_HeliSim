using UnityEngine;

/// <summary>Presentation preferences, independent of flight and hardware settings.</summary>
public sealed class SubtitlePreferences
{
    public const string EnabledKey = "presentation.subtitles.enabled";
    public const string ScalePercentKey = "presentation.subtitles.scalePercent";
    public const int DefaultScalePercent = 100;
    public const int MinScalePercent = 75;
    public const int MaxScalePercent = 150;

    public bool Enabled { get; private set; } = true;
    public int ScalePercent { get; private set; } = DefaultScalePercent;

    public void Load()
    {
        Enabled = PlayerPrefs.GetInt(EnabledKey, 1) != 0;
        ScalePercent = Mathf.Clamp(PlayerPrefs.GetInt(ScalePercentKey, DefaultScalePercent),
            MinScalePercent, MaxScalePercent);
    }

    public void Save(bool enabled, int scalePercent)
    {
        Enabled = enabled;
        ScalePercent = Mathf.Clamp(scalePercent, MinScalePercent, MaxScalePercent);
        PlayerPrefs.SetInt(EnabledKey, Enabled ? 1 : 0);
        PlayerPrefs.SetInt(ScalePercentKey, ScalePercent);
        PlayerPrefs.Save();
    }
}
