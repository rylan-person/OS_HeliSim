using Unity.Netcode;
using UnityEngine;

public class CustomNetworkManager : NetworkManager
{
    public Transform[] spawnPoints;
    private int nextSpawnIndex = 0;

}