using UnityEngine;

public class GameObjectAssigner : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField] private string targetName = "";

    void Awake() 
    {
        GameObjectTarget.target[targetName] = target;
    }
}
