using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class SettingsPageValidationTests
{
    private readonly List<Object> createdObjects = new List<Object>();
    private PrefSettings prefSettings;
    private GeneralSettingsPage generalPage;
    private CesiumSettingsPage cesiumPage;
    private TMP_InputField generalVolume;
    private TMP_InputField cesiumScreenSpaceError;
    private static readonly string[] PreferenceKeys =
    {
        "screenSpaceError", "preloadAncestors", "preloadSiblings", "forbidHoles",
        "MaximumSimultaneousTileLoads", "MaximumCachedBytes", "LoadingDescendantLimit",
        "enableFrustumCulling", "enableFogCulling", "enableForceScreenSpaceError",
        "culledScreenSpaceError", "controlScheme", "enableNovaPopup", "volume",
    };
    private readonly Dictionary<string, int> previousPreferences = new Dictionary<string, int>();
    private readonly HashSet<string> existingPreferences = new HashSet<string>();
    private string settingsPath;
    private byte[] previousSettingsFile;
    private bool hadSettingsFile;

    [SetUp]
    public void SetUp()
    {
        previousPreferences.Clear();
        existingPreferences.Clear();
        foreach (string key in PreferenceKeys)
        {
            if (PlayerPrefs.HasKey(key)) existingPreferences.Add(key);
            previousPreferences[key] = PlayerPrefs.GetInt(key);
        }
        settingsPath = Path.Combine(Application.persistentDataPath, "Settings", "settings.json");
        hadSettingsFile = File.Exists(settingsPath);
        previousSettingsFile = hadSettingsFile ? File.ReadAllBytes(settingsPath) : null;
        prefSettings = CreateGameObject("PrefSettings").AddComponent<PrefSettings>();
        generalPage = CreateGameObject("GeneralSettingsPage").AddComponent<GeneralSettingsPage>();
        cesiumPage = CreateGameObject("CesiumSettingsPage").AddComponent<CesiumSettingsPage>();
        generalPage.SetPrefSettingsForTest(prefSettings);
        cesiumPage.SetPrefSettingsForTest(prefSettings);
        generalVolume = CreateComponent<TMP_InputField>();
        generalPage.SetControlsForTest(generalVolume,
            CreateComponent<Toggle>(), CreateComponent<Button>(), CreateComponent<TextMeshProUGUI>());
        cesiumScreenSpaceError = CreateComponent<TMP_InputField>();
        cesiumPage.SetControlsForTest(cesiumScreenSpaceError, CreateComponent<Toggle>(), CreateComponent<Toggle>(),
            CreateComponent<Toggle>(), CreateComponent<TMP_InputField>(), CreateComponent<TMP_InputField>(),
            CreateComponent<TMP_InputField>(), CreateComponent<Toggle>(), CreateComponent<Toggle>(),
            CreateComponent<Toggle>(), CreateComponent<TMP_InputField>(), CreateComponent<Button>(),
            CreateComponent<TextMeshProUGUI>(), CreateComponent<TextMeshProUGUI>(), CreateComponent<TextMeshProUGUI>(),
            CreateComponent<TextMeshProUGUI>(), CreateComponent<TextMeshProUGUI>(), CreateComponent<TextMeshProUGUI>());
        generalPage.OnPageSelected();
        cesiumPage.OnPageSelected();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (string key in PreferenceKeys)
        {
            if (existingPreferences.Contains(key)) PlayerPrefs.SetInt(key, previousPreferences[key]);
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        if (hadSettingsFile) File.WriteAllBytes(settingsPath, previousSettingsFile);
        else if (File.Exists(settingsPath)) File.Delete(settingsPath);
        foreach (Object createdObject in createdObjects)
        {
            Object.DestroyImmediate(createdObject);
        }

        createdObjects.Clear();
    }

    [TestCase("42", 10, 256, true, 42)]
    [TestCase("nine", 10, 256, false, 0)]
    [TestCase("9", 10, 256, false, 0)]
    [TestCase("257", 10, 256, false, 0)]
    public void TryParseInt_RequiresWholeValueWithinInclusiveRange(
        string input, int min, int max, bool expectedResult, int expectedValue)
    {
        bool parsed = NumericSettingParser.TryParseInt(input, min, max, out int value, out string error);

        Assert.That(parsed, Is.EqualTo(expectedResult));
        Assert.That(value, Is.EqualTo(expectedValue));
        Assert.That(string.IsNullOrEmpty(error), Is.EqualTo(expectedResult));
    }

    [TestCase("0", true)]
    [TestCase("200", true)]
    [TestCase("-1", false)]
    [TestCase("201", false)]
    public void GeneralPage_ApplyValidatesVolumeRange(string volume, bool expectedResult)
    {
        generalPage.SetVolumeForTest(volume);

        bool applied = generalPage.TryApplyChanges();

        Assert.That(applied, Is.EqualTo(expectedResult));
        Assert.That(prefSettings.volume, Is.EqualTo(expectedResult ? int.Parse(volume) : 10));
    }

    [Test]
    public void CesiumPage_ApplyWithInvalidLoadingLimit_DoesNotChangePrefSettings()
    {
        prefSettings.LoadingDescendantLimit = 10;
        cesiumPage.OnPageSelected();
        cesiumPage.SetLoadingDescendantLimitForTest("invalid");

        Assert.That(cesiumPage.TryApplyChanges(), Is.False);
        Assert.That(prefSettings.LoadingDescendantLimit, Is.EqualTo(10));
        Assert.That(cesiumPage.LoadingDescendantLimitError, Does.Contain("whole number"));
    }

    [TestCase("9", false)]
    [TestCase("10", true)]
    [TestCase("256", true)]
    [TestCase("257", false)]
    public void CesiumPage_ApplyValidatesScreenSpaceErrorRange(string value, bool expectedResult)
    {
        cesiumPage.SetScreenSpaceErrorForTest(value);

        Assert.That(cesiumPage.TryApplyChanges(), Is.EqualTo(expectedResult));
        Assert.That(prefSettings.screenSpaceError, Is.EqualTo(expectedResult ? int.Parse(value) : 64));
    }

    [TestCase("-1", false)]
    [TestCase("0", true)]
    [TestCase("1", true)]
    [TestCase("1000", true)]
    [TestCase("1001", false)]
    public void CesiumPage_ApplyValidatesTileLoadRange(string value, bool expectedResult)
    {
        cesiumPage.SetMaximumSimultaneousTileLoadsForTest(value);

        Assert.That(cesiumPage.TryApplyChanges(), Is.EqualTo(expectedResult));
        Assert.That(prefSettings.MaximumSimultaneousTileLoads, Is.EqualTo(expectedResult ? uint.Parse(value) : 28));
    }

    [TestCase("-1", false)]
    [TestCase("0", true)]
    [TestCase("1000", true)]
    [TestCase("1001", false)]
    public void CesiumPage_ApplyValidatesDescendantLimitRange(string value, bool expectedResult)
    {
        cesiumPage.SetLoadingDescendantLimitForTest(value);

        Assert.That(cesiumPage.TryApplyChanges(), Is.EqualTo(expectedResult));
        Assert.That(prefSettings.LoadingDescendantLimit, Is.EqualTo(expectedResult ? uint.Parse(value) : 10));
    }

    [TestCase(-1, false, 0u)]
    [TestCase(0, true, 0u)]
    [TestCase(1000, true, 1000u)]
    [TestCase(1001, false, 0u)]
    public void PrefSettings_TileLimitParserAcceptsInclusiveRangeWithoutWrappingNegativeValues(
        int value, bool expectedResult, uint expectedValue)
    {
        Assert.That(PrefSettings.TryParseCesiumTileLimit(value, out uint parsed), Is.EqualTo(expectedResult));
        Assert.That(parsed, Is.EqualTo(expectedValue));
    }

    [TestCase("MaximumSimultaneousTileLoads", -1, 28u)]
    [TestCase("MaximumSimultaneousTileLoads", 0, 0u)]
    [TestCase("MaximumSimultaneousTileLoads", 1000, 1000u)]
    [TestCase("MaximumSimultaneousTileLoads", 1001, 28u)]
    [TestCase("LoadingDescendantLimit", -1, 10u)]
    [TestCase("LoadingDescendantLimit", 0, 0u)]
    [TestCase("LoadingDescendantLimit", 1000, 1000u)]
    [TestCase("LoadingDescendantLimit", 1001, 10u)]
    public void PrefSettings_PlayerPrefsLoadKeepsPreviousValueWhenLimitInvalid(
        string key, int storedValue, uint expectedValue)
    {
        bool hadValue = PlayerPrefs.HasKey(key);
        int previousValue = PlayerPrefs.GetInt(key);
        try
        {
            PlayerPrefs.SetInt(key, storedValue);
            prefSettings.SettingsToVariables();

            uint actualValue = key == "MaximumSimultaneousTileLoads"
                ? prefSettings.MaximumSimultaneousTileLoads
                : prefSettings.LoadingDescendantLimit;
            Assert.That(actualValue, Is.EqualTo(expectedValue));
        }
        finally
        {
            if (hadValue) PlayerPrefs.SetInt(key, previousValue);
            else PlayerPrefs.DeleteKey(key);
        }
    }

    [TestCase("MaximumSimultaneousTileLoads", -1, false)]
    [TestCase("MaximumSimultaneousTileLoads", 0, true)]
    [TestCase("MaximumSimultaneousTileLoads", 1000, true)]
    [TestCase("MaximumSimultaneousTileLoads", 1001, false)]
    [TestCase("LoadingDescendantLimit", -1, false)]
    [TestCase("LoadingDescendantLimit", 0, true)]
    [TestCase("LoadingDescendantLimit", 1000, true)]
    [TestCase("LoadingDescendantLimit", 1001, false)]
    public void PrefSettings_JsonLoadIgnoresOutOfRangeTileValueButLoadsGeneralValue(string key, int storedValue, bool expectedResult)
    {
        string json = "{\"" + key + "\":" + storedValue + ",\"volume\":42}";

        Assert.That(prefSettings.TryLoadFromJson(json), Is.EqualTo(expectedResult));
        Assert.That(prefSettings.volume, Is.EqualTo(42));
        uint actualValue = key == "MaximumSimultaneousTileLoads"
            ? prefSettings.MaximumSimultaneousTileLoads
            : prefSettings.LoadingDescendantLimit;
        uint previousValue = key == "MaximumSimultaneousTileLoads" ? 28u : 10u;
        Assert.That(actualValue, Is.EqualTo(expectedResult ? (uint)storedValue : previousValue));
    }

    [Test]
    public void PrefSettings_InvalidCesiumLimitStillAppliesAndPersistsGeneralSettings()
    {
        const string tileKey = "MaximumSimultaneousTileLoads";
        const string volumeKey = "volume";
        bool hadTile = PlayerPrefs.HasKey(tileKey);
        bool hadVolume = PlayerPrefs.HasKey(volumeKey);
        int previousTile = PlayerPrefs.GetInt(tileKey);
        int previousVolume = PlayerPrefs.GetInt(volumeKey);
        float previousAudioVolume = AudioListener.volume;
        string settingsPath = Path.Combine(Application.persistentDataPath, "Settings", "settings.json");
        bool hadSettingsFile = File.Exists(settingsPath);
        byte[] previousSettingsFile = hadSettingsFile ? File.ReadAllBytes(settingsPath) : null;
        try
        {
            PlayerPrefs.SetInt(tileKey, 28);
            prefSettings.MaximumSimultaneousTileLoads = 1001;
            prefSettings.volume = 40;
            LogAssert.Expect(LogType.Warning, "Cesium tile limits must be between 0 and 1000. Cesium settings were not applied.");

            prefSettings.VariablesToObjects();

            Assert.That(prefSettings.MaximumSimultaneousTileLoads, Is.EqualTo(1001));
            Assert.That(AudioListener.volume, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(PlayerPrefs.GetInt(volumeKey), Is.EqualTo(40));
            Assert.That(PlayerPrefs.GetInt(tileKey), Is.EqualTo(28));
            string savedJson = File.ReadAllText(settingsPath);
            Assert.That(Regex.IsMatch(savedJson, "\"volume\"\\s*:\\s*40"), Is.True);
            Assert.That(Regex.IsMatch(savedJson, "\"MaximumSimultaneousTileLoads\"\\s*:\\s*28"), Is.True);
        }
        finally
        {
            AudioListener.volume = previousAudioVolume;
            if (hadTile) PlayerPrefs.SetInt(tileKey, previousTile);
            else PlayerPrefs.DeleteKey(tileKey);
            if (hadVolume) PlayerPrefs.SetInt(volumeKey, previousVolume);
            else PlayerPrefs.DeleteKey(volumeKey);
            if (hadSettingsFile) File.WriteAllBytes(settingsPath, previousSettingsFile);
            else if (File.Exists(settingsPath)) File.Delete(settingsPath);
        }
    }

    [Test]
    public void CesiumPage_InvalidTileLimitDoesNotApplyOtherDraftsAndDiscardRestoresSnapshot()
    {
        cesiumPage.SetScreenSpaceErrorForTest("128");
        cesiumPage.SetMaximumSimultaneousTileLoadsForTest("1001");

        Assert.That(cesiumPage.TryApplyChanges(), Is.False);
        Assert.That(prefSettings.screenSpaceError, Is.EqualTo(64));
        Assert.That(prefSettings.MaximumSimultaneousTileLoads, Is.EqualTo(28));
        Assert.That(cesiumPage.MaximumSimultaneousTileLoadsError, Does.Contain("1000"));

        cesiumPage.DiscardChanges();
        Assert.That(cesiumPage.IsDirty, Is.False);
        Assert.That(cesiumPage.MaximumSimultaneousTileLoadsError, Is.Empty);
    }

    [TestCase("0", true)]
    [TestCase("-1", false)]
    public void CesiumPage_ApplyRequiresNonNegativeCachedBytes(string value, bool expectedResult)
    {
        cesiumPage.SetMaximumCachedBytesForTest(value);

        Assert.That(cesiumPage.TryApplyChanges(), Is.EqualTo(expectedResult));
        Assert.That(prefSettings.MaximumCachedBytes, Is.EqualTo(expectedResult ? int.Parse(value) : 1684354560));
    }

    [Test]
    public void GeneralPage_ChangedDraftIsDirtyUntilDiscarded()
    {
        generalPage.SetVolumeForTest("12");

        Assert.That(generalPage.IsDirty, Is.True);

        generalPage.DiscardChanges();

        Assert.That(generalPage.IsDirty, Is.False);
        Assert.That(generalPage.VolumeForTest, Is.EqualTo("10"));
    }

    [Test]
    public void GeneralPage_RealInputEventUpdatesDraftAndApplyState()
    {
        generalVolume.text = "12";

        Assert.That(generalPage.IsDirty, Is.True);
        Assert.That(generalPage.TryApplyChanges(), Is.True);
        Assert.That(prefSettings.volume, Is.EqualTo(12));
    }

    [Test]
    public void GeneralPage_MissingControlsDisablesApplyWithDiagnostic()
    {
        GeneralSettingsPage missing = CreateGameObject("MissingGeneral").AddComponent<GeneralSettingsPage>();
        missing.SetPrefSettingsForTest(prefSettings);
        missing.OnPageSelected();

        Assert.That(missing.TryApplyChanges(), Is.False);
        Assert.That(missing.ApplyError, Does.Contain("controls"));
    }

    [Test]
    public void CesiumPage_DiscardRestoresSnapshotAndClearsValidationErrors()
    {
        cesiumPage.SetLoadingDescendantLimitForTest("invalid");
        Assert.That(cesiumPage.TryApplyChanges(), Is.False);

        cesiumPage.DiscardChanges();

        Assert.That(cesiumPage.IsDirty, Is.False);
        Assert.That(cesiumPage.LoadingDescendantLimitForTest, Is.EqualTo("10"));
        Assert.That(cesiumPage.LoadingDescendantLimitError, Is.Empty);
    }

    [Test]
    public void CesiumPage_RealInputEventUpdatesDraftAndApplyState()
    {
        cesiumScreenSpaceError.text = "128";

        Assert.That(cesiumPage.IsDirty, Is.True);
        Assert.That(cesiumPage.TryApplyChanges(), Is.True);
        Assert.That(prefSettings.screenSpaceError, Is.EqualTo(128));
    }

    [Test]
    public void CesiumPage_MissingControlsDisablesApplyWithDiagnostic()
    {
        CesiumSettingsPage missing = CreateGameObject("MissingCesium").AddComponent<CesiumSettingsPage>();
        missing.SetPrefSettingsForTest(prefSettings);
        missing.OnPageSelected();

        Assert.That(missing.TryApplyChanges(), Is.False);
        Assert.That(missing.LoadingDescendantLimitError, Does.Contain("controls"));
    }

    [Test]
    public void CesiumPage_ApplyWithValidAllFieldsCopiesWholeDraftToPrefSettings()
    {
        cesiumPage.SetScreenSpaceErrorForTest("128");
        cesiumPage.SetPreloadAncestorsForTest(false);
        cesiumPage.SetPreloadSiblingsForTest(false);
        cesiumPage.SetForbidHolesForTest(false);
        cesiumPage.SetMaximumSimultaneousTileLoadsForTest("12");
        cesiumPage.SetMaximumCachedBytesForTest("4096");
        cesiumPage.SetLoadingDescendantLimitForTest("5");
        cesiumPage.SetEnableFrustumCullingForTest(true);
        cesiumPage.SetEnableFogCullingForTest(true);
        cesiumPage.SetEnableForceScreenSpaceErrorForTest(true);
        cesiumPage.SetCulledScreenSpaceErrorForTest("16");

        Assert.That(cesiumPage.TryApplyChanges(), Is.True);
        Assert.That(prefSettings.screenSpaceError, Is.EqualTo(128));
        Assert.That(prefSettings.preloadAncestors, Is.False);
        Assert.That(prefSettings.preloadSiblings, Is.False);
        Assert.That(prefSettings.forbidHoles, Is.False);
        Assert.That(prefSettings.MaximumSimultaneousTileLoads, Is.EqualTo(12));
        Assert.That(prefSettings.MaximumCachedBytes, Is.EqualTo(4096));
        Assert.That(prefSettings.LoadingDescendantLimit, Is.EqualTo(5));
        Assert.That(prefSettings.enableFrustumCulling, Is.True);
        Assert.That(prefSettings.enableFogCulling, Is.True);
        Assert.That(prefSettings.enableForceScreenSpaceError, Is.True);
        Assert.That(prefSettings.culledScreenSpaceError, Is.EqualTo(16));
        Assert.That(cesiumPage.IsDirty, Is.False);
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private T CreateComponent<T>() where T : Component
    {
        GameObject gameObject = CreateGameObject(typeof(T).Name);
        return gameObject.AddComponent<T>();
    }
}
