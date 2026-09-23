using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public sealed class PlayerInputBindingOverrides : MonoBehaviour
{
    private InputRebindingService service;

    public InputRebindingService Service => service;

    private void Start()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogWarning("Input binding overrides require a local PlayerInput action asset.", this);
            return;
        }

        service = new InputRebindingService(playerInput.actions, new PlayerPrefsBindingOverrideStore());
        service.Load();
    }

    private void OnDestroy()
    {
        service?.Dispose();
    }
}

