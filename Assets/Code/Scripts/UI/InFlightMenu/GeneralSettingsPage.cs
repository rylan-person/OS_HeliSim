using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GeneralSettingsPage : InFlightMenuPage
{
    [SerializeField] private PrefSettings prefSettings;
    [SerializeField] private TMP_Dropdown controlScheme;
    [SerializeField] private TMP_InputField volume;
    [SerializeField] private Toggle enableNovaPopup;

    private ControlScheme snapshotControlScheme;
    private bool snapshotNovaPopup;
    private string snapshotVolume = "10";
    private string volumeDraft = "10";
    private string validationError = string.Empty;

    public override bool IsDirty => volumeDraft != snapshotVolume ||
        GetControlSchemeDraft() != snapshotControlScheme ||
        GetNovaPopupDraft() != snapshotNovaPopup;

    public string VolumeForTest => volumeDraft;
    public string VolumeError => validationError;
    public string ApplyError => validationError;

    public void SetPrefSettingsForTest(PrefSettings settings)
    {
        prefSettings = settings;
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
        if (IsDirty)
        {
            return;
        }

        PrefSettings settings = ResolvePrefSettings();
        if (settings == null)
        {
            validationError = "PrefSettings is not assigned.";
            return;
        }

        snapshotControlScheme = settings.controlScheme;
        snapshotNovaPopup = settings.enableNovaPopup;
        snapshotVolume = settings.volume.ToString();
        volumeDraft = snapshotVolume;
        SetControlsFromSnapshot();
        validationError = string.Empty;
    }

    public override bool TryApplyChanges()
    {
        PrefSettings settings = ResolvePrefSettings();
        if (settings == null)
        {
            validationError = "PrefSettings is not assigned.";
            return false;
        }

        if (!NumericSettingParser.TryParseInt(volumeDraft, 0, 200, out int parsedVolume, out validationError))
        {
            return false;
        }

        ControlScheme parsedScheme = GetControlSchemeDraft();
        bool parsedNovaPopup = GetNovaPopupDraft();
        settings.controlScheme = parsedScheme;
        settings.volume = parsedVolume;
        settings.enableNovaPopup = parsedNovaPopup;
        settings.VariablesToObjects();
        snapshotControlScheme = parsedScheme;
        snapshotVolume = parsedVolume.ToString();
        snapshotNovaPopup = parsedNovaPopup;
        volumeDraft = snapshotVolume;
        validationError = string.Empty;
        return true;
    }

    public override void DiscardChanges()
    {
        volumeDraft = snapshotVolume;
        SetControlsFromSnapshot();
        validationError = string.Empty;
    }

    private PrefSettings ResolvePrefSettings()
    {
        if (prefSettings == null)
        {
            prefSettings = GetComponentInParent<PrefSettings>();
        }

        return prefSettings;
    }

    private ControlScheme GetControlSchemeDraft()
    {
        return controlScheme == null ? snapshotControlScheme : (ControlScheme)controlScheme.value;
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
        if (controlScheme != null)
        {
            controlScheme.SetValueWithoutNotify((int)snapshotControlScheme);
        }
        if (enableNovaPopup != null)
        {
            enableNovaPopup.SetIsOnWithoutNotify(snapshotNovaPopup);
        }
    }
}
