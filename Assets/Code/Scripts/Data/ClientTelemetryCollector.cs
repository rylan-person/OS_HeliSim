using System;
using System.Collections.Generic;
using Oyedoyin.RotaryWing;
using Unity.Netcode;
using UnityEngine;

public enum TelemetrySeries
{
    XJoy,
    YJoy,
    RotationPitch,
    RotationYaw,
    RotationRoll
}

public class ClientTelemetryCollector : NetworkBehaviour
{
    public struct Sample
    {
        public float time;
        public float value;
    }

    [Header("Sources")]
    public RotaryComputer rotaryComputer;
    public Transform rotationTarget;

    [Header("Collection")]
    [Tooltip("Retention for all collected telemetry values.")]
    public float retentionSeconds = 20f;

    [Tooltip("Samples taken per second.")]
    public int samplesPerSecond = 60;

    [Header("Activation")]
    [Tooltip("Automatically make this source active on the local owning player.")]
    public bool autoAssignForLocalPlayer = true;

    public static ClientTelemetryCollector ActiveCollector { get; private set; }
    public static event Action<ClientTelemetryCollector> ActiveCollectorChanged;

    private readonly List<Sample> xJoySamples = new List<Sample>();
    private readonly List<Sample> yJoySamples = new List<Sample>();
    private readonly List<Sample> pitchSamples = new List<Sample>();
    private readonly List<Sample> yawSamples = new List<Sample>();
    private readonly List<Sample> rollSamples = new List<Sample>();

    private float sampleInterval;
    private float sampleTimer;
    private bool hasAutoAssigned;

    private readonly NetworkVariable<float> netXJoy = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> netYJoy = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> netPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> netYaw = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<float> netRoll = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        if (rotaryComputer == null)
        {
            rotaryComputer = GetComponentInChildren<RotaryComputer>(true);
            if (rotaryComputer == null)
            {
                rotaryComputer = GetComponentInParent<RotaryComputer>();
            }
        }

        if (rotationTarget == null)
        {
            if (rotaryComputer != null)
            {
                rotationTarget = rotaryComputer.transform;
            }
            else
            {
                rotationTarget = transform;
            }
        }

