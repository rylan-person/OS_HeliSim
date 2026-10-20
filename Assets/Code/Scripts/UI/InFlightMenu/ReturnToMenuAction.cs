using UnityEngine;

public sealed class ReturnToMenuAction : MonoBehaviour
{
    public void RequestReturnToMenu()
    {
        if (!Application.isPlaying || !Application.CanStreamedLevelBeLoaded(HeliSim.FrontEnd.MenuSessionCoordinator.MenuScenePath))
        {
            Debug.LogWarning("Return to Menu is unavailable outside Play mode or without the menu in build settings.");
            return;
        }
        var canvas = GetComponentInParent<Canvas>();
        var controller = canvas != null ? canvas.GetComponentInChildren<PauseMenuController>(true) : null;
        if (controller != null) controller.RequestAction(HeliSim.FrontEnd.MenuSessionCoordinator.RequestReturnToMenu);
        else HeliSim.FrontEnd.MenuSessionCoordinator.RequestReturnToMenu();
    }
}
