using TMPro;
using UnityEngine;

public class StreetMapDashboardPanel : DashboardPanel
{
    public override string PanelName => "Street Map";

    [SerializeField] private UnityEngine.UI.RawImage displayImage;
    [SerializeField] private UnityEngine.UI.Image helicopterMarker;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private UnityEngine.UI.Button fullscreenButton;

    private HelicopterMapTracker tracker;
    private Camera mapCamera;
    private RenderTexture renderTexture;
    private bool isShown;

    protected override void Awake()
    {
        base.Awake();

        if (titleText != null)
            titleText.text = PanelName;

        if (fullscreenButton != null)
            fullscreenButton.onClick.AddListener(ToggleFullscreen);
    }

    private void Update()
    {
        if (!isShown)
            return;

        if (mapCamera == null)
            CreateMapCamera();

        if (mapCamera != null)
        {
            mapCamera.transform.position = tracker.Map.transform.position + Vector3.up * 200f;
            ResizeRenderTexture();
        }

        if (helicopterMarker != null)
            helicopterMarker.enabled = tracker != null && tracker.HasHelicopter;
    }

    public override void OnPanelShown()
    {
        isShown = true;
        CreateMapCamera();
        if (tracker != null)
            tracker.SetMapVisible(true);
        ResizeRenderTexture();
        if (mapCamera != null)
            mapCamera.enabled = true;
        if (helicopterMarker != null)
            helicopterMarker.enabled = tracker != null && tracker.HasHelicopter;
    }

    public override void OnPanelHidden()
    {
        isShown = false;
        if (tracker != null)
            tracker.SetMapVisible(false);
        if (mapCamera != null)
            mapCamera.enabled = false;
        ReleaseRenderTexture();
    }

    public override void SetFullscreen(bool fullscreen)
    {
        ResizeRenderTexture();
    }

    private void CreateMapCamera()
    {
        if (mapCamera != null || displayImage == null)
            return;

        if (tracker == null)
            tracker = FindFirstObjectByType<HelicopterMapTracker>();

        if (tracker == null || tracker.GetOrCreateMap() == null)
            return;

        tracker.SetMapVisible(true);

        GameObject cameraObject = new GameObject("StreetMapPanelCamera");
        mapCamera = cameraObject.AddComponent<Camera>();
        mapCamera.transform.SetPositionAndRotation(
            tracker.Map.transform.position + Vector3.up * 200f,
            Quaternion.Euler(90f, 0f, 0f));
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = 100f;
        mapCamera.nearClipPlane = 0.1f;
        mapCamera.farClipPlane = 400f;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = new Color(0.08f, 0.1f, 0.13f);
        mapCamera.cullingMask = 1 << 8;
        mapCamera.enabled = isShown;
    }

    private void ResizeRenderTexture()
    {
        if (mapCamera == null || displayImage == null)
            return;

        Rect rect = displayImage.rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        int width = Mathf.Clamp(Mathf.RoundToInt(rect.width), 256, 1920);
        int height = Mathf.Clamp(Mathf.RoundToInt(rect.height), 256, 1080);
        if (renderTexture != null && renderTexture.width == width && renderTexture.height == height)
            return;

        ReleaseRenderTexture();
        renderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
        renderTexture.Create();
        mapCamera.targetTexture = renderTexture;
        displayImage.texture = renderTexture;
    }

    private void ReleaseRenderTexture()
    {
        if (mapCamera != null)
            mapCamera.targetTexture = null;
        if (displayImage != null)
            displayImage.texture = null;

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
    }

    private void ToggleFullscreen()
    {
        DashboardManager manager = DashboardManager.Instance;
        if (manager == null || OwnerSlot == null)
            return;

        if (manager.focusedSlot == OwnerSlot)
            manager.Unfocus();
        else
            manager.FocusSlot(OwnerSlot);
    }

    private void OnDestroy()
    {
        if (tracker != null)
            tracker.SetMapVisible(false);
        ReleaseRenderTexture();
        if (mapCamera != null)
            Destroy(mapCamera.gameObject);
    }
}
