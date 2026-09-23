using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Routes rebindable presentation and session commands to their existing owners.</summary>
public sealed class InFlightInputCommands : MonoBehaviour
{
    [SerializeField] private PauseMenuController menu;
    [SerializeField] private MainMenu mainMenu;

    private PlayerInput boundPlayer;
    private InputAction trim;
    private InputAction engineStart;
    private InputAction resetAircraft;
    private InputAction pauseMenu;
    private InputAction toggleMap;
    private InputAction resetVr;
    private InputAction cycleCamera;
    private float nextProbe;

    private void OnEnable()
    {
        if (menu == null) menu = GetComponentInChildren<PauseMenuController>(true);
        if (mainMenu == null) mainMenu = GetComponentInChildren<MainMenu>(true);
        TryBindLocalPlayer();
    }

    private void Update()
    {
        if (boundPlayer != null && boundPlayer.actions != null) return;
        if (Time.unscaledTime < nextProbe) return;
        nextProbe = Time.unscaledTime + 1f;
        TryBindLocalPlayer();
    }

    private void OnDisable() => Unbind();

    private void TryBindLocalPlayer()
    {
        HelicopterComponents components = HelicopterComponents.Instance;
        PlayerInput playerInput = components != null ? components.GetComponentInParent<PlayerInput>() : null;
        if (playerInput == null || playerInput.actions == null) return;
        if (boundPlayer == playerInput) return;

        Unbind();
        boundPlayer = playerInput;
        InputActionAsset actions = playerInput.actions;
        trim = Bind(actions, "Trim", OnTrim);
        engineStart = Bind(actions, "Start Engine Global", OnEngineStart);
        resetAircraft = Bind(actions, "Reset Helicopter", OnResetAircraft);
        pauseMenu = Bind(actions, "Pause Menu", OnPauseMenu);
        toggleMap = Bind(actions, "Toggle Map", OnToggleMap);
        resetVr = Bind(actions, "Reset VR", OnResetVr);
        cycleCamera = Bind(actions, "Cycle Camera", OnCycleCamera);
    }

    private static InputAction Bind(InputActionAsset actions, string name,
        System.Action<InputAction.CallbackContext> callback)
    {
        InputAction action = actions.FindAction(name, false);
        if (action != null) action.performed += callback;
        return action;
    }

    private void Unbind()
    {
        if (trim != null) trim.performed -= OnTrim;
        if (engineStart != null) engineStart.performed -= OnEngineStart;
        if (resetAircraft != null) resetAircraft.performed -= OnResetAircraft;
        if (pauseMenu != null) pauseMenu.performed -= OnPauseMenu;
        if (toggleMap != null) toggleMap.performed -= OnToggleMap;
        if (resetVr != null) resetVr.performed -= OnResetVr;
        if (cycleCamera != null) cycleCamera.performed -= OnCycleCamera;
        trim = engineStart = resetAircraft = pauseMenu = toggleMap = resetVr = cycleCamera = null;
        boundPlayer = null;
    }

    private void OnTrim(InputAction.CallbackContext context)
    {
        if (menu != null && menu.IsOpen) return;
        if (mainMenu != null) mainMenu.ToggleAutoTrimFromInput();
    }

    private void OnEngineStart(InputAction.CallbackContext context)
    {
        if (menu != null && menu.IsOpen) return;
        if (mainMenu != null) mainMenu.startEngine();
    }

    private void OnResetAircraft(InputAction.CallbackContext context)
    {
        if (menu != null && menu.IsOpen) return;
        if (mainMenu != null) mainMenu.PlayerResetHelicopter();
    }

    private void OnPauseMenu(InputAction.CallbackContext context)
    {
        if (menu != null) menu.Toggle();
    }

    private void OnToggleMap(InputAction.CallbackContext context)
    {
        if (menu != null && menu.IsOpen) return;
        if (mainMenu != null) mainMenu.ToggleOfflineMapFromInput();
    }

    private void OnResetVr(InputAction.CallbackContext context)
    {
        if (menu != null && menu.IsOpen) return;
        if (mainMenu != null) mainMenu.ResetVROrientation();
    }

    private void OnCycleCamera(InputAction.CallbackContext context)
    {
        if (menu != null && menu.IsOpen) return;
        CameraSwitcher switcher = CameraSwitcher.Instance;
        if (switcher == null || switcher.cameraDropdown == null || switcher.cameraDropdown.options.Count == 0) return;
        int next = (switcher.cameraDropdown.value + 1) % switcher.cameraDropdown.options.Count;
        switcher.cameraDropdown.SetValueWithoutNotify(next);
        switcher.OnCameraSelected(next);
    }
}
