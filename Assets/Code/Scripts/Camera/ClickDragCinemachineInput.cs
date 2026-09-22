using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class ClickDragCinemachineInput : MonoBehaviour
{
 [Header("Input Actions")]
    [SerializeField] private InputActionReference orbitInput;
    [SerializeField] private InputActionReference dragButton;

    [Header("Cinemachine")]
    [SerializeField] private CinemachineInputAxisController inputAxisController;

    [Header("Dashboard Input")]
    [SerializeField] private PanelCameraType panelCameraType = PanelCameraType.Orbit;

    private bool dragStartedOverPanel;

    private void OnEnable()
    {
        dragStartedOverPanel = false;
        inputAxisController.enabled = false;
        orbitInput.action.Enable();
        dragButton.action.Enable();
    }

    private void OnDisable()
    {
        orbitInput.action.Disable();
        dragButton.action.Disable();
        dragStartedOverPanel = false;
        inputAxisController.enabled = false;
    }

    private void Update()
    {
        bool isDragButtonPressed = dragButton.action.IsPressed();
        if (dragButton.action.WasPressedThisFrame())
        {
            Vector2 pointerPosition = Pointer.current != null ? Pointer.current.position.ReadValue() : default;
            dragStartedOverPanel = DashboardCameraRegistry.IsPointerOverPanel(panelCameraType, pointerPosition);
        }
        else if (!isDragButtonPressed)
        {
            dragStartedOverPanel = false;
        }

        inputAxisController.enabled = isDragButtonPressed && dragStartedOverPanel;
    }
}
