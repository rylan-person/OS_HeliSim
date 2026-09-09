using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public class WaypointManager : MonoBehaviour
{
    public static WaypointManager Instance { get; private set; }

    public bool lapActive = false;
    public bool goLock = false;
    public Transform helicopterTransform;
    [SerializeField] private  Transform defaultStartPosition;
    private Vector3 startingPosition;
    [SerializeField] private GameObject[] waypoints;
    public int _sectors = 5;
    [SerializeField] public List<float> currentLapSectors = new List<float>();
    [SerializeField] private List<float> bestLapSectors = new List<float>();
    [SerializeField] private List<float> bestSectors = new List<float>();
    [SerializeField] private List<float> averageSectors = new List<float>();
    private float averageLap;

    // Array of lapTimes
    [SerializeField] private List<float> lapTimes = new List<float>();
    [SerializeField] private int currentSector = 0;
    [SerializeField] private int currentWaypoint = 0;
    [SerializeField] private float currentLapTime = 0;
    [SerializeField] private float currentSectorTime = 0;

    private float lastLapTime = 0;
    private float lastLapDiffTime = 0;
    private bool lastLapWasNewBest = false;

    [SerializeField] private List<(float distanceFromEnd, float currentLapTime)> currentLapDistanceTimes = new List<(float, float)>();
    [SerializeField] private List<(float distanceFromEnd, float bestLapTime)> bestLapDistanceWaypointTimes = new List<(float, float)>();


    [SerializeField] private float bestLap = float.MaxValue;

    [SerializeField] private Timing[] timingManagers;
    [SerializeField] private PlayerTimeTrialState playerTimeTrialState;
    [SerializeField] private bool autoRegisterTimingManagersOnStart = true;
    [SerializeField] private bool debugLogsEnabled = false;

    private float optLapTime = 0;

    private string filePath;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        for (int i = 0; i < waypoints.Length; i++)
        {
            var waypoint = waypoints[i].GetComponentInChildren<Waypoint>();
            if (waypoint == null)
            {
                Debug.LogError($"{nameof(WaypointManager)}: waypoint entry {i} ('{waypoints[i].name}') has no Waypoint component in its children.", waypoints[i]);
                continue;
            }
            waypoint.waypointNumber = i + 1;
        }

        ResolvePlayerState();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool finishWaypoint(int waypointNumber)
    {
        LogDebug("WaypointNumber: " + waypointNumber + " : currentWaypoint: " + currentWaypoint);
        if (currentWaypoint == waypointNumber - 1)
        {
            var waypoint = waypoints[currentWaypoint].GetComponentInChildren<Waypoint>();
            if (waypoint == null)
            {
                Debug.LogError($"{nameof(WaypointManager)}: waypoint {currentWaypoint} has no Waypoint component.", waypoints[currentWaypoint]);
                return false;
            }
            waypoint.checkpointPassed();

            if (currentWaypoint + 3 < waypoints.Length)
            {
                waypoints[currentWaypoint + 3].SetActive(true);
            }
            if (currentWaypoint - 1 >= 0)
            {
                waypoints[currentWaypoint - 1].SetActive(false);
            }

            currentWaypoint++;
            if (currentWaypoint < waypoints.Length)
            {
                var nextWaypoint = waypoints[currentWaypoint].GetComponentInChildren<Waypoint>();
                if (nextWaypoint != null)
                {
                    nextWaypoint.setAsNext();
                }
            }

            PublishPlayerProgress();
            return true;
        }

        return false;
    }

    public void finishSector(int sectorNumber)
    {
        if (sectorNumber == currentSector + 1)
        {
            currentLapSectors[currentSector] = currentSectorTime;
            for (int i = 0; i < timingManagers.Length; i++)
            {
                timingManagers[i].updateSectorTime(currentSector, currentLapSectors[currentSector]);
                timingManagers[i].updateSectorTimeDifference(currentSector, currentLapSectors[currentSector], averageSectors[currentSector], bestSectors[currentSector], bestLapSectors[currentSector]);
            }
            currentSector++;
            currentSectorTime = 0;


            if (currentSector == _sectors)
            {
                finishLap();
            }

            PublishPlayerProgress();
        }
    }

    private void finishLap()
    {
        // Add the lap to lap times
        lapTimes.Add(currentLapTime);

        // if the new best lap true
        bool newBest = false;

        lastLapTime = currentLapTime;
        lastLapDiffTime = lastLapTime - bestLap;

        // Check if the lap is the best lap
        if (currentLapTime < bestLap)
        {
            // Copy the values rather than aliasing the list reference — currentLapSectors
            // continues to be mutated in-place during the next lap (see finishSector), and if
            // bestLapSectors pointed at the same List<float> instance those in-place writes
            // would silently corrupt the "best lap" record before it could be displayed/saved.
            bestLapSectors = new List<float>(currentLapSectors);
            bestLap = currentLapTime;
            bestLapDistanceWaypointTimes = new List<(float, float)>(currentLapDistanceTimes);
            newBest = true;
        }

        // If there was a best sector, update the best sectors
        for (int i = 0; i < bestSectors.Count; i++)
        {
            if (currentLapSectors[i] < bestSectors[i])
            {
                bestSectors[i] = currentLapSectors[i];
            }
        }

        // Calculate the new average sectors
        for (int i = 0; i < averageSectors.Count; i++)
        {
            averageSectors[i] = (averageSectors[i] * lapTimes.Count + currentLapSectors[i]) / (lapTimes.Count + 1);
        }

        averageLap = 0;
        for (int i = 0; i < averageSectors.Count; i++)
        {
            averageLap += averageSectors[i];
        }

        lapActive = false;
        goLock = true;
        lastLapWasNewBest = newBest;

        updateDisplayTimes(newBest);
        updateOptLap();
        PublishPlayerComparisons(newBest);

        SaveTimesToCSV();
    }

    private void updateDisplayTimes(bool newBest, bool resetLapTimes = false)
    {
        averageLap = 0f;
        for (int i = 0; i < averageSectors.Count; i++)
        {
            averageLap += averageSectors[i];
        }

        for (int i = 0; i < timingManagers.Length; i++)
        {
            if (resetLapTimes)
            {
                timingManagers[i].resetCurrentLapTime();
            }
            timingManagers[i].updateBestLapTime(bestLap);
            timingManagers[i].updateOptLapTime(optLapTime);
            timingManagers[i].updateLastLapTime(lastLapTime, newBest);
            timingManagers[i].updateLastLapTimeDifference(lastLapDiffTime, bestLap, averageLap);
        }
    }

    public void resetLap()
    {
        LogDebug("Resetting Lap");
        ResetLapDataToDefault();
        PublishPlayerProgress();
    }

    private void getBestLap()// [7,10,5,6]
    {
        if (lapTimes.Count == 0)
        {
            bestLap = float.MaxValue;
            return;
        }

        for (int i = 0; i < lapTimes.Count; i++)
        {
            if (lapTimes[i] < bestLap)
            {
                bestLap = lapTimes[i];
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (autoRegisterTimingManagersOnStart)
        {
            RegisterTimingManagersForLoadedPlayer();
        }

        // Keep in start
        filePath = Path.Combine(Application.persistentDataPath, "LapTimes.txt");
        LoadTimesFromCSV();

        int defaultSectorCount = _sectors;
        if (averageSectors.Count == 0) averageSectors = CreateFilledList(defaultSectorCount, 0f);
        if (bestLapSectors.Count == 0) bestLapSectors = CreateFilledList(defaultSectorCount, float.MaxValue);
        if (bestSectors.Count == 0) bestSectors = CreateFilledList(defaultSectorCount, float.MaxValue);

        for (int i = 0; i < 3 && i < waypoints.Length; i++)
        {
            waypoints[i].SetActive(true);
        }


        if (helicopterTransform != null)
        {
            startingPosition = helicopterTransform.position;
        }
        else
        {
            startingPosition = Vector3.zero;
            Debug.LogWarning("HelicopterTransform and DefaultStartPosition are null. Using Vector3.zero as starting position.");
        }

        ResetLapDataToDefault();

        // Set each waypoints distance from the end using the end waypoint as the finish line, the calculate the distance from the end
        for (int i = waypoints.Length - 2; i >= 0; i--)
        {
            var currentWaypointComponent = waypoints[i].GetComponentInChildren<Waypoint>();
            var nextWaypointComponent = waypoints[i + 1].GetComponentInChildren<Waypoint>();
            if (currentWaypointComponent == null || nextWaypointComponent == null)
            {
                Debug.LogError($"{nameof(WaypointManager)}: waypoint {i} or {i + 1} is missing a Waypoint component; skipping distance-from-end calculation.");
                continue;
            }

            currentWaypointComponent.distanceFromEnd = Vector3.Distance(waypoints[i].transform.position, waypoints[i + 1].transform.position) + nextWaypointComponent.distanceFromEnd;
        }

    }
    
    public void SaveTimesToCSV()
    {
        try
        {
            using (StreamWriter file = new StreamWriter(filePath))
            {
                file.WriteLine(string.Join(",", lapTimes));
                file.WriteLine(string.Join(",", averageSectors));
                file.WriteLine(string.Join(",", bestLapSectors));
                file.WriteLine(string.Join(",", bestSectors));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error saving lap times: " + ex.Message);
        }
    }


    public void LoadTimesFromCSV()
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("Lap times file not found. Initializing default values.");
            InitializeSectorLists();
            return;
        }

        try
        {
            using (StreamReader file = new StreamReader(filePath))
            {
                string line;

                // Line 1: Lap Times
                line = file.ReadLine();
                lapTimes = ParseFloatList(line);

                // Line 2: Average Sectors
                line = file.ReadLine();
                averageSectors = ParseFloatList(line);

                // Line 3: Best Lap Sectors
                line = file.ReadLine();
                bestLapSectors = ParseFloatList(line);

                // Line 4: Best Sectors
                line = file.ReadLine();
                bestSectors = ParseFloatList(line);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error reading lap times file: " + ex.Message);
            lapTimes = new List<float>();
            InitializeSectorLists();  // fallback to defaults
        }

        // Ensure lists are valid lengths
        PadOrInitializeSectors(ref averageSectors);
        PadOrInitializeSectors(ref bestLapSectors, float.MaxValue);
        PadOrInitializeSectors(ref bestSectors, float.MaxValue);
    }

    private void updateOptLap()
    {
        float newOptLap = 0f;
        if (bestSectors.Count == 0)
        {
            optLapTime = float.MaxValue;
            return;
        }
        for (int i = 0; i < bestSectors.Count; i++)
        {
            newOptLap += bestSectors[i];
        }

        optLapTime = newOptLap;
    }

    // Update is called once per frame
    void Update()
    {
        if (helicopterTransform == null || waypoints.Length == 0) return;

        if (startingPosition == null || startingPosition == Vector3.zero)
        {
            startingPosition = helicopterTransform.position;
        }

        // if the distance from the start point to the helicopter is greater than 3, start the lap
        if (Vector3.Distance(helicopterTransform.position, startingPosition) > 3 && goLock == false)
        {
            lapActive = true;
        }

        if (lapActive)
        {
            currentLapTime += Time.deltaTime;
            currentSectorTime += Time.deltaTime;

            for (int i = 0; i < timingManagers.Length; i++)
            {
                // update the current lap time in minuites and seconds
                timingManagers[i].updateCurrentLapTime(currentLapTime);

                // update the current sector time and difference
                timingManagers[i].updateSectorTime(currentSector, currentSectorTime);
            }
        }

        PublishPlayerProgress();

        // on spacebar press go next waypoint
        if (Input.GetKeyDown(KeyCode.Space))
        {
            finishWaypoint(currentWaypoint + 1);
        }
    }

    private void ResetLapDataToDefault()
    {
        currentLapTime = 0f;

        currentLapSectors = new List<float>();
        for (int i = 0; i < _sectors; i++)
        {
            currentLapSectors.Add(0);
        }

        currentSector = 0;

        currentWaypoint = 0;

        currentSectorTime = 0;

        getBestLap();
        updateOptLap();

        averageLap = 0;
        for (int i = 0; i < averageSectors.Count; i++)
        {
            averageLap += averageSectors[i];
        }


        // Reset the checkpoints
        for (int i = 0; i < waypoints.Length; i++)
        {
            var waypoint = waypoints[i].GetComponentInChildren<Waypoint>();
            if (waypoint != null)
            {
                waypoint.resetCheckpoint();
            }
            waypoints[i].SetActive(false);
        }

        // Set the first 3 waypoints active
        for (int i = 0; i < 3 && i < waypoints.Length; i++)
        {
            waypoints[i].SetActive(true);
        }


        // Set the first waypoint as the next
        if (waypoints.Length > 0)
        {
            var firstWaypoint = waypoints[0].GetComponentInChildren<Waypoint>();
            if (firstWaypoint != null)
            {
                firstWaypoint.setAsNext();
            }
        }

        lapActive = false;

        updateDisplayTimes(false, true);
        lastLapWasNewBest = false;
        PublishPlayerProgress();
    }
    
    private List<float> ParseFloatList(string line)
    {
        List<float> result = new List<float>();
        if (string.IsNullOrWhiteSpace(line)) return result;

        string[] parts = line.Split(',');
        foreach (var part in parts)
        {
            if (float.TryParse(part.Trim(), out float value))
            {
                result.Add(value);
            }
        }
        return result;
    }

    private void PadOrInitializeSectors(ref List<float> list, float defaultValue = 0f)
    {
        if (list == null)
            list = new List<float>();

        while (list.Count < _sectors)
            list.Add(defaultValue);

        if (list.Count > _sectors)
            list.RemoveRange(_sectors, list.Count - _sectors);
    }

    private void InitializeSectorLists()
    {
        averageSectors = CreateFilledList(_sectors, 0f);
        bestLapSectors = CreateFilledList(_sectors, float.MaxValue);
        bestSectors = CreateFilledList(_sectors, float.MaxValue);
    }

    private static List<float> CreateFilledList(int count, float value)
    {
        var list = new List<float>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(value);
        }
        return list;
    }

    private void LogDebug(string message)
    {
        if (!debugLogsEnabled)
        {
            return;
        }

        Debug.Log($"[WaypointManager] {message}");
    }

    private void ResolvePlayerState()
    {
        if (playerTimeTrialState != null)
        {
            return;
        }

        if (helicopterTransform != null)
        {
            playerTimeTrialState = helicopterTransform.GetComponentInParent<PlayerTimeTrialState>();
            if (playerTimeTrialState == null)
            {
                playerTimeTrialState = helicopterTransform.GetComponentInChildren<PlayerTimeTrialState>();
            }
        }

    }

    private void PublishPlayerProgress()
    {
        ResolvePlayerState();
        if (playerTimeTrialState == null)
        {
            return;
        }

        playerTimeTrialState.PublishLocalProgress(currentWaypoint, currentSector, currentLapTime, currentSectorTime, lapActive, currentLapSectors);
        PublishPlayerComparisons(lastLapWasNewBest);
    }

    private void PublishPlayerComparisons(bool newBest)
    {
        ResolvePlayerState();
        if (playerTimeTrialState == null)
        {
            return;
        }

        float normalizedBestLap = bestLap >= float.MaxValue * 0.5f ? 0f : bestLap;
        float normalizedOptLap = optLapTime >= float.MaxValue * 0.5f ? 0f : optLapTime;

        playerTimeTrialState.PublishComparisonData(
            normalizedBestLap,
            normalizedOptLap,
            lastLapTime,
            lastLapDiffTime,
            averageLap,
            newBest,
            averageSectors,
            bestSectors,
            bestLapSectors);
    }

    private void RegisterTimingManagersForLoadedPlayer()
    {
        var managers = new List<Timing>();

        if (timingManagers != null)
        {
            for (int i = 0; i < timingManagers.Length; i++)
            {
                if (timingManagers[i] != null && !managers.Contains(timingManagers[i]))
                {
                    managers.Add(timingManagers[i]);
                }
            }
        }

        if (helicopterTransform != null)
        {
            var fromHelicopter = helicopterTransform.GetComponentsInChildren<Timing>(true);
            for (int i = 0; i < fromHelicopter.Length; i++)
            {
                if (fromHelicopter[i] != null && !managers.Contains(fromHelicopter[i]))
                {
                    managers.Add(fromHelicopter[i]);
                }
            }
        }

        if (managers.Count == 0)
        {
            var allTimingManagers = FindObjectsByType<Timing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allTimingManagers.Length; i++)
            {
                if (allTimingManagers[i] != null && !managers.Contains(allTimingManagers[i]))
                {
                    managers.Add(allTimingManagers[i]);
                }
            }
        }

        timingManagers = managers.ToArray();
    }

}
