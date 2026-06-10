using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Global/GameReferences")]
public class GameReferences : ScriptableObject
{
    public MainMenu mainMenu;
    public WaypointManager waypointManager;
}
