using System;
using System.Collections.Generic;
using HeliSim.InFlightMenu;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public sealed class ControlsRebindingPage : InFlightMenuPage
{
    [SerializeField] private InputActionAsset defaultActions;
    [SerializeField] private ControlsBindingRow bindingRowPrefab;
    [SerializeField] private Transform bindingRowsRoot;
    [SerializeField] private Button resetProfileButton;
    [SerializeField] private TMP_Text diagnosticLabel;
    [SerializeField] private TMP_Text hardwareSummaryLabel;
    [SerializeField] private Button keyboardTabButton;
    [SerializeField] private Button joystickTabButton;
    [SerializeField] private GameObject yawModeRoot;
    [SerializeField] private Toggle yawModeToggle;
    [SerializeField] private GameObject bindingDialogRoot;
    [SerializeField] private TMP_Text dialogTitleLabel;
    [SerializeField] private TMP_Text dialogBindingLabel;
    [SerializeField] private TMP_Text dialogStatusLabel;
    [SerializeField] private Button dialogCancelButton;
    [SerializeField] private Button dialogClearButton;
    [SerializeField] private Button dialogRestoreButton;
    [SerializeField] private TMP_Text categoryHeadingPrefab;

    private readonly List<ControlsBindingRow> rows = new List<ControlsBindingRow>();
    private InputRebindingService service;
    private InputRebindingService ownedService;
    private InputActionAsset fallbackActions;
    private IBindingOverrideStore store = new PlayerPrefsBindingOverrideStore();
    private Button wiredResetProfileButton;
    private bool injectedServiceForTest;
    private BindingDeviceTab selectedTab = BindingDeviceTab.KeyboardMouse;
    private ControlsBindingRow selectedRow;
    private int dialogClosedFrame = -1;
    private bool closingDialog;
    private int dialogOpenedFrame = -1;
    private bool presentationWired;
    private readonly List<TMP_Text> headings = new List<TMP_Text>();

    public InputRebindingService Service => service;
    public bool IsBindingDialogOpen => bindingDialogRoot != null && bindingDialogRoot.activeSelf;

    private void OnEnable() => WireResetButton();

    private void OnDestroy()
    {
        if (wiredResetProfileButton != null)
        {
            wiredResetProfileButton.onClick.RemoveListener(ResetProfile);
        }

        ReleaseOwnedService();
        UnwirePresentation();
    }

    public override void OnPageSelected()
    {
        WireResetButton();
        WirePresentation();
        CloseDialog();
        selectedTab = BindingDeviceTab.KeyboardMouse;
        UpdateTabButtons();
        ResolveService();
        UpdateYawModeControls();
        RebuildRows();
        RefreshHardwareSummary();
    }

    public override void OnPageDeselected()
    {
        service?.CancelRebind();
        CloseDialog();
    }

    public void SetPresentationForTest(Button keyboardTab, Button joystickTab, GameObject dialog,
        TMP_Text title, TMP_Text binding, TMP_Text status, Button cancel, Button clear, Button restore)
    {
        UnwirePresentation();
        keyboardTabButton = keyboardTab;
        joystickTabButton = joystickTab;
        bindingDialogRoot = dialog;
        dialogTitleLabel = title;
        dialogBindingLabel = binding;
        dialogStatusLabel = status;
        dialogCancelButton = cancel;
        dialogClearButton = clear;
        dialogRestoreButton = restore;
        WirePresentation();
    }

    public bool TryHandleEscape()
    {
        if (IsBindingDialogOpen)
        {
            CloseDialog();
            return true;
        }

        return dialogClosedFrame == Time.frameCount;
    }

    public void SetControlsForTest(InputActionAsset fallback, IBindingOverrideStore overrideStore,
        ControlsBindingRow rowPrefab, Transform rowsRoot, Button resetProfile,
        TMP_Text diagnostic, TMP_Text hardwareSummary)
    {
        defaultActions = fallback;
        store = overrideStore ?? new PlayerPrefsBindingOverrideStore();
        bindingRowPrefab = rowPrefab;
        bindingRowsRoot = rowsRoot;
        resetProfileButton = resetProfile;
        diagnosticLabel = diagnostic;
        hardwareSummaryLabel = hardwareSummary;
        WireResetButton();
    }

    public void SetServiceForTest(InputRebindingService injectedService)
    {
        ReleaseOwnedService();
        service = injectedService;
        injectedServiceForTest = injectedService != null;
    }

    private void ResolveService()
    {
        if (injectedServiceForTest)
        {
            return;
        }

        HelicopterComponents localHelicopter = HelicopterComponents.Instance;
        PlayerInput localPlayerInput = localHelicopter != null
            ? localHelicopter.GetComponentInParent<PlayerInput>()
            : null;
        InputActionAsset targetActions = localPlayerInput != null ? localPlayerInput.actions : null;

        if (targetActions != null)
        {
            PlayerInputBindingOverrides loader = localPlayerInput.GetComponent<PlayerInputBindingOverrides>();
            InputRebindingService loadedService = loader != null ? loader.Service : null;
            if (loadedService != null)
            {
                if (service != loadedService)
                {
                    ReleaseOwnedService();
                    service = loadedService;
                }

                loadedService.PairConnectedDevices();
                SetDiagnostic(string.Empty);
                return;
            }

            if (ownedService == null || ownedService.Actions != targetActions)
            {
                ReleaseOwnedService();
                ownedService = new InputRebindingService(targetActions, store);
                ownedService.Load();
                service = ownedService;
            }

            ownedService.PairConnectedDevices();
            SetDiagnostic(string.Empty);
            return;
        }

        if (defaultActions == null)
        {
            ReleaseOwnedService();
            service = null;
            SetDiagnostic("Assign DefaultHeliAction to enable offline binding edits.");
            return;
        }

        if (fallbackActions == null)
        {
            ReleaseOwnedService();
            fallbackActions = Instantiate(defaultActions);
            fallbackActions.name = defaultActions.name + " (binding preview)";
            ownedService = new InputRebindingService(fallbackActions, store);
            ownedService.Load();
            service = ownedService;
        }

        SetDiagnostic("Flight controls are disconnected. Saved bindings will load for the local player.");
    }

    private void RebuildRows()
    {
        if (bindingRowPrefab == null || bindingRowsRoot == null)
        {
            SetDiagnostic("Assign a binding row prefab and row container.");
            return;
        }

        int used = 0;
        int usedHeadings = 0;
        string category = null;
        foreach (InputRebindingCommand command in InputRebindingCommands.All)
        {
            IReadOnlyList<InputBindingRow> bindings = service != null
                ? service.GetBindingRows(command.ActionName, selectedTab)
                : new InputBindingRow[0];
            if (bindings.Count == 0) continue;
            if (command.ActionName == "Yaw Input" && selectedTab == BindingDeviceTab.Joystick && yawModeRoot != null)
                yawModeRoot.transform.SetAsLastSibling();
            string nextCategory = GetCategory(command.ActionName);
            if (nextCategory != category && categoryHeadingPrefab != null)
            {
                if (usedHeadings == headings.Count) headings.Add(Instantiate(categoryHeadingPrefab, bindingRowsRoot));
                TMP_Text heading = headings[usedHeadings++];
                heading.text = nextCategory.ToUpperInvariant();
                heading.fontStyle |= FontStyles.Bold;
                heading.gameObject.SetActive(true);
                heading.transform.SetAsLastSibling();
            }
            category = nextCategory;
            bool displayedDirectJoystick = false;
            for (int index = 0; index < bindings.Count; index++)
            {
                InputBindingRow binding = bindings[index];
                bool compositePart = binding.IsAvailable && service.Actions.FindAction(command.ActionName)
                    .bindings[binding.BindingIndex].isPartOfComposite;
                bool groupedJoystick = selectedTab == BindingDeviceTab.Joystick && !compositePart;
                if (groupedJoystick && displayedDirectJoystick) continue;
                if (groupedJoystick) displayedDirectJoystick = true;

                if (used == rows.Count)
                {
                    rows.Add(Instantiate(bindingRowPrefab, bindingRowsRoot));
                }

                ControlsBindingRow row = rows[used++];
                row.gameObject.SetActive(true);
                if (groupedJoystick)
                    binding = new InputBindingRow(binding.ActionName, binding.BindingIndex, string.Empty,
                        service.GetJoystickBindingDisplay(command.ActionName), binding.Error);
                row.Configure(service, command, binding, selectedTab == BindingDeviceTab.KeyboardMouse && index == 0,
                    RefreshRows, OpenDialog, groupedJoystick);
                row.transform.SetAsLastSibling();
            }
        }
        for (int index = usedHeadings; index < headings.Count; index++) headings[index].gameObject.SetActive(false);

        for (int index = used; index < rows.Count; index++)
        {
            rows[index].gameObject.SetActive(false);
        }

        if (resetProfileButton != null)
        {
            resetProfileButton.interactable = service != null;
        }
    }

    private static string GetCategory(string actionName)
    {
        switch (actionName)
        {
            case "Pitch Input": case "Roll Input": case "Collective Lever": case "Throttle Lever":
            case "Yaw Input": case "Trim": return "Flight controls";
            case "Start Engine Global": case "Reset Helicopter": return "Aircraft";
            default: return "View and menu";
        }
    }

    private void SelectTab(BindingDeviceTab tab)
    {
        CloseDialog();
        selectedTab = tab;
        UpdateTabButtons();
        UpdateYawModeControls();
        RebuildRows();
    }

    private void UpdateTabButtons()
    {
        if (keyboardTabButton != null) keyboardTabButton.interactable = selectedTab != BindingDeviceTab.KeyboardMouse;
        if (joystickTabButton != null) joystickTabButton.interactable = selectedTab != BindingDeviceTab.Joystick;
    }

    private void UpdateYawModeControls()
    {
        if (yawModeRoot != null) yawModeRoot.SetActive(service != null && selectedTab == BindingDeviceTab.Joystick);
        if (yawModeToggle != null)
        {
            yawModeToggle.interactable = service != null;
            yawModeToggle.SetIsOnWithoutNotify(service != null && service.YawMode == YawBindingMode.Axis);
            yawModeRoot?.GetComponent<SettingsToggleVisual>()?.Refresh();
        }
    }

    private void OnYawModeChanged(bool axis) => SetYawMode(axis ? YawBindingMode.Axis : YawBindingMode.Buttons);

    private void SetYawMode(YawBindingMode mode)
    {
        if (service == null) return;
        CloseDialog();
        service.SetYawBindingMode(mode);
        UpdateYawModeControls();
        RebuildRows();
    }

    private void OpenDialog(ControlsBindingRow row)
    {
        if (service == null || row == null || bindingDialogRoot == null) return;
        selectedRow = row;
        if (dialogTitleLabel != null) dialogTitleLabel.text = row.GetCommandLabel();
        RefreshDialogBinding();
        if (dialogStatusLabel != null) dialogStatusLabel.text = "Press a control to assign it. Escape cancels.";
        bindingDialogRoot.SetActive(true);
        dialogOpenedFrame = Time.frameCount;
        service.TryStartRebind(row.ActionName, row.BindingIndex,
            error => { if (dialogStatusLabel != null) dialogStatusLabel.text = error; },
            () => { RefreshRows(); CloseDialog(); },
            () => CloseDialog(), AcceptRebindControl, row.IsGroupedJoystick);
    }

    private bool AcceptRebindControl(InputControl control)
    {
        if (control == null || Time.frameCount == dialogOpenedFrame) return false;
        if (!InputRebindingService.IsDeviceAllowedForTab(control.device, selectedTab)) return false;
        if (!(control.device is Mouse mouse)) return true;
        Vector2 pointer = mouse.position.ReadValue();
        return !PointerInsideButton(dialogCancelButton, pointer) &&
            !PointerInsideButton(dialogClearButton, pointer) &&
            !PointerInsideButton(dialogRestoreButton, pointer);
    }

    private static bool PointerInsideButton(Button button, Vector2 pointer)
    {
        RectTransform rect = button != null ? button.transform as RectTransform : null;
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, pointer);
    }

    private void RefreshDialogBinding()
    {
        if (dialogBindingLabel != null && selectedRow != null && service != null)
            dialogBindingLabel.text = "Current: " + (selectedRow.IsGroupedJoystick
                ? service.GetJoystickBindingDisplay(selectedRow.ActionName)
                : service.GetDisplayString(selectedRow.ActionName, selectedRow.BindingIndex));
    }

    private void CloseDialog()
    {
        if (closingDialog) return;
        closingDialog = true;
        if (IsBindingDialogOpen) dialogClosedFrame = Time.frameCount;
        if (bindingDialogRoot != null) bindingDialogRoot.SetActive(false);
        selectedRow = null;
        service?.CancelRebind();
        closingDialog = false;
    }

    private void ClearSelected()
    {
        if (selectedRow == null || service == null) return;
        bool cleared = selectedRow.IsGroupedJoystick
            ? service.TryClearJoystickBindings(selectedRow.ActionName, out string error)
            : service.TryClear(selectedRow.ActionName, selectedRow.BindingIndex, out error);
        if (cleared)
        {
            RefreshRows(); CloseDialog();
        }
        else if (dialogStatusLabel != null) dialogStatusLabel.text = error;
    }

    private void RestoreSelected()
    {
        if (selectedRow == null || service == null) return;
        bool restored = selectedRow.IsGroupedJoystick
            ? service.TryResetJoystickBindings(selectedRow.ActionName, out string error)
            : service.TryReset(selectedRow.ActionName, selectedRow.BindingIndex, out error);
        if (restored)
        {
            RefreshRows(); CloseDialog();
        }
        else if (dialogStatusLabel != null) dialogStatusLabel.text = error;
    }

    private void WirePresentation()
    {
        if (presentationWired) return;
        if (keyboardTabButton != null) keyboardTabButton.onClick.AddListener(ShowKeyboardTab);
        if (joystickTabButton != null) joystickTabButton.onClick.AddListener(ShowJoystickTab);
        if (yawModeToggle != null) yawModeToggle.onValueChanged.AddListener(OnYawModeChanged);
        if (dialogCancelButton != null) dialogCancelButton.onClick.AddListener(CloseDialog);
        if (dialogClearButton != null) dialogClearButton.onClick.AddListener(ClearSelected);
        if (dialogRestoreButton != null) dialogRestoreButton.onClick.AddListener(RestoreSelected);
        presentationWired = true;
    }

    private void UnwirePresentation()
    {
        if (!presentationWired) return;
        if (keyboardTabButton != null) keyboardTabButton.onClick.RemoveListener(ShowKeyboardTab);
        if (joystickTabButton != null) joystickTabButton.onClick.RemoveListener(ShowJoystickTab);
        if (yawModeToggle != null) yawModeToggle.onValueChanged.RemoveListener(OnYawModeChanged);
        if (dialogCancelButton != null) dialogCancelButton.onClick.RemoveListener(CloseDialog);
        if (dialogClearButton != null) dialogClearButton.onClick.RemoveListener(ClearSelected);
        if (dialogRestoreButton != null) dialogRestoreButton.onClick.RemoveListener(RestoreSelected);
        presentationWired = false;
    }

    private void ShowKeyboardTab() => SelectTab(BindingDeviceTab.KeyboardMouse);
    private void ShowJoystickTab() => SelectTab(BindingDeviceTab.Joystick);

    private void RefreshRows()
    {
        RebuildRows();
    }

    private void ResetProfile()
    {
        if (service == null)
        {
            SetDiagnostic("Input bindings are unavailable.");
            return;
        }

        service.ResetProfile();
        SetDiagnostic("Binding profile reset to defaults.");
        UpdateYawModeControls();
        RefreshRows();
    }

    private void RefreshHardwareSummary()
    {
        if (hardwareSummaryLabel == null)
        {
            return;
        }

        hardwareSummaryLabel.text = $"Connected input: keyboard {(Keyboard.current != null ? "available" : "unavailable")}, " +
            $"joysticks {Joystick.all.Count}, gamepads {Gamepad.all.Count}.";
    }

    private void WireResetButton()
    {
        if (wiredResetProfileButton == resetProfileButton)
        {
            return;
        }

        if (wiredResetProfileButton != null)
        {
            wiredResetProfileButton.onClick.RemoveListener(ResetProfile);
        }

        wiredResetProfileButton = resetProfileButton;
        if (wiredResetProfileButton != null)
        {
            wiredResetProfileButton.onClick.AddListener(ResetProfile);
        }
    }

    private void ReleaseOwnedService()
    {
        ownedService?.Dispose();
        ownedService = null;
        if (fallbackActions != null)
        {
            if (Application.isPlaying) Destroy(fallbackActions);
            else DestroyImmediate(fallbackActions);
            fallbackActions = null;
        }

        service = null;
    }

    private void SetDiagnostic(string message)
    {
        if (diagnosticLabel != null)
        {
            diagnosticLabel.text = message;
        }
    }
}
