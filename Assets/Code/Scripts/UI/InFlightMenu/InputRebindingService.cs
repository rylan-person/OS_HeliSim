using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Users;

public interface IBindingOverrideStore
{
    string Load();
    void Save(string json);
    void Clear();
}

public sealed class PlayerPrefsBindingOverrideStore : IBindingOverrideStore
{
    public const string Key = "HeliSim.InputBindingOverrides.v1";

    public string Load() => PlayerPrefs.GetString(Key, string.Empty);

    public void Save(string json)
    {
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();
    }

    public void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}

public readonly struct InputBindingRow
{
    public readonly string ActionName;
    public readonly int BindingIndex;
    public readonly string BindingName;
    public readonly string Display;
    public readonly string Error;

    public InputBindingRow(string actionName, int bindingIndex, string bindingName, string display, string error)
    {
        ActionName = actionName;
        BindingIndex = bindingIndex;
        BindingName = bindingName;
        Display = display;
        Error = error;
    }

    public bool IsAvailable => BindingIndex >= 0;
}

public enum YawBindingMode
{
    Buttons,
    Axis,
}

public interface IYawBindingModeStore
{
    YawBindingMode Load();
    void Save(YawBindingMode mode);
    void Clear();
}

public sealed class PlayerPrefsYawBindingModeStore : IYawBindingModeStore
{
    public const string Key = "HeliSim.YawBindingMode.v1";

    public YawBindingMode Load() => PlayerPrefs.GetInt(Key, 0) == 1 ? YawBindingMode.Axis : YawBindingMode.Buttons;

