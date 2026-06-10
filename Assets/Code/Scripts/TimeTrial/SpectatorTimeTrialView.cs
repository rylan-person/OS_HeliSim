using UnityEngine;

public class SpectatorTimeTrialView : MonoBehaviour
{
    [SerializeField] private bool enableView = true;
    [SerializeField] private GameObject[] waypoints;
    [SerializeField] private Timing[] timingManagers;

    private PlayerTimeTrialState activeState;
    private int lastWaypoint = -1;

    private void OnEnable()
    {
        PlayerTimeTrialState.ActiveStateChanged += OnActiveStateChanged;
        OnActiveStateChanged(PlayerTimeTrialState.ActiveState);
    }

    private void OnDisable()
    {
        PlayerTimeTrialState.ActiveStateChanged -= OnActiveStateChanged;
    }

    private void Update()
    {
        if (!enableView || activeState == null)
        {
            return;
        }

        for (int i = 0; i < timingManagers.Length; i++)
        {
            timingManagers[i].updateCurrentLapTime(activeState.CurrentLapTime);
            timingManagers[i].updateBestLapTime(activeState.BestLapTime);
            timingManagers[i].updateOptLapTime(activeState.OptLapTime);
            timingManagers[i].updateLastLapTime(activeState.LastLapTime, activeState.LastLapWasNewBest);
            timingManagers[i].updateLastLapTimeDifference(activeState.LastLapDiffTime, activeState.BestLapTime, activeState.AverageLapTime);

            int sectorUiCount = timingManagers[i].sectorTimesText != null ? timingManagers[i].sectorTimesText.Length : 0;
            for (int sectorIndex = 0; sectorIndex < sectorUiCount; sectorIndex++)
            {
                if (sectorIndex < activeState.CurrentSectorSplitCount)
                {
                    float splitTime = activeState.GetCurrentSectorSplit(sectorIndex);
                    if (splitTime > 0f)
                    {
                        timingManagers[i].updateSectorTime(sectorIndex, splitTime);

                        float avgSector = activeState.GetAverageSector(sectorIndex);
                        float bestSector = activeState.GetBestSector(sectorIndex);
                        float bestLapSector = activeState.GetBestLapSector(sectorIndex);
                        if (bestSector < float.MaxValue * 0.5f)
                        {
                            timingManagers[i].updateSectorTimeDifference(sectorIndex, splitTime, avgSector, bestSector, bestLapSector);
                        }
                        else
                        {
                            timingManagers[i].resetSectorDifferenceTime(sectorIndex);
                        }
                    }
                    else
                    {
                        timingManagers[i].resetSectorTime(sectorIndex);
                        timingManagers[i].resetSectorDifferenceTime(sectorIndex);
                    }
                }
                else
                {
                    timingManagers[i].resetSectorTime(sectorIndex);
                    timingManagers[i].resetSectorDifferenceTime(sectorIndex);
                }
            }

            if (activeState.CurrentSector >= 0 && activeState.CurrentSector < timingManagers[i].sectorTimesText.Length)
            {
                timingManagers[i].updateSectorTime(activeState.CurrentSector, activeState.CurrentSectorTime);
            }
        }

        if (activeState.CurrentWaypoint != lastWaypoint)
        {
            ApplyWaypointView(activeState.CurrentWaypoint);
            lastWaypoint = activeState.CurrentWaypoint;
        }
    }

    private void OnActiveStateChanged(PlayerTimeTrialState state)
    {
        activeState = state;
        lastWaypoint = -1;

        ResetTimingView();
        ApplyWaypointView(activeState != null ? activeState.CurrentWaypoint : 0);
    }

    private void ResetTimingView()
    {
        for (int i = 0; i < timingManagers.Length; i++)
        {
            timingManagers[i].resetCurrentLapTime();
        }
    }

    private void ApplyWaypointView(int currentWaypoint)
    {
        for (int i = 0; i < waypoints.Length; i++)
        {
            var waypoint = waypoints[i].GetComponentInChildren<Waypoint>();
            if (waypoint != null)
            {
                waypoint.resetCheckpoint();
            }

            waypoints[i].SetActive(false);
        }

        for (int i = currentWaypoint; i < currentWaypoint + 3 && i < waypoints.Length; i++)
        {
            waypoints[i].SetActive(true);
        }

        if (currentWaypoint >= 0 && currentWaypoint < waypoints.Length)
        {
            var nextWaypoint = waypoints[currentWaypoint].GetComponentInChildren<Waypoint>();
            if (nextWaypoint != null)
            {
                nextWaypoint.setAsNext();
            }
        }
    }
}