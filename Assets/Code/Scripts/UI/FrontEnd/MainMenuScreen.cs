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
        [Tooltip("Pilot (off) or Spectator (on), shown as a segmented control.")]
        [SerializeField] private Toggle spectatorRole;
        [SerializeField] private TMP_Dropdown assistance;
        [SerializeField] private Toggle recordFlight;
        [SerializeField] private Toggle recordPreference;
        [SerializeField] private TMP_Text flightSummary;
        [SerializeField] private TMP_Text hardwareSummary;
        [SerializeField] private TMP_Text recordingSummary;
        [Header("Hardware cards (optional)")]
        [SerializeField] private TMP_Text controlsStatus;
        [SerializeField] private Graphic controlsDot;
        [SerializeField] private TMP_Text headsetStatus;
        [SerializeField] private TMP_Text headsetDetail;
        [SerializeField] private Graphic headsetDot;
        [Header("Header status chips (optional, read-only)")]
        [SerializeField] private TMP_Text inputChip;
        [SerializeField] private Graphic inputChipDot;
        [SerializeField] private TMP_Text headsetChip;
        [SerializeField] private Graphic headsetChipDot;
        [Header("Quick start (optional)")]
        [SerializeField] private TMP_Text quickStartSummary;
        [SerializeField] private HeliSim.UI.Theme.MenuTheme theme;
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
        private bool headsetWasActive;
        private bool hardwareDirty;

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
            spectatorRole.SetIsOnWithoutNotify(saved.Role == ClientRole.Spectator);
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

        private void OnEnable() => UnityEngine.InputSystem.InputSystem.onDeviceChange += OnDeviceChange;

        private void OnDisable() => UnityEngine.InputSystem.InputSystem.onDeviceChange -= OnDeviceChange;

        // Refresh once on the next frame rather than inside the Input System callback.
        private void OnDeviceChange(UnityEngine.InputSystem.InputDevice device, UnityEngine.InputSystem.InputDeviceChange change) =>
            hardwareDirty = true;

        private void Update()
        {
            bool headsetActive = MenuHardwareStatus.HeadsetActive;
            if (hardwareDirty || headsetActive != headsetWasActive)
            {
                hardwareDirty = false;
                RefreshHardware();
            }

            if (quitConfirmation != null && quitConfirmation.activeSelf &&
                UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelQuit();
        }

        private void OnPageChanged(string page)
        {
            if (page == "hardware") RefreshHardware();
            if (page == "recordings") RefreshRecordingSummary();
        }

        private void RefreshSummary()
        {
            flightSummary.text = "Assistance <color=#E6EDF2>" + assistance.options[assistance.value].text +
                "</color>  ·  CSV recording <color=#E6EDF2>" + (recordFlight.isOn ? "On" : "Off") + "</color>";
            RefreshHardware();
            RefreshRecordingSummary();
            RefreshQuickStart();
        }

        private static string Strong(string text) => "<color=#E6EDF2>" + text + "</color>";

        /// <summary>Summarises the stored last valid setup that "Fly last setup" launches.</summary>
        private void RefreshQuickStart()
        {
            if (quickStartSummary == null || saved == null) return;
            int option = saved.AssistancePreset + 1;
            string assist = option >= 0 && option < assistance.options.Count ? assistance.options[option].text : "Keep aircraft default";
            // Non-breaking spaces keep each label with its value when the line wraps.
            quickStartSummary.text = "Aircraft " + Strong("R66") + "     Location " + Strong("Multiplayer environment") +
                "     Assistance " + Strong(assist.Replace(' ', ' ')) + "     CSV " + Strong(saved.RecordFlightData ? "On" : "Off");
        }

        private void RefreshRecordingSummary()
        {
            string folder = Path.Combine(Application.persistentDataPath, "Data");
            try
            {
                int count = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.csv").Length : 0;
                recordingSummary.text = count == 1 ? "1 CSV recording" : count + " CSV recordings";
            }
            catch (Exception exception)
            {
                recordingSummary.text = "Recordings folder is unavailable: " + exception.Message;
            }
        }

        private void RefreshHardware()
        {
            var palette = HeliSim.UI.Theme.MenuTheme.PaletteOf(theme);
            headsetWasActive = MenuHardwareStatus.HeadsetActive;
            string inputLabel = MenuHardwareStatus.InputChipLabel(out MenuHardwareStatus.ChipState inputState);
            if (inputChip != null) inputChip.text = inputLabel;
            if (inputChipDot != null) inputChipDot.color = ChipColor(palette, inputState);
            string headsetLabel = MenuHardwareStatus.HeadsetChipLabel(out MenuHardwareStatus.ChipState headsetState);
            if (headsetChip != null) headsetChip.text = headsetLabel;
            if (headsetChipDot != null) headsetChipDot.color = ChipColor(palette, headsetState);
            bool cards = controlsStatus != null;
            hardwareSummary.text = cards ? MenuHardwareStatus.InputDevicesDetail() : MenuHardwareStatus.BuildSummary();
            if (!cards) return;
            controlsStatus.text = MenuHardwareStatus.InputDevicesValue();
            if (controlsDot != null) controlsDot.color = UnityEngine.InputSystem.InputSystem.devices.Count > 0 ? palette.ok : palette.text3;
            if (headsetStatus != null) headsetStatus.text = MenuHardwareStatus.HeadsetValue();
            if (headsetDetail != null) headsetDetail.text = MenuHardwareStatus.HeadsetDetail();
            if (headsetDot != null) headsetDot.color = MenuHardwareStatus.HeadsetActive ? palette.ok : palette.text3;
        }

        private static Color ChipColor(HeliSim.UI.Theme.MenuPalette palette, MenuHardwareStatus.ChipState state) =>
            state == MenuHardwareStatus.ChipState.Ok ? palette.ok :
            state == MenuHardwareStatus.ChipState.Caution ? palette.caution : palette.text3;

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
                Role = spectatorRole.isOn ? ClientRole.Spectator : ClientRole.Player,
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
