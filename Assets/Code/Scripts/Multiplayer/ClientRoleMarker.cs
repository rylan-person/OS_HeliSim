using UnityEngine;

// Explicit identity for the minimal spectator player prefab. Helicopters use
// NetworkHelicopterRegistry instead, keeping role classification local to spawn.
[DisallowMultipleComponent]
public sealed class ClientRoleMarker : MonoBehaviour
{
    public ClientRole Role => ClientRole.Spectator;
}
