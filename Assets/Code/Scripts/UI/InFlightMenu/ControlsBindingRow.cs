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
    [SerializeField] private Button rowButton;

    private InputRebindingService service;
    private string actionName;
    private int bindingIndex;
    private Action changed;
    private Action<ControlsBindingRow> rowSelected;
    private bool groupedJoystick;
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
        if (rowButton != null) rowButton.onClick.RemoveListener(SelectRow);
    }

    public void Configure(InputRebindingService rebindingService, InputRebindingCommand command,
        InputBindingRow binding, bool showPrimary, Action onChanged, Action<ControlsBindingRow> onSelected = null,
        bool showGroupedJoystick = false)
    {
        WireListeners();
        service = rebindingService;
        actionName = command.ActionName;
        bindingIndex = binding.BindingIndex;
        changed = onChanged;
        rowSelected = onSelected;
        groupedJoystick = showGroupedJoystick;

        if (commandLabel != null)
        {
            commandLabel.text = command.Label + (binding.IsAvailable && !string.IsNullOrEmpty(binding.BindingName)
                ? " — " + binding.BindingName : string.Empty);
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
            bindingLabel.text = available
                ? groupedJoystick ? service.GetJoystickBindingDisplay(actionName) : service.GetDisplayString(actionName, bindingIndex)
                : "Unavailable";
        }

        if (rebindButton != null) rebindButton.interactable = available;
        if (rowButton != null) rowButton.interactable = available;
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
            if (rowButton != null) rowButton.onClick.RemoveListener(SelectRow);
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

    public void SetRowButtonForTest(Button button)
    {
        if (rowButton != null) rowButton.onClick.RemoveListener(SelectRow);
        rowButton = button;
        if (rowButton != null) rowButton.onClick.AddListener(SelectRow);
    }

    public string ActionName => actionName;
    public int BindingIndex => bindingIndex;
    public bool IsGroupedJoystick => groupedJoystick;
    public string GetCommandLabel() => commandLabel != null ? commandLabel.text : actionName;

    private void WireListeners()
    {
        if (listenersWired)
        {
            return;
        }

        if (rebindButton != null) rebindButton.onClick.AddListener(Rebind);
        if (clearButton != null) clearButton.onClick.AddListener(Clear);
        if (resetButton != null) resetButton.onClick.AddListener(Reset);
        if (rowButton != null) rowButton.onClick.AddListener(SelectRow);
        listenersWired = true;
    }

    private void SelectRow() => rowSelected?.Invoke(this);

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
        }, replaceJoystickBindings: groupedJoystick);
    }

    private void Clear()
    {
        string error = null;
        if (service == null || !(groupedJoystick
            ? service.TryClearJoystickBindings(actionName, out error)
            : service.TryClear(actionName, bindingIndex, out error)))
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
        if (service == null || !(groupedJoystick
            ? service.TryResetJoystickBindings(actionName, out error)
            : service.TryReset(actionName, bindingIndex, out error)))
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
