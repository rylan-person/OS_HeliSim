using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CameraDashboardPanel : DashboardPanel
{
    public override string PanelName => "Camera Panel";
    [SerializeField] private PanelCameraType cameraType;
    [SerializeField] private Camera panelCamera;
    [SerializeField] private RawImage displayImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button fullscreenButton;

    private RenderTexture renderTexture;
    private DashboardSlot ownerSlot;

    private void Start()
    {
        ownerSlot = GetComponentInParent<DashboardSlot>();
        if (ownerSlot == null)
            Debug.LogWarning($"{name} could not find a parent DashboardSlot.", this);

        if (panelCamera == null)
        {
            panelCamera = DashboardCameraRegistry.GetCamera(cameraType);
            if (panelCamera == null)
            {
                Debug.LogError($"No camera found for type {cameraType} in {name}. Please assign a camera or ensure one is registered.", this);
            }
        }

        CreateRenderTexture(960, 540);

        SetTitleText();
        InitialiseButton();
    }

    private void InitialiseButton()
    {
        if (fullscreenButton == null)
            return;

        fullscreenButton.onClick.RemoveAllListeners();
        fullscreenButton.onClick.AddListener(() =>
        {
            if (ownerSlot != null && DashboardManager.Instance != null)
                if (DashboardManager.Instance.focusedSlot == ownerSlot)
                    DashboardManager.Instance.Unfocus();
                else
                    DashboardManager.Instance.FocusSlot(ownerSlot);
        });
    }

    private void CreateRenderTexture(int width, int height)
    {
        renderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
        renderTexture.Create();

        panelCamera.targetTexture = renderTexture;
        displayImage.texture = renderTexture;
    }

    private void SetTitleText()
    {
        if (titleText != null)
        {
            titleText.text = $"{PanelName} - {cameraType}";
        }
    }

    public override void SetFullscreen(bool fullscreen)
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (fullscreen)
            CreateRenderTexture(1920, 1080);
        else
            CreateRenderTexture(960, 540);
    }

    public override void OnPanelShown()
    {
        panelCamera.enabled = true;
    }

    public override void OnPanelHidden()
    {
        panelCamera.enabled = false;
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}