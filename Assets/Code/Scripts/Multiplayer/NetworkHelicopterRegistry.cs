using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>Locally spawned helicopters, independent of the server client table.</summary>
public class NetworkHelicopterRegistry : NetworkBehaviour
{
    private static readonly List<NetworkHelicopterRegistry> helicopters = new List<NetworkHelicopterRegistry>();
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        helicopters.Clear();
        Changed = null;
    }

    public static void GetHelicopters(NetworkManager manager, List<NetworkHelicopterRegistry> result)
    {
        result.Clear();
        foreach (var helicopter in helicopters)
        {
            if (helicopter != null && helicopter.IsSpawned && helicopter.NetworkObject.IsPlayerObject && helicopter.NetworkManager == manager)
                result.Add(helicopter);
        }
        result.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
    }

    public override void OnNetworkSpawn()
    {
        if (!helicopters.Contains(this)) helicopters.Add(this);
        Changed?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        Unregister();
    }

    public override void OnDestroy()
    {
        Unregister();
        base.OnDestroy();
    }

    private void Unregister()
    {
        if (helicopters.Remove(this)) Changed?.Invoke();
    }
}