        sampleInterval = 1f / Mathf.Max(1, samplesPerSecond);
        sampleTimer = 0f;
        hasAutoAssigned = false;
    }

    private void Start()
    {
        TryAutoAssignAsActive();
    }

    private void Update()
    {
        if (!hasAutoAssigned && (ActiveCollector == null || ActiveCollector == this))
        {
            TryAutoAssignAsActive();
        }

        float dt = Time.deltaTime;
        sampleTimer += dt;

        while (sampleTimer >= sampleInterval)
        {
            sampleTimer -= sampleInterval;
            AddSample(Time.time);
        }

        TrimOldSamples(Time.time - Mathf.Max(0.1f, retentionSeconds));
    }

    public IReadOnlyList<Sample> GetSamples(TelemetrySeries series)
    {
        switch (series)
        {
            case TelemetrySeries.XJoy:
                return xJoySamples;
            case TelemetrySeries.YJoy:
                return yJoySamples;
            case TelemetrySeries.RotationPitch:
                return pitchSamples;
            case TelemetrySeries.RotationYaw:
                return yawSamples;
            case TelemetrySeries.RotationRoll:
                return rollSamples;
            default:
                return xJoySamples;
        }
    }

    public static void SetActiveCollector(ClientTelemetryCollector collector)
    {
        if (ActiveCollector == collector)
        {
            return;
        }

        ActiveCollector = collector;
        ActiveCollectorChanged?.Invoke(ActiveCollector);
    }

    public static void SetActiveCollectorFromTransform(Transform target)
    {
        if (target == null)
        {
            if (ActiveCollector != null)
            {
                SetActiveCollector(null);
            }

            return;
        }

        var collector = target.GetComponentInChildren<ClientTelemetryCollector>();
        if (collector == null)
        {
            collector = target.GetComponentInParent<ClientTelemetryCollector>();
        }

        if (collector != null || ActiveCollector != null)
        {
            SetActiveCollector(collector);
        }
    }

    private void AddSample(float now)
    {
        bool useNetworkReplication = IsSpawned;

        if (useNetworkReplication && IsOwner)
        {
            float ownerXJoy = rotaryComputer != null ? (float)rotaryComputer.m_roll : 0f;
            float ownerYJoy = rotaryComputer != null ? (float)rotaryComputer.m_pitch : 0f;
            Vector3 ownerEuler = rotationTarget != null ? rotationTarget.eulerAngles : Vector3.zero;

            float ownerPitch = NormalizeAngle(ownerEuler.x);
            float ownerYaw = NormalizeAngle(ownerEuler.y);
            float ownerRoll = NormalizeAngle(ownerEuler.z);

            netXJoy.Value = ownerXJoy;
            netYJoy.Value = ownerYJoy;
            netPitch.Value = ownerPitch;
            netYaw.Value = ownerYaw;
            netRoll.Value = ownerRoll;

            AddToSeries(xJoySamples, now, ownerXJoy);
            AddToSeries(yJoySamples, now, ownerYJoy);
            AddToSeries(pitchSamples, now, ownerPitch);
            AddToSeries(yawSamples, now, ownerYaw);
            AddToSeries(rollSamples, now, ownerRoll);
            return;
        }

        if (useNetworkReplication)
        {
            AddToSeries(xJoySamples, now, netXJoy.Value);
            AddToSeries(yJoySamples, now, netYJoy.Value);
            AddToSeries(pitchSamples, now, netPitch.Value);
            AddToSeries(yawSamples, now, netYaw.Value);
            AddToSeries(rollSamples, now, netRoll.Value);
            return;
        }

        float localXJoy = rotaryComputer != null ? (float)rotaryComputer.m_roll : 0f;
        float localYJoy = rotaryComputer != null ? (float)rotaryComputer.m_pitch : 0f;
        Vector3 localEuler = rotationTarget != null ? rotationTarget.eulerAngles : Vector3.zero;

        AddToSeries(xJoySamples, now, localXJoy);
        AddToSeries(yJoySamples, now, localYJoy);
        AddToSeries(pitchSamples, now, NormalizeAngle(localEuler.x));
        AddToSeries(yawSamples, now, NormalizeAngle(localEuler.y));
        AddToSeries(rollSamples, now, NormalizeAngle(localEuler.z));
    }

    private void TrimOldSamples(float cutoff)
    {
        TrimSeries(xJoySamples, cutoff);
        TrimSeries(yJoySamples, cutoff);
        TrimSeries(pitchSamples, cutoff);
        TrimSeries(yawSamples, cutoff);
        TrimSeries(rollSamples, cutoff);
    }

    private static void AddToSeries(List<Sample> samples, float time, float value)
    {
        samples.Add(new Sample { time = time, value = value });
    }

    private static void TrimSeries(List<Sample> samples, float cutoff)
    {
        while (samples.Count > 0 && samples[0].time < cutoff)
        {
            samples.RemoveAt(0);
        }
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    private void TryAutoAssignAsActive()
    {
        if (!autoAssignForLocalPlayer)
        {
            return;
        }

        var networkObject = GetComponentInParent<NetworkObject>();
        if (networkObject != null)
        {
            if (!networkObject.IsSpawned)
            {
                var networkManager = NetworkManager.Singleton;
                bool netcodeNotRunning = networkManager == null || !networkManager.IsListening;

                if (netcodeNotRunning && ActiveCollector == null)
                {
                    SetActiveCollector(this);
                    hasAutoAssigned = true;
                }

                return;
            }

            if (networkObject.IsOwner)
            {
                SetActiveCollector(this);
                hasAutoAssigned = true;
            }

            return;
        }

        if (ActiveCollector == null)
        {
            SetActiveCollector(this);
            hasAutoAssigned = true;
        }
    }
}