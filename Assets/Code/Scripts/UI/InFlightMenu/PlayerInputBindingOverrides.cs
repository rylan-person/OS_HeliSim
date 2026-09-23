using System.Collections;
using Unity.Netcode;
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
        // PlayerInput paired devices before these saved overrides made new devices usable.
        StartCoroutine(PairLocalDevicesWhenReady());
    }

    private IEnumerator PairLocalDevicesWhenReady()
    {
        NetworkObject networkObject = GetComponentInParent<NetworkObject>();
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkObject != null && networkManager != null && networkManager.IsListening)
        {
            while (networkObject != null && !networkObject.IsSpawned)
                yield return null;

            if (networkObject == null || !networkObject.IsOwner)
                yield break;
        }

        service?.PairConnectedDevices();
    }

    private void OnDestroy()
    {
        service?.Dispose();
    }
}

