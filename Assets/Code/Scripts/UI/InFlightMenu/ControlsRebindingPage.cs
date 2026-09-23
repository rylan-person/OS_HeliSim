using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ControlsRebindingPage : InFlightMenuPage
{
    [SerializeField] private InputActionAsset defaultActions;
    [SerializeField] private ControlsBindingRow bindingRowPrefab;
    [SerializeField] private Transform bindingRowsRoot;
    [SerializeField] private Button resetProfileButton;
    [SerializeField] private TMP_Text diagnosticLabel;
    [SerializeField] private TMP_Text hardwareSummaryLabel;

    private readonly List<ControlsBindingRow> rows = new List<ControlsBindingRow>();
    private InputRebindingService service;
    private InputRebindingService ownedService;
    private InputActionAsset fallbackActions;
    private IBindingOverrideStore store = new PlayerPrefsBindingOverrideStore();
    private Button wiredResetProfileButton;
    private bool injectedServiceForTest;

    public InputRebindingService Service => service;

    private void OnEnable() => WireResetButton();

    private void OnDestroy()
    {
        if (wiredResetProfileButton != null)
        {
            wiredResetProfileButton.onClick.RemoveListener(ResetProfile);
        }

        ReleaseOwnedService();
    }

    public override void OnPageSelected()
    {
        WireResetButton();
        ResolveService();
        RebuildRows();
        RefreshHardwareSummary();
    }

    public override void OnPageDeselected()
    {
        service?.CancelRebind();
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
        foreach (InputRebindingCommand command in InputRebindingCommands.All)
        {
            IReadOnlyList<InputBindingRow> bindings = service != null
                ? service.GetBindingRows(command.ActionName)
                : new[] { new InputBindingRow(command.ActionName, -1, string.Empty, "Unavailable", "Input bindings are unavailable.") };
            for (int index = 0; index < bindings.Count; index++)
            {
                if (used == rows.Count)
                {
                    rows.Add(Instantiate(bindingRowPrefab, bindingRowsRoot));
                }

                ControlsBindingRow row = rows[used++];
                row.gameObject.SetActive(true);
                row.Configure(service, command, bindings[index], index == 0, RefreshRows);
            }
        }

        for (int index = used; index < rows.Count; index++)
        {
            rows[index].gameObject.SetActive(false);
        }

        if (resetProfileButton != null)
        {
            resetProfileButton.interactable = service != null;
        }
    }

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
