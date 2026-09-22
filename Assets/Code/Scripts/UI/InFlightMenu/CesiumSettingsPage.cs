using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CesiumSettingsPage : InFlightMenuPage
{
    [SerializeField] private PrefSettings prefSettings;
    [SerializeField] private TMP_InputField screenSpaceError;
    [SerializeField] private Toggle preloadAncestors;
    [SerializeField] private Toggle preloadSiblings;
    [SerializeField] private Toggle forbidHoles;
    [SerializeField] private TMP_InputField maximumSimultaneousTileLoads;
    [SerializeField] private TMP_InputField maximumCachedBytes;
    [SerializeField] private TMP_InputField loadingDescendantLimit;
    [SerializeField] private Toggle enableFrustumCulling;
    [SerializeField] private Toggle enableFogCulling;
    [SerializeField] private Toggle enableForceScreenSpaceError;
    [SerializeField] private TMP_InputField culledScreenSpaceError;

    private string snapshotScreenSpaceError = "64";
    private string snapshotMaximumSimultaneousTileLoads = "28";
    private string snapshotMaximumCachedBytes = "1684354560";
    private string snapshotLoadingDescendantLimit = "10";
    private string snapshotCulledScreenSpaceError = "0";
    private string screenSpaceErrorDraft = "64";
    private string maximumSimultaneousTileLoadsDraft = "28";
    private string maximumCachedBytesDraft = "1684354560";
    private string loadingDescendantLimitDraft = "10";
    private string culledScreenSpaceErrorDraft = "0";
    private bool snapshotPreloadAncestors;
    private bool snapshotPreloadSiblings;
    private bool snapshotForbidHoles;
    private bool snapshotEnableFrustumCulling;
    private bool snapshotEnableFogCulling;
    private bool snapshotEnableForceScreenSpaceError;
    private bool preloadAncestorsDraft;
    private bool preloadSiblingsDraft;
    private bool forbidHolesDraft;
    private bool enableFrustumCullingDraft;
    private bool enableFogCullingDraft;
    private bool enableForceScreenSpaceErrorDraft;

    public string ScreenSpaceErrorError { get; private set; } = string.Empty;
    public string MaximumSimultaneousTileLoadsError { get; private set; } = string.Empty;
    public string MaximumCachedBytesError { get; private set; } = string.Empty;
    public string LoadingDescendantLimitError { get; private set; } = string.Empty;
    public string CulledScreenSpaceErrorError { get; private set; } = string.Empty;
    public string LoadingDescendantLimitForTest => loadingDescendantLimitDraft;
    public override bool IsDirty => screenSpaceErrorDraft != snapshotScreenSpaceError ||
        maximumSimultaneousTileLoadsDraft != snapshotMaximumSimultaneousTileLoads ||
        maximumCachedBytesDraft != snapshotMaximumCachedBytes ||
        loadingDescendantLimitDraft != snapshotLoadingDescendantLimit ||
        culledScreenSpaceErrorDraft != snapshotCulledScreenSpaceError ||
        preloadAncestorsDraft != snapshotPreloadAncestors ||
        preloadSiblingsDraft != snapshotPreloadSiblings ||
        forbidHolesDraft != snapshotForbidHoles ||
        enableFrustumCullingDraft != snapshotEnableFrustumCulling ||
        enableFogCullingDraft != snapshotEnableFogCulling ||
        enableForceScreenSpaceErrorDraft != snapshotEnableForceScreenSpaceError;

    public void SetPrefSettingsForTest(PrefSettings settings) => prefSettings = settings;
    public void SetScreenSpaceErrorForTest(string value) => SetDraft(ref screenSpaceErrorDraft, screenSpaceError, value);
    public void SetMaximumSimultaneousTileLoadsForTest(string value) => SetDraft(ref maximumSimultaneousTileLoadsDraft, maximumSimultaneousTileLoads, value);
    public void SetMaximumCachedBytesForTest(string value) => SetDraft(ref maximumCachedBytesDraft, maximumCachedBytes, value);
    public void SetLoadingDescendantLimitForTest(string value) => SetDraft(ref loadingDescendantLimitDraft, loadingDescendantLimit, value);
    public void SetCulledScreenSpaceErrorForTest(string value) => SetDraft(ref culledScreenSpaceErrorDraft, culledScreenSpaceError, value);
    public void SetPreloadAncestorsForTest(bool value) => SetToggle(ref preloadAncestorsDraft, preloadAncestors, value);
    public void SetPreloadSiblingsForTest(bool value) => SetToggle(ref preloadSiblingsDraft, preloadSiblings, value);
    public void SetForbidHolesForTest(bool value) => SetToggle(ref forbidHolesDraft, forbidHoles, value);
    public void SetEnableFrustumCullingForTest(bool value) => SetToggle(ref enableFrustumCullingDraft, enableFrustumCulling, value);
    public void SetEnableFogCullingForTest(bool value) => SetToggle(ref enableFogCullingDraft, enableFogCulling, value);
    public void SetEnableForceScreenSpaceErrorForTest(bool value) => SetToggle(ref enableForceScreenSpaceErrorDraft, enableForceScreenSpaceError, value);

    public override void OnPageSelected()
    {
        if (IsDirty)
        {
            return;
        }

        PrefSettings settings = ResolvePrefSettings();
        if (settings == null)
        {
            LoadingDescendantLimitError = "PrefSettings is not assigned.";
            return;
        }

        snapshotScreenSpaceError = settings.screenSpaceError.ToString();
        snapshotPreloadAncestors = settings.preloadAncestors;
        snapshotPreloadSiblings = settings.preloadSiblings;
        snapshotForbidHoles = settings.forbidHoles;
        snapshotMaximumSimultaneousTileLoads = settings.MaximumSimultaneousTileLoads.ToString();
        snapshotMaximumCachedBytes = settings.MaximumCachedBytes.ToString();
        snapshotLoadingDescendantLimit = settings.LoadingDescendantLimit.ToString();
        snapshotEnableFrustumCulling = settings.enableFrustumCulling;
        snapshotEnableFogCulling = settings.enableFogCulling;
        snapshotEnableForceScreenSpaceError = settings.enableForceScreenSpaceError;
        snapshotCulledScreenSpaceError = settings.culledScreenSpaceError.ToString();
        CopySnapshotToDraft();
        ClearErrors();
    }

    public override bool TryApplyChanges()
    {
        PrefSettings settings = ResolvePrefSettings();
        if (settings == null)
        {
            LoadingDescendantLimitError = "PrefSettings is not assigned.";
            return false;
        }

        ClearErrors();
        bool valid = NumericSettingParser.TryParseInt(screenSpaceErrorDraft, 10, 256, out int parsedScreenSpaceError, out string error);
        ScreenSpaceErrorError = valid ? string.Empty : error;
        valid &= NumericSettingParser.TryParseInt(maximumSimultaneousTileLoadsDraft, 1, 1000, out int parsedTileLoads, out error);
        MaximumSimultaneousTileLoadsError = valid ? string.Empty : error;
        valid &= NumericSettingParser.TryParseInt(maximumCachedBytesDraft, 0, int.MaxValue, out int parsedCachedBytes, out error);
        MaximumCachedBytesError = valid ? string.Empty : error;
        valid &= NumericSettingParser.TryParseInt(loadingDescendantLimitDraft, 0, 1000, out int parsedDescendantLimit, out error);
        LoadingDescendantLimitError = valid ? string.Empty : error;
        valid &= NumericSettingParser.TryParseInt(culledScreenSpaceErrorDraft, 0, 256, out int parsedCulledError, out error);
        CulledScreenSpaceErrorError = valid ? string.Empty : error;
        if (!valid)
        {
            return false;
        }

        settings.screenSpaceError = parsedScreenSpaceError;
        settings.preloadAncestors = preloadAncestorsDraft;
        settings.preloadSiblings = preloadSiblingsDraft;
        settings.forbidHoles = forbidHolesDraft;
        settings.MaximumSimultaneousTileLoads = (uint)parsedTileLoads;
        settings.MaximumCachedBytes = parsedCachedBytes;
        settings.LoadingDescendantLimit = (uint)parsedDescendantLimit;
        settings.enableFrustumCulling = enableFrustumCullingDraft;
        settings.enableFogCulling = enableFogCullingDraft;
        settings.enableForceScreenSpaceError = enableForceScreenSpaceErrorDraft;
        settings.culledScreenSpaceError = parsedCulledError;
        settings.VariablesToObjects();
        snapshotScreenSpaceError = screenSpaceErrorDraft;
        snapshotMaximumSimultaneousTileLoads = maximumSimultaneousTileLoadsDraft;
        snapshotMaximumCachedBytes = maximumCachedBytesDraft;
        snapshotLoadingDescendantLimit = loadingDescendantLimitDraft;
        snapshotCulledScreenSpaceError = culledScreenSpaceErrorDraft;
        snapshotPreloadAncestors = settings.preloadAncestors;
        snapshotPreloadSiblings = settings.preloadSiblings;
        snapshotForbidHoles = settings.forbidHoles;
        snapshotEnableFrustumCulling = settings.enableFrustumCulling;
        snapshotEnableFogCulling = settings.enableFogCulling;
        snapshotEnableForceScreenSpaceError = settings.enableForceScreenSpaceError;
        return true;
    }

    public override void DiscardChanges()
    {
        CopySnapshotToDraft();
        ClearErrors();
    }

    private PrefSettings ResolvePrefSettings()
    {
        if (prefSettings == null)
        {
            prefSettings = GetComponentInParent<PrefSettings>();
        }
        return prefSettings;
    }

    private void CopySnapshotToDraft()
    {
        screenSpaceErrorDraft = snapshotScreenSpaceError;
        maximumSimultaneousTileLoadsDraft = snapshotMaximumSimultaneousTileLoads;
        maximumCachedBytesDraft = snapshotMaximumCachedBytes;
        loadingDescendantLimitDraft = snapshotLoadingDescendantLimit;
        culledScreenSpaceErrorDraft = snapshotCulledScreenSpaceError;
        SetDraft(ref screenSpaceErrorDraft, screenSpaceError, screenSpaceErrorDraft);
        SetDraft(ref maximumSimultaneousTileLoadsDraft, maximumSimultaneousTileLoads, maximumSimultaneousTileLoadsDraft);
        SetDraft(ref maximumCachedBytesDraft, maximumCachedBytes, maximumCachedBytesDraft);
        SetDraft(ref loadingDescendantLimitDraft, loadingDescendantLimit, loadingDescendantLimitDraft);
        SetDraft(ref culledScreenSpaceErrorDraft, culledScreenSpaceError, culledScreenSpaceErrorDraft);
        SetToggle(preloadAncestors, snapshotPreloadAncestors);
        SetToggle(preloadSiblings, snapshotPreloadSiblings);
        SetToggle(forbidHoles, snapshotForbidHoles);
        SetToggle(enableFrustumCulling, snapshotEnableFrustumCulling);
        SetToggle(enableFogCulling, snapshotEnableFogCulling);
        SetToggle(enableForceScreenSpaceError, snapshotEnableForceScreenSpaceError);
        preloadAncestorsDraft = snapshotPreloadAncestors;
        preloadSiblingsDraft = snapshotPreloadSiblings;
        forbidHolesDraft = snapshotForbidHoles;
        enableFrustumCullingDraft = snapshotEnableFrustumCulling;
        enableFogCullingDraft = snapshotEnableFogCulling;
        enableForceScreenSpaceErrorDraft = snapshotEnableForceScreenSpaceError;
    }

    private void ClearErrors()
    {
        ScreenSpaceErrorError = string.Empty;
        MaximumSimultaneousTileLoadsError = string.Empty;
        MaximumCachedBytesError = string.Empty;
        LoadingDescendantLimitError = string.Empty;
        CulledScreenSpaceErrorError = string.Empty;
    }

    private static bool GetToggle(Toggle toggle, bool fallback) => toggle == null ? fallback : toggle.isOn;
    private static void SetToggle(Toggle toggle, bool value) { if (toggle != null) toggle.SetIsOnWithoutNotify(value); }
    private static void SetToggle(ref bool draft, Toggle toggle, bool value)
    {
        draft = value;
        SetToggle(toggle, value);
    }
    private static void SetDraft(ref string draft, TMP_InputField input, string value)
    {
        draft = value ?? string.Empty;
        if (input != null) input.SetTextWithoutNotify(draft);
    }
}
