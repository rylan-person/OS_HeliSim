using UnityEngine;

public sealed class ReturnToMenuAction : MonoBehaviour
{
    public void RequestReturnToMenu()
    {
        // Menu navigation intentionally remains unavailable until a destination scene exists.
        Debug.LogWarning("Return to Menu is unavailable because no menu scene has been configured.");
    }
}
