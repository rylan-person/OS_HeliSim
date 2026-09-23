using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GeneralSettingsPage : InFlightMenuPage
{
    [SerializeField] private PrefSettings prefSettings;
    [SerializeField] private TMP_InputField volume;
    [SerializeField] private Toggle enableNovaPopup;
    [SerializeField] private Button applyButton;
    [SerializeField] private TMP_Text diagnosticLabel;
    [SerializeField] private TMP_Text novaStatusLabel;

    private bool snapshotNovaPopup;
    private string snapshotVolume = "10";
    private string volumeDraft = "10";
    private string validationError = string.Empty;
    private bool controlsWired;
    private bool snapshotInitialized;

    public override bool IsDirty => snapshotInitialized && (volumeDraft != snapshotVolume ||
        GetNovaPopupDraft() != snapshotNovaPopup);

    public string VolumeForTest => volumeDraft;
    public string VolumeError => validationError;
    public string ApplyError => validationError;

    private void OnEnable() => WireControls();

    public void SetPrefSettingsForTest(PrefSettings settings)
    {
        prefSettings = settings;
    }

    public void SetControlsForTest(TMP_InputField volumeInput, Toggle novaToggle,
        Button apply, TMP_Text diagnostic)
    {
        volume = volumeInput;
        enableNovaPopup = novaToggle;
        applyButton = apply;
        diagnosticLabel = diagnostic;
        WireControls();
        UpdateAvailability();
    }

    public void SetVolumeForTest(string value)
    {
        volumeDraft = value ?? string.Empty;
        if (volume != null)
        {
            volume.SetTextWithoutNotify(volumeDraft);
        }
    }

    public override void OnPageSelected()
    {
        if (snapshotInitialized && IsDirty)
        {
            return;
        }

        PrefSettings settings = ResolvePrefSettings();
        if (settings == null)
        {
            validationError = "PrefSettings is not assigned.";
            return;
        }

        snapshotNovaPopup = settings.enableNovaPopup;
        snapshotVolume = settings.volume.ToString();
        volumeDraft = snapshotVolume;
        SetControlsFromSnapshot();
        snapshotInitialized = true;
        validationError = string.Empty;
        UpdateAvailability();
        if (novaStatusLabel != null)
        {
            NovaController nova = FindAnyObjectByType<NovaController>(FindObjectsInactive.Include);
            novaStatusLabel.text = nova == null || !nova.active
                ? "NOVA interface: inactive or unavailable."
                : "NOVA interface: initialized; physical platform connection is unverified.";
        }
    }

    public override bool TryApplyChanges()
    {
        PrefSettings settings = ResolvePrefSettings();
        if (!UpdateAvailability() || settings == null)
        {
            validationError = string.IsNullOrEmpty(validationError) ? "Assign PrefSettings and all General settings controls before applying." : validationError;
            SetDiagnostic(validationError);
            return false;
        }

        int parsedVolume = settings.volume;
        if (!NumericSettingParser.TryParseInt(volumeDraft, 0, 200, out parsedVolume, out validationError))
        {
            SetDiagnostic(validationError);
            UpdateAvailability();
            return false;
        }

        bool parsedNovaPopup = GetNovaPopupDraft();
        settings.volume = parsedVolume;
        settings.enableNovaPopup = parsedNovaPopup;
        settings.VariablesToObjects();
        snapshotVolume = parsedVolume.ToString();
        snapshotNovaPopup = parsedNovaPopup;
        volumeDraft = snapshotVolume;
        validationError = string.Empty;
        SetDiagnostic(string.Empty);
        UpdateAvailability();
        return true;
    }

    public override void DiscardChanges()
    {
        volumeDraft = snapshotVolume;
        SetControlsFromSnapshot();
        validationError = string.Empty;
        UpdateAvailability();
    }

    private PrefSettings ResolvePrefSettings()
    {
        if (prefSettings == null)
        {
            prefSettings = GetComponentInParent<PrefSettings>();
        }

        return prefSettings;
    }

    private bool GetNovaPopupDraft()
    {
        return enableNovaPopup == null ? snapshotNovaPopup : enableNovaPopup.isOn;
    }

    private void SetControlsFromSnapshot()
    {
        if (volume != null)
        {
            volume.SetTextWithoutNotify(volumeDraft);
        }
        if (enableNovaPopup != null)
        {
            enableNovaPopup.SetIsOnWithoutNotify(snapshotNovaPopup);
        }
    }

    private void WireControls()
    {
        if (controlsWired)
        {
            if (volume != null) volume.onValueChanged.RemoveListener(OnVolumeChanged);
            if (enableNovaPopup != null) enableNovaPopup.onValueChanged.RemoveListener(OnNovaPopupChanged);
        }

        if (volume != null) volume.onValueChanged.AddListener(OnVolumeChanged);
        if (enableNovaPopup != null) enableNovaPopup.onValueChanged.AddListener(OnNovaPopupChanged);
        controlsWired = true;
    }

    private void OnVolumeChanged(string value) { volumeDraft = value ?? string.Empty; ClearDiagnosticOnEdit(); }
    private void OnNovaPopupChanged(bool value) { ClearDiagnosticOnEdit(); }

    private bool UpdateAvailability()
    {
        bool ready = ResolvePrefSettings() != null && volume != null && enableNovaPopup != null;
        if (!ready)
        {
            SetDiagnostic("Assign PrefSettings and the controls owned by this settings page before applying.");
        }
        else if (string.IsNullOrEmpty(validationError))
        {
            SetDiagnostic(string.Empty);
        }
        if (applyButton != null) applyButton.interactable = ready && string.IsNullOrEmpty(validationError);
        return ready;
    }

    private void ClearDiagnosticOnEdit()
    {
        validationError = string.Empty;
        if (applyButton != null) applyButton.interactable = UpdateAvailability();
    }

    private void SetDiagnostic(string message)
    {
        if (diagnosticLabel != null) diagnosticLabel.text = message ?? string.Empty;
    }
}
