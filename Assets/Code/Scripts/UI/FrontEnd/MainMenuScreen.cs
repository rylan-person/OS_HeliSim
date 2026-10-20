using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeliSim.FrontEnd
{
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private MainMenuPageRouter router;
        [SerializeField] private TMP_InputField address;
        [SerializeField] private TMP_InputField port;
        [SerializeField] private TMP_Dropdown role;
        [SerializeField] private TMP_Dropdown assistance;
        [SerializeField] private Toggle recordFlight;
        [SerializeField] private Toggle recordPreference;
        [SerializeField] private TMP_Text flightSummary;
        [SerializeField] private TMP_Text hardwareSummary;
        [SerializeField] private TMP_Text recordingSummary;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Button quickStart;
        [SerializeField] private Button startSolo;
        [SerializeField] private Button startHost;
        [SerializeField] private Button startJoin;
        [SerializeField] private Button recordingsFolder;
        [SerializeField] private Button quit;
        [SerializeField] private GameObject quitConfirmation;
        [SerializeField] private Button confirmQuit;
        [SerializeField] private Button cancelQuit;

        private readonly MenuSessionOptionsStore store = new MenuSessionOptionsStore();
        private MenuSessionOptions saved;
        private GameObject selectionBeforeQuit;
        private CanvasGroup background;

        private void Awake()
        {
            GetComponentInChildren<HeliSim.InFlightMenu.DisplaySettingsPage>(true)?.RestorePreferencesAtStartup();
        }

        private void Start()
        {
            saved = store.Load();
            string lastAddress = PlayerPrefs.GetString("ServerIP", saved.Address);
            address.SetTextWithoutNotify(RoleSelectionUI.TryNormalizeAddress(lastAddress, out string normalized) ? normalized : saved.Address);
            port.SetTextWithoutNotify(saved.Port.ToString());
            role.SetValueWithoutNotify(saved.Role == ClientRole.Spectator ? 1 : 0);
            assistance.SetValueWithoutNotify(saved.AssistancePreset + 1);
            recordFlight.SetIsOnWithoutNotify(saved.RecordFlightData);
            recordPreference.SetIsOnWithoutNotify(saved.RecordFlightData);
            quickStart.onClick.AddListener(QuickStart);
            startSolo.onClick.AddListener(() => Begin(MenuLaunchMode.Solo));
            startHost.onClick.AddListener(() => Begin(MenuLaunchMode.Host));
            startJoin.onClick.AddListener(() => Begin(MenuLaunchMode.Join));
            recordingsFolder.onClick.AddListener(OpenRecordingsFolder);
            quit.onClick.AddListener(RequestQuit);
            confirmQuit.onClick.AddListener(ConfirmQuit);
            cancelQuit.onClick.AddListener(CancelQuit);
            recordFlight.onValueChanged.AddListener(SetRecordingPreference);
            recordPreference.onValueChanged.AddListener(SetRecordingPreference);
            assistance.onValueChanged.AddListener(_ => RefreshSummary());
            router.PageChanged += OnPageChanged;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            quitConfirmation.SetActive(false);
            RefreshSummary();
            SetStatus(MenuSessionCoordinator.ConsumeMenuMessage());
        }

        private void OnDestroy()
        {
            if (router != null) router.PageChanged -= OnPageChanged;
        }

        private void Update()
        {
            if (quitConfirmation != null && quitConfirmation.activeSelf &&
                UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelQuit();
        }

        private void OnPageChanged(string page)
        {
            if (page == "hardware") hardwareSummary.text = MenuHardwareStatus.BuildSummary();
            if (page == "recordings") RefreshRecordingSummary();
        }

        private void RefreshSummary()
        {
            flightSummary.text = "Assistance: " +
                assistance.options[assistance.value].text + "\nCSV recording: " + (recordFlight.isOn ? "On" : "Off") +
                "\nTime-trial gates and session controls are available in flight.";
            hardwareSummary.text = MenuHardwareStatus.BuildSummary();
            RefreshRecordingSummary();
        }

        private void RefreshRecordingSummary()
        {
            string folder = Path.Combine(Application.persistentDataPath, "Data");
            try
            {
                int count = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.csv").Length : 0;
                recordingSummary.text = count + " CSV recordings\n\n" + folder +
                    "\n\nCSV flight data uses the existing recorder and units. Journey recordings remain available through the in-flight menu. Playback is configured in flight.";
            }
            catch (Exception exception)
            {
                recordingSummary.text = "Recordings folder is unavailable: " + exception.Message;
            }
        }

        private void SetRecordingPreference(bool enabled)
        {
            recordFlight.SetIsOnWithoutNotify(enabled);
            recordPreference.SetIsOnWithoutNotify(enabled);
            saved.RecordFlightData = enabled;
            store.Save(saved);
            RefreshSummary();
        }

        private void QuickStart()
        {
            var options = store.Load();
            options.Mode = MenuLaunchMode.Solo;
            options.Role = ClientRole.Player;
            router.RequestAction(() => Launch(options));
        }

        private void Begin(MenuLaunchMode mode)
        {
            if (!ushort.TryParse(port.text, out ushort number) || number == 0)
            {
                SetStatus("Enter a port between 1 and 65535.");
                EventSystem.current?.SetSelectedGameObject(port.gameObject);
                return;
            }
            var options = new MenuSessionOptions
            {
                Mode = mode,
                Role = role.value == 1 ? ClientRole.Spectator : ClientRole.Player,
                Address = address.text,
                Port = number,
                AssistancePreset = assistance.value - 1,
                RecordFlightData = recordFlight.isOn,
            };
            router.RequestAction(() => Launch(options));
        }

        private void Launch(MenuSessionOptions options)
        {
            if (!options.TryValidate(UnityEngine.Application.CanStreamedLevelBeLoaded, out string error))
            {
                SetStatus(error);
                return;
            }
            MenuSessionCoordinator.Launch(options);
        }

        private void OpenRecordingsFolder()
        {
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "Data");
                Directory.CreateDirectory(folder);
                Application.OpenURL(new Uri(folder).AbsoluteUri);
            }
            catch (Exception exception) { SetStatus("Could not open recordings: " + exception.Message); }
        }

        private void RequestQuit() => router.RequestAction(() =>
        {
            router.NavigationSuspended = true;
            selectionBeforeQuit = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            // The modal is a sibling of the page container, which owns all normal controls.
            background = GetComponentInChildren<MainMenuContentMarker>(true)?.GetComponent<CanvasGroup>();
            if (background != null) { background.interactable = false; background.blocksRaycasts = false; }
            quitConfirmation.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(cancelQuit.gameObject);
        });

        private void CancelQuit()
        {
            quitConfirmation.SetActive(false);
            StartCoroutine(ResumeNavigationNextFrame());
            if (background != null) { background.interactable = true; background.blocksRaycasts = true; }
            EventSystem.current?.SetSelectedGameObject(selectionBeforeQuit != null ? selectionBeforeQuit : quit.gameObject);
        }

        private System.Collections.IEnumerator ResumeNavigationNextFrame()
        {
            yield return null;
            router.NavigationSuspended = false;
        }

        private static void ConfirmQuit()
        {
            PlayerPrefs.Save();
            Application.Quit();
        }

        private void SetStatus(string message) { if (status != null) status.text = message ?? string.Empty; }
    }
}
