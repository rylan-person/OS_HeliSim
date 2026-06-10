using Oyedoyin.RotaryWing;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.Netcode;
using UnityEngine;

public class HelicopterComponents : MonoBehaviour
{
    public static HelicopterComponents Instance { get; private set; }

    private void Awake()
    {
        var networkObject = GetComponentInParent<NetworkObject>();

        if (networkObject != null)
        {
            StartCoroutine(InitializeWhenOwnershipIsReady(networkObject));
            return;
        }

        TryBecomeSingleton();
    }

    private IEnumerator InitializeWhenOwnershipIsReady(NetworkObject networkObject)
    {
        while (networkObject != null && (NetworkManager.Singleton == null || !networkObject.IsSpawned))
        {
            yield return null;
        }

        if (networkObject == null)
        {
            yield break;
        }

        if (!networkObject.IsOwner)
        {
            Debug.Log($"Disabling HelicopterComponents on '{gameObject.name}' because it is not owned by the local client.");
            enabled = false;
            yield break;
        }

        TryBecomeSingleton();
    }

    private void TryBecomeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log($"Disabling HelicopterComponents on '{gameObject.name}' because another instance already exists.");
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public RotaryComputer rotaryComputer;
    public RotaryController rotaryController;
    public Rigidbody helicopterRb;
    public GameObject helicopter;
    public AutoTrim autoTrim;
    public Transform helicopterTransform;
    public Transform mainRotor;
    public Transform tailRotor;
    public TMPro.TextMeshProUGUI timeRemainingTextInternal;
}
