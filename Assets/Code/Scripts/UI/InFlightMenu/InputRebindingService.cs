using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

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
    private readonly InputActionAsset actions;
    private readonly IBindingOverrideStore store;
    private InputActionRebindingExtensions.RebindingOperation operation;

    public InputRebindingService(InputActionAsset actions, IBindingOverrideStore store)
    {
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public InputActionAsset Actions => actions;

    public IReadOnlyList<InputBindingRow> GetBindingRows(string actionName)
    {
        List<InputBindingRow> rows = new List<InputBindingRow>();
        InputAction action = actions.FindAction(actionName, false);
        if (action == null)
        {
            rows.Add(new InputBindingRow(actionName, -1, string.Empty, "Unavailable", "This command has no Input System action yet."));
            return rows;
        }

        for (int index = 0; index < action.bindings.Count; index++)
        {
            InputBinding binding = action.bindings[index];
            if (binding.isComposite)
            {
                continue;
            }

            string name = binding.isPartOfComposite ? binding.name : "Primary " + (rows.Count + 1);
            rows.Add(new InputBindingRow(actionName, index, name, GetDisplayString(actionName, index), null));
        }

        if (rows.Count == 0)
        {
            rows.Add(new InputBindingRow(actionName, -1, string.Empty, "Unavailable", "This command has no bindings yet."));
        }

        return rows;
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
    }

    public bool TryStartRebind(string actionName, int bindingIndex, Action<string> onError, Action onChanged)
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

        operation = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .OnApplyBinding((rebind, path) =>
            {
                if (TryApplyBindingPath(actionName, bindingIndex, path, rebind.selectedControl, out string applyError))
                {
                    onChanged?.Invoke();
                }
                else
                {
                    onError?.Invoke(applyError);
                }
            })
            .OnComplete(rebind => FinishRebind(action, wasEnabled))
            .OnCancel(rebind => FinishRebind(action, wasEnabled));
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

        return string.IsNullOrEmpty(binding.effectivePath) ? "Unbound" : action.GetBindingDisplayString(bindingIndex);
    }

    public void Dispose()
    {
        CancelRebind();
        operation?.Dispose();
        operation = null;
    }

    private bool TryApplyBindingPath(string actionName, int bindingIndex, string path, InputControl control, out string error)
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
        Save();
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
