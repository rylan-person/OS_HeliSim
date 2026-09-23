using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SubtitlePreferencesTests
{
    private bool hadEnabled;
    private bool hadScale;
    private int previousEnabled;
    private int previousScale;

    [SetUp]
    public void SetUp()
    {
        hadEnabled = PlayerPrefs.HasKey(SubtitlePreferences.EnabledKey);
        hadScale = PlayerPrefs.HasKey(SubtitlePreferences.ScalePercentKey);
        previousEnabled = PlayerPrefs.GetInt(SubtitlePreferences.EnabledKey);
        previousScale = PlayerPrefs.GetInt(SubtitlePreferences.ScalePercentKey);
        ClearPreferences();
    }

    [TearDown]
    public void TearDown()
    {
        if (hadEnabled) PlayerPrefs.SetInt(SubtitlePreferences.EnabledKey, previousEnabled);
        else PlayerPrefs.DeleteKey(SubtitlePreferences.EnabledKey);
        if (hadScale) PlayerPrefs.SetInt(SubtitlePreferences.ScalePercentKey, previousScale);
        else PlayerPrefs.DeleteKey(SubtitlePreferences.ScalePercentKey);
        PlayerPrefs.Save();
    }

    [Test]
    public void LoadWithoutSavedPreferences_UsesPresentationDefaults()
    {
        var preferences = new SubtitlePreferences();

        preferences.Load();

        Assert.That(preferences.Enabled, Is.True);
        Assert.That(preferences.ScalePercent, Is.EqualTo(100));
    }

    [Test]
    public void SaveThenLoad_RestoresEnabledAndScale()
    {
        new SubtitlePreferences().Save(false, 125);

        var loaded = new SubtitlePreferences();
        loaded.Load();

        Assert.That(loaded.Enabled, Is.False);
        Assert.That(loaded.ScalePercent, Is.EqualTo(125));
    }

    [Test]
    public void LoadInvalidSavedScale_ClampsToSupportedRange()
    {
        PlayerPrefs.SetInt(SubtitlePreferences.ScalePercentKey, 900);

        var preferences = new SubtitlePreferences();
        preferences.Load();

        Assert.That(preferences.ScalePercent, Is.EqualTo(150));
    }

    [Test]
    public void PageWithoutSubtitleSource_DisablesControlsAndShowsReason()
    {
        var pageObject = new GameObject("FlightSessionPage");
        var toggleObject = new GameObject("SubtitlesToggle");
        var sliderObject = new GameObject("SubtitleScale");
        var diagnosticObject = new GameObject("SubtitleAvailability");
        try
        {
            var page = pageObject.AddComponent<FlightSessionPage>();
            var toggle = toggleObject.AddComponent<Toggle>();
            var slider = sliderObject.AddComponent<Slider>();
            var diagnostic = diagnosticObject.AddComponent<TextMeshProUGUI>();

            page.SetSubtitleControlsForTest(toggle, slider, null, diagnostic, null);
            page.OnPageSelected();

            Assert.That(toggle.interactable, Is.False);
            Assert.That(slider.interactable, Is.False);
            Assert.That(diagnostic.text, Does.Contain("no subtitle source"));
        }
        finally
        {
            Object.DestroyImmediate(pageObject);
            Object.DestroyImmediate(toggleObject);
            Object.DestroyImmediate(sliderObject);
            Object.DestroyImmediate(diagnosticObject);
        }
    }

    [Test]
    public void AdvancedSettings_AreCollapsedOnPageSelectionAndToggleOpen()
    {
        var pageObject = new GameObject("FlightSessionPage");
        var disclosureObject = new GameObject("AdvancedDisclosure");
        var settings = new GameObject("AdvancedSettings");
        try
        {
            var page = pageObject.AddComponent<FlightSessionPage>();
            var disclosure = disclosureObject.AddComponent<Toggle>();
            page.SetAdvancedControlsForTest(disclosure, settings);

            Assert.That(settings.activeSelf, Is.False);

            disclosure.isOn = true;
            Assert.That(settings.activeSelf, Is.True);

            page.OnPageSelected();
            Assert.That(settings.activeSelf, Is.False);
            Assert.That(disclosure.isOn, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(pageObject);
            Object.DestroyImmediate(disclosureObject);
            Object.DestroyImmediate(settings);
        }
    }

    private static void ClearPreferences()
    {
        PlayerPrefs.DeleteKey(SubtitlePreferences.EnabledKey);
        PlayerPrefs.DeleteKey(SubtitlePreferences.ScalePercentKey);
    }
}
