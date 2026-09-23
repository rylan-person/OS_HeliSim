using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ControlsBindingRow : MonoBehaviour
{
    [SerializeField] private TMP_Text commandLabel;
    [SerializeField] private TMP_Text bindingLabel;
    [SerializeField] private TMP_Text primaryBindingLabel;
    [SerializeField] private TMP_Text errorLabel;
    [SerializeField] private Button rebindButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button resetButton;

    private InputRebindingService service;
    private string actionName;
    private int bindingIndex;
    private Action changed;
    private bool listenersWired;

    private void Awake() => WireListeners();

    private void OnDestroy()
    {
        if (!listenersWired)
        {
            return;
        }

        if (rebindButton != null) rebindButton.onClick.RemoveListener(Rebind);
        if (clearButton != null) clearButton.onClick.RemoveListener(Clear);
        if (resetButton != null) resetButton.onClick.RemoveListener(Reset);
    }

    public void Configure(InputRebindingService rebindingService, InputRebindingCommand command,
        InputBindingRow binding, bool showPrimary, Action onChanged)
    {
        WireListeners();
        service = rebindingService;
        actionName = command.ActionName;
        bindingIndex = binding.BindingIndex;
        changed = onChanged;

        if (commandLabel != null)
        {
            commandLabel.text = command.Label + (binding.IsAvailable ? " — " + binding.BindingName : string.Empty);
        }

        if (primaryBindingLabel != null)
        {
            primaryBindingLabel.text = showPrimary && service != null
                ? "Primary: " + service.GetPrimaryBindingDisplay(actionName)
                : string.Empty;
        }

        SetError(binding.Error);
        Refresh();
    }

    public void Refresh()
    {
        bool available = service != null && bindingIndex >= 0;
        if (bindingLabel != null)
        {
            bindingLabel.text = available ? service.GetDisplayString(actionName, bindingIndex) : "Unavailable";
        }

        if (rebindButton != null) rebindButton.interactable = available;
        if (clearButton != null) clearButton.interactable = available;
        if (resetButton != null) resetButton.interactable = available;
    }

    public void SetControlsForTest(TMP_Text command, TMP_Text binding, TMP_Text primary, TMP_Text error,
        Button rebind, Button clear, Button reset)
    {
        if (listenersWired)
        {
            if (rebindButton != null) rebindButton.onClick.RemoveListener(Rebind);
            if (clearButton != null) clearButton.onClick.RemoveListener(Clear);
            if (resetButton != null) resetButton.onClick.RemoveListener(Reset);
            listenersWired = false;
        }

        commandLabel = command;
        bindingLabel = binding;
        primaryBindingLabel = primary;
        errorLabel = error;
        rebindButton = rebind;
        clearButton = clear;
        resetButton = reset;
        WireListeners();
    }

    private void WireListeners()
    {
        if (listenersWired)
        {
            return;
        }

        if (rebindButton != null) rebindButton.onClick.AddListener(Rebind);
        if (clearButton != null) clearButton.onClick.AddListener(Clear);
        if (resetButton != null) resetButton.onClick.AddListener(Reset);
        listenersWired = true;
    }

    private void Rebind()
    {
        if (service == null)
        {
            SetError("Input bindings are unavailable.");
            return;
        }

        SetError("Press a control. Escape cancels.");
        service.TryStartRebind(actionName, bindingIndex, SetError, () =>
        {
            SetError(string.Empty);
            changed?.Invoke();
        });
    }

    private void Clear()
    {
        string error = null;
        if (service == null || !service.TryClear(actionName, bindingIndex, out error))
        {
            SetError(error ?? "Input bindings are unavailable.");
            return;
        }

        SetError(string.Empty);
        changed?.Invoke();
    }

    private void Reset()
    {
        string error = null;
        if (service == null || !service.TryReset(actionName, bindingIndex, out error))
        {
            SetError(error ?? "Input bindings are unavailable.");
            return;
        }

        SetError(string.Empty);
        changed?.Invoke();
    }

    private void SetError(string error)
    {
        if (errorLabel != null)
        {
            errorLabel.text = error ?? string.Empty;
        }
    }
}
