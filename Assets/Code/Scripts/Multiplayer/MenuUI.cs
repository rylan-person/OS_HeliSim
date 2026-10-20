using System.Collections.Generic;
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
    [SerializeField] private Button m_StartHostButton;
    [SerializeField] private Button m_StartClientButton;
    // Retain old serialized fields for prefab compatibility. Startup is owned by RoleSelectionUI.
    [SerializeField] private NetworkPrefabsList networkPrefabsList;
    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private ushort serverPort = 7777;
    [SerializeField] private RoleSelectionUI roleSelectionUI;
    public List<GameObject> networkPrefabs = new List<GameObject>();

    private void Awake()
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

    private void Start()
    {
        ResolveRoleUI();
        if (m_StartHostButton != null && !HasSharedStartupListener(m_StartHostButton, "StartAsHost"))
        {
            m_StartHostButton.onClick.AddListener(StartHost);
        }
        if (m_StartClientButton != null && !HasSharedStartupListener(m_StartClientButton, "StartAsClient"))
        {
            m_StartClientButton.onClick.AddListener(StartClient);
        }
    }

    private bool HasSharedStartupListener(Button button, string methodName)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) is RoleSelectionUI && button.onClick.GetPersistentMethodName(i) == methodName &&
                button.onClick.GetPersistentListenerState(i) != UnityEngine.Events.UnityEventCallState.Off)
            {
                return true;
            }
        }
        return false;
    }

    private RoleSelectionUI ResolveRoleUI()
    {
        if (roleSelectionUI == null)
        {
            roleSelectionUI = GetComponent<RoleSelectionUI>();
        }
        if (roleSelectionUI == null)
        {
            roleSelectionUI = GetComponentInParent<RoleSelectionUI>();
        }
        if (roleSelectionUI == null)
        {
            roleSelectionUI = FindAnyObjectByType<RoleSelectionUI>(FindObjectsInactive.Include);
        }
        return roleSelectionUI;
    }

    public void SetServerAddress(string address)
    {
        serverAddress = address;
        ResolveRoleUI()?.SetServerAddress(address);
    }

    private void StartClient()
    {
        ResolveRoleUI()?.StartAsClient();
    }

    private void StartHost()
    {
        ResolveRoleUI()?.StartAsHost();
    }

    private void OnDestroy()
    {
        if (m_StartHostButton != null)
        {
            m_StartHostButton.onClick.RemoveListener(StartHost);
        }
        if (m_StartClientButton != null)
        {
            m_StartClientButton.onClick.RemoveListener(StartClient);
        }
    }
}
