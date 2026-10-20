using UnityEngine;
using UnityEngine.XR;

namespace HeliSim.FrontEnd
{
    // A separate menu camera reads the configured XR runtime; gameplay rigs are untouched.
    public sealed class MenuVrPresentation : MonoBehaviour
    {
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
            RefreshMode();
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

        private void Update()
        {
            RefreshMode();
            UpdateHeadPose();
        }

        private void RefreshMode()
        {
            bool vr = XRSettings.isDeviceActive && menuCamera != null;
            if (vr == presentingInVr) return;
            presentingInVr = vr;
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
                menuCanvas.transform.localScale = Vector3.one;
                menuCamera.cullingMask = 0;
                if (cameraOnlyForVr) menuCamera.enabled = false;
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

        private void OnDestroy() { Application.onBeforeRender -= UpdateHeadPose; }
    }
}
