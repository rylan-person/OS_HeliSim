using UnityEngine;

public class GameObjectAssigner : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField] private string targetName = "";

    void Awake()
    {
        GameObjectTarget.Register(targetName, target);
    }

    void OnDestroy()
    {
        // Avoid leaving a stale/destroyed reference behind in the shared registry.
        GameObjectTarget.Unregister(targetName, target);
    }
}
