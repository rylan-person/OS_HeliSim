using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SettingsPageValidationTests
{
    private readonly List<Object> createdObjects = new List<Object>();
    private PrefSettings prefSettings;
    private GeneralSettingsPage generalPage;
    private CesiumSettingsPage cesiumPage;

    [SetUp]
    public void SetUp()
    {
        prefSettings = CreateGameObject("PrefSettings").AddComponent<PrefSettings>();
        generalPage = CreateGameObject("GeneralSettingsPage").AddComponent<GeneralSettingsPage>();
        cesiumPage = CreateGameObject("CesiumSettingsPage").AddComponent<CesiumSettingsPage>();
        generalPage.SetPrefSettingsForTest(prefSettings);
        cesiumPage.SetPrefSettingsForTest(prefSettings);
        generalPage.OnPageSelected();
        cesiumPage.OnPageSelected();
    }

    [TearDown]
    public void TearDown()
    {
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

    [TestCase("0", false)]
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
}
