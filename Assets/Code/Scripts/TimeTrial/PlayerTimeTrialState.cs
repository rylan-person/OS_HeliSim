using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerTimeTrialState : NetworkBehaviour
{
    [SerializeField] private bool autoAssignForLocalPlayer = true;

    public static PlayerTimeTrialState ActiveState { get; private set; }
    public static event Action<PlayerTimeTrialState> ActiveStateChanged;

    private readonly NetworkVariable<int> currentWaypoint = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<int> currentSector = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> currentLapTime = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> currentSectorTime = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<bool> lapActive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkList<float> currentSectorSplits = new NetworkList<float>(
        null,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkList<float> averageSectors = new NetworkList<float>(
        null,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkList<float> bestSectors = new NetworkList<float>(
        null,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkList<float> bestLapSectors = new NetworkList<float>(
        null,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> bestLapTime = new NetworkVariable<float>(
        float.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> optLapTime = new NetworkVariable<float>(
        float.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> lastLapTime = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> lastLapDiffTime = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> averageLapTime = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<bool> lastLapWasNewBest = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public int CurrentWaypoint => currentWaypoint.Value;
    public int CurrentSector => currentSector.Value;
    public float CurrentLapTime => currentLapTime.Value;
    public float CurrentSectorTime => currentSectorTime.Value;
    public bool LapActive => lapActive.Value;
    public int CurrentSectorSplitCount => currentSectorSplits.Count;
    public float BestLapTime => bestLapTime.Value;
    public float OptLapTime => optLapTime.Value;
    public float LastLapTime => lastLapTime.Value;
    public float LastLapDiffTime => lastLapDiffTime.Value;
    public float AverageLapTime => averageLapTime.Value;
    public bool LastLapWasNewBest => lastLapWasNewBest.Value;
    public int AverageSectorCount => averageSectors.Count;
    public int BestSectorCount => bestSectors.Count;
    public int BestLapSectorCount => bestLapSectors.Count;

    public float GetCurrentSectorSplit(int index)
    {
        if (index < 0 || index >= currentSectorSplits.Count)
        {
            return 0f;
        }

        return currentSectorSplits[index];
    }

    public float GetAverageSector(int index)
    {
        if (index < 0 || index >= averageSectors.Count)
        {
            return 0f;
        }

        return averageSectors[index];
    }

    public float GetBestSector(int index)
    {
        if (index < 0 || index >= bestSectors.Count)
        {
            return float.MaxValue;
        }

        return bestSectors[index];
    }

    public float GetBestLapSector(int index)
    {
        if (index < 0 || index >= bestLapSectors.Count)
        {
            return float.MaxValue;
        }

        return bestLapSectors[index];
    }

    public void PublishLocalProgress(int waypoint, int sector, float lapTime, float sectorTime, bool isLapActive, IReadOnlyList<float> sectorSplits = null)
    {
        if (!CanWriteState())
        {
            return;
        }

        currentWaypoint.Value = Mathf.Max(0, waypoint);
        currentSector.Value = Mathf.Max(0, sector);
        currentLapTime.Value = Mathf.Max(0f, lapTime);
        currentSectorTime.Value = Mathf.Max(0f, sectorTime);
        lapActive.Value = isLapActive;

        if (sectorSplits != null)
        {
            SyncSectorSplits(sectorSplits);
        }
    }

    public void PublishComparisonData(
        float bestLap,
        float optLap,
        float completedLap,
        float completedLapDiff,
        float averageLap,
        bool wasNewBest,
        IReadOnlyList<float> averageSectorValues,
        IReadOnlyList<float> bestSectorValues,
        IReadOnlyList<float> bestLapSectorValues)
    {
        if (!CanWriteState())
        {
            return;
        }

        bestLapTime.Value = Mathf.Max(0f, bestLap);
        optLapTime.Value = Mathf.Max(0f, optLap);
        lastLapTime.Value = Mathf.Max(0f, completedLap);
        lastLapDiffTime.Value = completedLapDiff;
        averageLapTime.Value = Mathf.Max(0f, averageLap);
        lastLapWasNewBest.Value = wasNewBest;

        if (averageSectorValues != null)
        {
            SyncList(averageSectors, averageSectorValues);
        }

        if (bestSectorValues != null)
        {
            SyncList(bestSectors, bestSectorValues);
        }

        if (bestLapSectorValues != null)
        {
            SyncList(bestLapSectors, bestLapSectorValues);
        }
    }

    public static void SetActiveState(PlayerTimeTrialState state)
    {
        if (ActiveState == state)
        {
            return;
        }

        ActiveState = state;
        ActiveStateChanged?.Invoke(ActiveState);
    }

    public static void SetActiveStateFromTransform(Transform target)
    {
        if (target == null)
        {
            if (ActiveState != null)
            {
                SetActiveState(null);
            }

            return;
        }

        var state = target.GetComponentInChildren<PlayerTimeTrialState>();
        if (state == null)
        {
            state = target.GetComponentInParent<PlayerTimeTrialState>();
        }

        if (state != null || ActiveState != null)
        {
            SetActiveState(state);
        }
    }

    private bool CanWriteState()
    {
        if (!IsSpawned)
        {
            return true;
        }

        return IsOwner;
    }

    private void Start()
    {
        TryAutoAssignAsActive();
    }

    private void Update()
    {
        if (ActiveState == null)
        {
            TryAutoAssignAsActive();
        }
    }

    private void TryAutoAssignAsActive()
    {
        if (!autoAssignForLocalPlayer)
        {
            return;
        }

        if (!IsSpawned)
        {
            var networkManager = NetworkManager.Singleton;
            bool netcodeNotRunning = networkManager == null || !networkManager.IsListening;
            if (netcodeNotRunning && ActiveState == null)
            {
                SetActiveState(this);
            }

            return;
        }

        if (IsOwner)
        {
            SetActiveState(this);
        }
    }

    private void SyncSectorSplits(IReadOnlyList<float> sectorSplits)
    {
        bool changed = false;

        if (currentSectorSplits.Count != sectorSplits.Count)
        {
            changed = true;
        }
        else
        {
            for (int i = 0; i < sectorSplits.Count; i++)
            {
                if (!Mathf.Approximately(currentSectorSplits[i], sectorSplits[i]))
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
        {
            return;
        }

        currentSectorSplits.Clear();
        for (int i = 0; i < sectorSplits.Count; i++)
        {
            currentSectorSplits.Add(sectorSplits[i]);
        }
    }

    private static void SyncList(NetworkList<float> destination, IReadOnlyList<float> source)
    {
        bool changed = false;

        if (destination.Count != source.Count)
        {
            changed = true;
        }
        else
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (!Mathf.Approximately(destination[i], source[i]))
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
        {
            return;
        }

        destination.Clear();
        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(source[i]);
        }
    }
}