    public void Save(YawBindingMode mode)
    {
        PlayerPrefs.SetInt(Key, mode == YawBindingMode.Axis ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}

public enum BindingDeviceTab
{
    KeyboardMouse,
    Joystick,
}

public readonly struct InputRebindingCommand
{
    public readonly string Label;
    public readonly string ActionName;

    public InputRebindingCommand(string label, string actionName)
    {
        Label = label;
        ActionName = actionName;
    }
}

public static class InputRebindingCommands
{
    public static readonly IReadOnlyList<InputRebindingCommand> All = new[]
    {
        new InputRebindingCommand("Cyclic pitch", "Pitch Input"),
        new InputRebindingCommand("Cyclic roll", "Roll Input"),
        new InputRebindingCommand("Collective", "Collective Lever"),
        new InputRebindingCommand("Throttle", "Throttle Lever"),
        new InputRebindingCommand("Pedals / yaw", "Yaw Input"),
        new InputRebindingCommand("Trim", "Trim"),
        new InputRebindingCommand("Engine start", "Start Engine Global"),
        new InputRebindingCommand("Reset aircraft", "Reset Helicopter"),
        new InputRebindingCommand("Pause / menu", "Pause Menu"),
        new InputRebindingCommand("Map", "Toggle Map"),
        new InputRebindingCommand("VR recenter", "Reset VR"),
        new InputRebindingCommand("Cycle camera", "Cycle Camera"),
        new InputRebindingCommand("Camera orbit", "Camera Orbit"),
    };
}

/// <summary>Changes bindings on a local PlayerInput action asset and persists Input System overrides.</summary>
public sealed class InputRebindingService : IDisposable
{
    private const string YawActionName = "Yaw Input";
    private const string YawButtonsGroup = "YawButtons";
    private const string YawAxisGroup = "YawAxis";
    private readonly InputActionAsset actions;
    private readonly IBindingOverrideStore store;
    private readonly IYawBindingModeStore yawModeStore;
    private InputActionRebindingExtensions.RebindingOperation operation;

    public InputRebindingService(InputActionAsset actions, IBindingOverrideStore store,
        IYawBindingModeStore yawModeStore = null)
    {
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.yawModeStore = yawModeStore ?? new PlayerPrefsYawBindingModeStore();
        ApplyYawBindingMode(this.yawModeStore.Load());
    }

    public InputActionAsset Actions => actions;
    public YawBindingMode YawMode { get; private set; }

    public void SetYawBindingMode(YawBindingMode mode)
    {
        ApplyYawBindingMode(mode);
        yawModeStore.Save(mode);
    }

    private void ApplyYawBindingMode(YawBindingMode mode)
    {
        YawMode = mode == YawBindingMode.Axis ? YawBindingMode.Axis : YawBindingMode.Buttons;
        InputAction yaw = actions.FindAction(YawActionName, false);
        if (yaw != null && HasYawModeBindings(yaw))
            yaw.bindingMask = InputBinding.MaskByGroup(YawMode == YawBindingMode.Axis ? YawAxisGroup : YawButtonsGroup);
    }

    private static bool HasYawModeBindings(InputAction action)
    {
        foreach (InputBinding binding in action.bindings)
            if (BindingHasGroup(binding, YawAxisGroup)) return true;
        return false;
    }

    private static bool BindingHasGroup(InputBinding binding, string group)
    {
        if (string.IsNullOrEmpty(binding.groups)) return false;
        foreach (string candidate in binding.groups.Split(InputBinding.Separator))
            if (string.Equals(candidate, group, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool IsDeviceAllowedForTab(InputDevice device, BindingDeviceTab tab)
    {
        if (device == null) return false;
        bool keyboardOrMouse = device is Keyboard || device is Mouse;
        return tab == BindingDeviceTab.KeyboardMouse ? keyboardOrMouse : !keyboardOrMouse;
    }

    public IReadOnlyList<InputBindingRow> GetBindingRows(string actionName)
    {
        return GetBindingRowsInternal(actionName, null);
    }

    public IReadOnlyList<InputBindingRow> GetBindingRows(string actionName, BindingDeviceTab deviceTab)
    {
        return GetBindingRowsInternal(actionName, deviceTab);
    }

    private IReadOnlyList<InputBindingRow> GetBindingRowsInternal(string actionName, BindingDeviceTab? deviceTab)
    {
        List<InputBindingRow> rows = new List<InputBindingRow>();
        InputAction action = actions.FindAction(actionName, false);
        if (action == null)
        {
            if (!deviceTab.HasValue || deviceTab.Value == BindingDeviceTab.KeyboardMouse)
                rows.Add(new InputBindingRow(actionName, -1, string.Empty, "Unavailable", "This command has no Input System action yet."));
            return rows;
        }

        int bindingOrdinal = 0;
        for (int index = 0; index < action.bindings.Count; index++)
        {
            InputBinding binding = action.bindings[index];
            if (binding.isComposite)
            {
                continue;
            }

            bindingOrdinal++;
            if (deviceTab.HasValue && GetOriginalDeviceTab(binding.path) != deviceTab.Value) continue;
            if (deviceTab == BindingDeviceTab.Joystick && actionName == YawActionName &&
                !BindingHasGroup(binding, YawMode == YawBindingMode.Axis ? YawAxisGroup : YawButtonsGroup)) continue;

            string name = binding.isPartOfComposite ? binding.name : "Primary " + bindingOrdinal;
            if (actionName == YawActionName && deviceTab == BindingDeviceTab.Joystick &&
                YawMode == YawBindingMode.Buttons && binding.isPartOfComposite)
                name = binding.name == "negative" ? "Left" : binding.name == "positive" ? "Right" : name;
            rows.Add(new InputBindingRow(actionName, index, name, GetDisplayString(actionName, index), null));
        }

        if (rows.Count == 0 && !deviceTab.HasValue)
        {
            rows.Add(new InputBindingRow(actionName, -1, string.Empty, "Unavailable", "This command has no bindings yet."));
        }

        return rows;
    }

    private static BindingDeviceTab GetOriginalDeviceTab(string path)
    {
        return !string.IsNullOrEmpty(path) &&
            (path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase))
            ? BindingDeviceTab.KeyboardMouse : BindingDeviceTab.Joystick;
    }

    public string GetPrimaryBindingDisplay(string actionName)
    {
        IReadOnlyList<InputBindingRow> rows = GetBindingRows(actionName);
        InputAction action = actions.FindAction(actionName, false);
        if (action != null)
        {
            for (int index = 0; index < action.bindings.Count; index++)
            {
                if (!action.bindings[index].isComposite)
                {
                    continue;
                }

                List<string> parts = new List<string>();
                for (int part = index + 1; part < action.bindings.Count && action.bindings[part].isPartOfComposite; part++)
                {
                    if (IsKeyboardPath(action.bindings[part].effectivePath))
                    {
                        parts.Add(action.bindings[part].name + ": " + GetDisplayString(actionName, part));
                    }
                }

                if (parts.Count >= 2)
                {
                    return string.Join(" / ", parts);
                }
            }

            foreach (InputBindingRow row in rows)
            {
                if (row.IsAvailable && IsKeyboardPath(action.bindings[row.BindingIndex].effectivePath))
                {
                    return row.Display;
                }
            }
        }

        return rows[0].Display;
    }

    public void Load()
    {
        string json = store.Load();
        actions.RemoveAllBindingOverrides();
        ApplyYawBindingMode(yawModeStore.Load());
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            actions.LoadBindingOverridesFromJson(json);
            RestoreKeyboardFallbacks();
        }
        catch (Exception exception)
        {
            actions.RemoveAllBindingOverrides();
            Debug.LogWarning("Input binding overrides could not be loaded: " + exception.Message);
        }
    }

    public bool TryApplyBindingPath(string actionName, int bindingIndex, string path, out string error)
    {
        return TryApplyBindingPath(actionName, bindingIndex, path, null, out error);
    }

    public void PairConnectedDevices(InputDevice selectedDevice = null)
    {
        foreach (InputUser user in InputUser.all)
        {
            if (!user.valid || !ReferenceEquals(user.actions, actions)) continue;

            if (selectedDevice != null)
            {
                PairUsableDevice(user, selectedDevice);
                continue;
            }

            using (var unpairedDevices = InputUser.GetUnpairedInputDevices())
                foreach (InputDevice device in unpairedDevices)
                    PairUsableDevice(user, device);
        }
    }

    private void PairUsableDevice(InputUser user, InputDevice device)
    {
        foreach (InputDevice paired in user.pairedDevices)
            if (paired == device) return;

        foreach (InputActionMap map in actions.actionMaps)
        {
            if (!map.IsUsableWithDevice(device)) continue;
            InputUser.PerformPairingWithDevice(device, user);
            return;
        }
    }

    public string GetJoystickBindingDisplay(string actionName)
    {
        InputAction action = actions.FindAction(actionName, false);
        if (action == null) return "Unavailable";

        int count = 0;
        int lastIndex = -1;
        bool factoryBindings = true;
        List<string> deviceNames = new List<string>();
        foreach (int index in GetJoystickBindingIndices(action))
        {
            if (string.IsNullOrEmpty(action.bindings[index].effectivePath)) continue;
            count++;
            lastIndex = index;
            factoryBindings &= string.IsNullOrEmpty(action.bindings[index].overridePath);
            string deviceName = BindingDeviceDisplayFormatter.FormatPath(action.bindings[index].effectivePath);
            if (!string.IsNullOrEmpty(deviceName) && !deviceNames.Contains(deviceName)) deviceNames.Add(deviceName);
        }

        if (count == 0) return "Unbound";
        if (count == 1) return GetDisplayString(actionName, lastIndex);
        string summary = factoryBindings ? "Factory controls" : "Multiple controls";
        return deviceNames.Count == 0 ? summary : summary + " — " + string.Join(", ", deviceNames);
    }

    public bool TryApplyJoystickBindingPath(string actionName, int bindingIndex, string path, out string error)
    {
        return TryApplyJoystickBindingPath(actionName, bindingIndex, path, null, out error);
    }

    public bool TryClearJoystickBindings(string actionName, out string error)
    {
        if (!TryGetJoystickBindings(actionName, out InputAction action, out List<int> indices, out error)) return false;
        foreach (int index in indices) action.ApplyBindingOverride(index, string.Empty);
        Save();
        return true;
    }

    public bool TryResetJoystickBindings(string actionName, out string error)
    {
        if (!TryGetJoystickBindings(actionName, out InputAction action, out List<int> indices, out error)) return false;
        foreach (int index in indices) action.RemoveBindingOverride(index);
        Save();
        return true;
    }

    public bool TryClear(string actionName, int bindingIndex, out string error)
    {
        if (!TryGetBinding(actionName, bindingIndex, out InputAction action, out InputBinding binding, out error))
        {
            return false;
        }

        if (binding.isComposite)
        {
            error = "Select an individual axis direction to clear.";
            return false;
        }

        if (WouldRemoveLastKeyboardBinding(action, bindingIndex, string.Empty))
        {
            error = "Keep a keyboard binding for this control.";
            return false;
        }

        action.ApplyBindingOverride(bindingIndex, string.Empty);
        Save();
        error = null;
        return true;
    }

    public bool TryReset(string actionName, int bindingIndex, out string error)
    {
        if (!TryGetBinding(actionName, bindingIndex, out InputAction action, out InputBinding binding, out error))
        {
            return false;
        }

        if (binding.isComposite)
        {
            for (int index = bindingIndex + 1; index < action.bindings.Count && action.bindings[index].isPartOfComposite; index++)
            {
                action.RemoveBindingOverride(index);
            }
        }
        else
        {
            action.RemoveBindingOverride(bindingIndex);
        }

        Save();
        error = null;
        return true;
    }

    public void ResetProfile()
    {
        CancelRebind();
        actions.RemoveAllBindingOverrides();
        store.Clear();
        yawModeStore.Clear();
        ApplyYawBindingMode(YawBindingMode.Buttons);
    }

    public bool TryStartRebind(string actionName, int bindingIndex, Action<string> onError, Action onChanged,
        Action onCancelled = null, Func<InputControl, bool> acceptControl = null, bool replaceJoystickBindings = false)
    {
        if (!TryGetBinding(actionName, bindingIndex, out InputAction action, out InputBinding binding, out string error))
        {
            onError?.Invoke(error);
            return false;
        }

        if (binding.isComposite)
        {
            onError?.Invoke("Select an individual axis direction to rebind.");
            return false;
        }

        CancelRebind();
        bool wasEnabled = action.enabled;
        if (wasEnabled)
        {
            action.Disable();
        }

        bool bindingChanged = false;
        operation = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .OnApplyBinding((rebind, path) =>
            {
                bool applied = replaceJoystickBindings
                    ? TryApplyJoystickBindingPath(actionName, bindingIndex, path, rebind.selectedControl, out string applyError)
                    : TryApplyBindingPath(actionName, bindingIndex, path, rebind.selectedControl, out applyError);
                if (applied)
                {
                    bindingChanged = true;
                }
                else
                {
                    onError?.Invoke(applyError);
                }
            })
            .OnComplete(rebind => { FinishRebind(action, wasEnabled); if (bindingChanged) onChanged?.Invoke(); })
            .OnCancel(rebind => { FinishRebind(action, wasEnabled); onCancelled?.Invoke(); });
        if (acceptControl != null)
        {
            // UI pointer events must reach the dialog buttons even while listening.
            operation.WithMatchingEventsBeingSuppressed(false)
                .OnPotentialMatch(rebind =>
                {
                    for (int index = rebind.candidates.Count - 1; index >= 0; index--)
                    {
                        InputControl candidate = rebind.candidates[index];
                        if (!acceptControl(candidate)) rebind.RemoveCandidate(candidate);
                    }

                    if (rebind.candidates.Count > 0) rebind.Complete();
                });
        }
        operation.Start();
        return true;
    }

    public void CancelRebind()
    {
        operation?.Cancel();
    }

    public string GetDisplayString(string actionName, int bindingIndex)
    {
        if (!TryGetBinding(actionName, bindingIndex, out InputAction action, out InputBinding binding, out _))
        {
            return "Unavailable";
        }

        if (binding.isComposite)
        {
            return action.GetBindingDisplayString(bindingIndex);
        }

        if (string.IsNullOrEmpty(binding.effectivePath)) return "Unbound";
        string device = BindingDeviceDisplayFormatter.FormatPath(binding.effectivePath);
        string control = action.GetBindingDisplayString(bindingIndex);
        return string.IsNullOrEmpty(device) ? control : control + " — " + device;
    }

    public void Dispose()
    {
        CancelRebind();
        operation?.Dispose();
        operation = null;
    }

    private bool TryApplyJoystickBindingPath(string actionName, int bindingIndex, string path,
        InputControl control, out string error)
    {
        if (!TryGetJoystickBindings(actionName, out InputAction action, out List<int> indices, out error)) return false;
        if (!indices.Contains(bindingIndex))
        {
            error = "Select a joystick binding to reassign.";
            return false;
        }

        if (!TryApplyBindingPath(actionName, bindingIndex, path, control, out error, false)) return false;
        foreach (int index in indices)
        {
            if (index != bindingIndex) action.ApplyBindingOverride(index, string.Empty);
        }
        Save();
        PairConnectedDevices(control?.device);
        return true;
    }

    private bool TryGetJoystickBindings(string actionName, out InputAction action, out List<int> indices, out string error)
    {
        action = actions.FindAction(actionName, false);
        indices = action != null ? GetJoystickBindingIndices(action) : new List<int>();
        if (indices.Count > 0)
        {
            error = null;
            return true;
        }

        error = action == null ? "This command has no Input System action yet." : "This command has no joystick binding.";
        return false;
    }

    private List<int> GetJoystickBindingIndices(InputAction action)
    {
        List<int> indices = new List<int>();
        for (int index = 0; index < action.bindings.Count; index++)
        {
            InputBinding binding = action.bindings[index];
            if (!binding.isComposite && !binding.isPartOfComposite &&
                GetOriginalDeviceTab(binding.path) == BindingDeviceTab.Joystick &&
                (action.name != YawActionName || BindingHasGroup(binding,
                    YawMode == YawBindingMode.Axis ? YawAxisGroup : YawButtonsGroup)))
                indices.Add(index);
        }
        return indices;
    }

    private bool TryApplyBindingPath(string actionName, int bindingIndex, string path, InputControl control,
        out string error, bool saveOverride = true)
    {
        if (!TryGetBinding(actionName, bindingIndex, out InputAction action, out InputBinding binding, out error))
        {
            return false;
        }

        if (binding.isComposite)
        {
            error = "Select an individual axis direction to rebind.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "No input control was selected.";
            return false;
        }

        if (IsEscapePath(path) && !IsEscapePath(binding.path))
        {
            error = "Escape is reserved for menu recovery.";
            return false;
        }

        bool needsButton = binding.isPartOfComposite || action.expectedControlType == "Button" || action.type == InputActionType.Button;
        bool compatible = control != null
            ? needsButton ? control is ButtonControl : control is AxisControl && !(control is ButtonControl)
            : IsCompatiblePath(path, needsButton);
        if (!compatible)
        {
            error = needsButton ? "Choose a button or key." : "Choose an axis control.";
            return false;
        }

        if (WouldRemoveLastKeyboardBinding(action, bindingIndex, path))
        {
            error = "Keep a keyboard binding for this control.";
            return false;
        }

        action.ApplyBindingOverride(bindingIndex, path);
        if (saveOverride)
        {
            Save();
            PairConnectedDevices(control?.device);
        }
        error = null;
        return true;
    }

    private static bool IsCompatiblePath(string path, bool needsButton)
    {
        string layout = InputControlPath.TryGetControlLayout(path);
        if (string.IsNullOrEmpty(layout))
        {
            return false;
        }

        return needsButton
            ? InputSystem.IsFirstLayoutBasedOnSecond(layout, "Button")
            : InputSystem.IsFirstLayoutBasedOnSecond(layout, "Axis") && !InputSystem.IsFirstLayoutBasedOnSecond(layout, "Button");
    }

    private static bool WouldRemoveLastKeyboardBinding(InputAction action, int bindingIndex, string replacement)
    {
        InputBinding target = action.bindings[bindingIndex];
        if (target.isPartOfComposite && IsKeyboardPath(target.path) && !IsKeyboardPath(replacement))
        {
            return true;
        }

        if (IsEscapePath(target.path) && IsEscapePath(target.effectivePath) && !IsEscapePath(replacement))
        {
            return true;
        }

        if (!IsKeyboardPath(target.path) || !IsKeyboardPath(target.effectivePath) || IsKeyboardPath(replacement))
        {
            return false;
        }

        for (int index = 0; index < action.bindings.Count; index++)
        {
            if (index != bindingIndex && IsKeyboardPath(action.bindings[index].effectivePath))
            {
                return false;
            }
        }

        return true;
    }

    private void RestoreKeyboardFallbacks()
    {
        foreach (InputActionMap map in actions.actionMaps)
        {
            foreach (InputAction action in map.actions)
            {
                for (int index = 0; index < action.bindings.Count; index++)
                {
                    InputBinding binding = action.bindings[index];
                    if (binding.isPartOfComposite && IsKeyboardPath(binding.path) && !IsKeyboardPath(binding.effectivePath))
                    {
                        action.RemoveBindingOverride(index);
                    }
                    else if (IsEscapePath(binding.path) && !IsEscapePath(binding.effectivePath))
                    {
                        action.RemoveBindingOverride(index);
                    }
                }

                bool hasKeyboard = false;
                for (int index = 0; index < action.bindings.Count; index++)
                {
                    hasKeyboard |= IsKeyboardPath(action.bindings[index].effectivePath);
                }

                if (hasKeyboard)
                {
                    continue;
                }

                for (int index = 0; index < action.bindings.Count; index++)
                {
                    if (IsEscapePath(action.bindings[index].path) || IsKeyboardPath(action.bindings[index].path))
                    {
                        action.RemoveBindingOverride(index);
                    }
                }
            }
        }
    }

    private static bool IsKeyboardPath(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEscapePath(string path)
    {
        return string.Equals(path, "<Keyboard>/escape", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetBinding(string actionName, int bindingIndex, out InputAction action, out InputBinding binding, out string error)
    {
        action = actions.FindAction(actionName, false);
        binding = default;
        if (action == null)
        {
            error = "This command has no Input System action yet.";
            return false;
        }

        if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            error = "This command has no binding at that position.";
            return false;
        }

        binding = action.bindings[bindingIndex];
        error = null;
        return true;
    }

    private void Save()
    {
        store.Save(actions.SaveBindingOverridesAsJson());
    }

    private void FinishRebind(InputAction action, bool wasEnabled)
    {
        operation?.Dispose();
        operation = null;
        if (wasEnabled)
        {
            action.Enable();
        }
    }
}
