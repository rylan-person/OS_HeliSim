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

    private void OnEnable()
    {
        orbitInput.action.Enable();
        dragButton.action.Enable();
    }

    private void OnDisable()
    {
        orbitInput.action.Disable();
        dragButton.action.Disable();
    }

    private void Update()
    {
        bool dragging = dragButton.action.IsPressed();

        inputAxisController.enabled = dragging;
    }
}
