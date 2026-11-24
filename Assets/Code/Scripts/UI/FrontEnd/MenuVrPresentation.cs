using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR;

namespace HeliSim.FrontEnd
{
    // A separate menu camera reads the configured XR runtime; gameplay rigs are untouched.
    public sealed class MenuVrPresentation : MonoBehaviour
    {
        private const string MonitorPreference = "HeliSim.MainMenu.UseMonitor";
        [SerializeField] private Button switchDisplay;
        [SerializeField] private TMP_Text switchDisplayLabel;
        private ControlsRebindingPage controls;
        private bool preferMonitor;
        private bool headsetAvailable;
        private CanvasGroup displayAvailability;
        private bool modeApplied;
        private Canvas menuCanvas;
        private Camera menuCamera;
        private bool presentingInVr;
        private bool cameraOnlyForVr;

        public void ConfigureCamera(Camera camera, bool onlyForVr)
        {
            menuCamera = camera;
            cameraOnlyForVr = onlyForVr;
            if (cameraOnlyForVr) menuCamera.enabled = false;
        }

        private void Start()
        {
            menuCanvas = GetComponent<Canvas>();
            if (menuCamera == null)
                foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (camera.gameObject.scene == gameObject.scene && camera.name == "MenuCamera") { menuCamera = camera; break; }
            if (switchDisplay != null)
            {
                displayAvailability = switchDisplay.GetComponent<CanvasGroup>();
                preferMonitor = PlayerPrefs.GetInt(MonitorPreference, 0) == 1;
                controls = GetComponentInChildren<ControlsRebindingPage>(true);
                switchDisplay.onClick.AddListener(ToggleDisplay);
            }
            RefreshMode(XRSettings.isDeviceActive);
        }

        private void OnEnable()
        {
            Application.onBeforeRender += UpdateHeadPose;
            if (cameraOnlyForVr && menuCamera != null && presentingInVr) menuCamera.enabled = true;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= UpdateHeadPose;
            if (cameraOnlyForVr && menuCamera != null) menuCamera.enabled = false;
        }

        private void Update() => UpdatePresentation(XRSettings.isDeviceActive);

        private void UpdatePresentation(bool headsetActive)
        {
            RefreshMode(headsetActive);
            if (switchDisplay != null && headsetAvailable && Keyboard.current != null &&
                Keyboard.current.f8Key.wasPressedThisFrame && (controls == null || !controls.CapturingShortcutThisFrame))
                ToggleDisplay();
            UpdateHeadPose();
        }

        private void ToggleDisplay()
        {
            if (!headsetAvailable) return;
            preferMonitor = !preferMonitor;
            PlayerPrefs.SetInt(MonitorPreference, preferMonitor ? 1 : 0);
            PlayerPrefs.Save();
            RefreshMode(headsetAvailable);
        }

        private void RefreshMode(bool headsetActive)
        {
            bool available = headsetActive && menuCamera != null;
            bool vr = available && !preferMonitor;
            if (switchDisplay != null && (!modeApplied || headsetAvailable != available || presentingInVr != vr))
            {
                displayAvailability.interactable = available;
                switchDisplayLabel.text = !available ? "Headset unavailable" :
                    vr ? "Menu on monitor (F8)" : "Menu in headset (F8)";
            }
            headsetAvailable = available;
            if (modeApplied && vr == presentingInVr) return;
            modeApplied = true;
            presentingInVr = vr;
            if (switchDisplay != null && menuCamera != null)
            {
                menuCamera.stereoTargetEye = vr ? StereoTargetEyeMask.Both : StereoTargetEyeMask.None;
                menuCamera.GetUniversalAdditionalCameraData().allowXRRendering = vr;
            }
            if (vr)
            {
                menuCamera.enabled = true;
                menuCanvas.renderMode = RenderMode.WorldSpace;
                menuCanvas.worldCamera = menuCamera;
                var rect = (RectTransform)menuCanvas.transform;
                rect.sizeDelta = new Vector2(1920, 1080);
                rect.localScale = Vector3.one * 0.0015f;
                menuCamera.cullingMask = 1 << gameObject.layer;
                menuCamera.nearClipPlane = 0.05f;
                RecenterMenu();
            }
            else
            {
                menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                menuCanvas.worldCamera = null;
                menuCanvas.transform.localRotation = Quaternion.identity;
                menuCanvas.transform.localScale = Vector3.one;
                if (menuCamera != null)
                {
                    menuCamera.cullingMask = 0;
                    if (cameraOnlyForVr) menuCamera.enabled = false;
                }
            }
        }

        private void UpdateHeadPose()
        {
            if (!presentingInVr || menuCamera == null) return;
            menuCamera.transform.SetPositionAndRotation(InputTracking.GetLocalPosition(XRNode.CenterEye), InputTracking.GetLocalRotation(XRNode.CenterEye));
        }

        public void RecenterMenu()
        {
            if (!presentingInVr || menuCamera == null) return;
            UpdateHeadPose();
            Quaternion yaw = Quaternion.Euler(0, menuCamera.transform.eulerAngles.y, 0);
            menuCanvas.transform.SetPositionAndRotation(menuCamera.transform.position + yaw * Vector3.forward * 2.4f, yaw);
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= UpdateHeadPose;
            if (switchDisplay != null) switchDisplay.onClick.RemoveListener(ToggleDisplay);
        }
    }
}
