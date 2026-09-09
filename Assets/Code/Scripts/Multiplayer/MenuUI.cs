using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if NEW_INPUT_SYSTEM_INSTALLED
using UnityEngine.InputSystem.UI;
#endif

public enum NetworkPrefabsList
{
    PlayerPrefab,
    SpectatorPrefab
}

public class MenuUI : MonoBehaviour
{
    [SerializeField]
    Button m_StartHostButton;
    [SerializeField]
    Button m_StartClientButton;
    [SerializeField]
    NetworkPrefabsList networkPrefabsList;

    [Header("Connection Settings")]
    [Tooltip("Address the client connects to. Override in the inspector, or via SetServerAddress, for anything other than local testing.")]
    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private ushort serverPort = 7777;

    public List<GameObject> networkPrefabs = new List<GameObject>();

    void Awake()
    {
        if (!FindAnyObjectByType<EventSystem>())
        {
            var inputType = typeof(StandaloneInputModule);
#if ENABLE_INPUT_SYSTEM && NEW_INPUT_SYSTEM_INSTALLED
            inputType = typeof(InputSystemUIInputModule);                
#endif
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), inputType);
            eventSystem.transform.SetParent(transform);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        m_StartHostButton.onClick.AddListener(StartHost);
        m_StartClientButton.onClick.AddListener(StartClient);
    }

    void OnDestroy()
    {
        if (m_StartHostButton != null) m_StartHostButton.onClick.RemoveListener(StartHost);
        if (m_StartClientButton != null) m_StartClientButton.onClick.RemoveListener(StartClient);
    }

    /// <summary>Allows other UI (e.g. a server-address input field) to override the default before connecting.</summary>
    public void SetServerAddress(string address)
    {
        serverAddress = address;
    }

    void StartClient()
    {
        Debug.Log("StartClient called. Attempting to connect to server...");
        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        transport.ConnectionData.Address = serverAddress;
        transport.ConnectionData.Port = serverPort;
        NetworkManager.Singleton.StartClient();
        DeactivateButtons();
    }

    void StartHost()
    {
        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        transport.ConnectionData.Port = serverPort;

        int prefabIndex = (int)networkPrefabsList;
        if (networkPrefabs == null || prefabIndex < 0 || prefabIndex >= networkPrefabs.Count)
        {
            Debug.LogError($"{nameof(MenuUI)}: networkPrefabsList index {prefabIndex} is out of range of networkPrefabs (count {networkPrefabs?.Count ?? 0}).");
            return;
        }

        NetworkManager.Singleton.NetworkConfig.PlayerPrefab = networkPrefabs[prefabIndex];
        NetworkManager.Singleton.StartHost();
        DeactivateButtons();
    }

    void DeactivateButtons()
    {
        m_StartHostButton.interactable = false;
        m_StartClientButton.interactable = false;
    }
}
