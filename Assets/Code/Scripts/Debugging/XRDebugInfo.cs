using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

public class XRDebugInfo : MonoBehaviour
{
    private IEnumerator Start()
    {
        XRManagerSettings manager =
            XRGeneralSettings.Instance?.Manager;

        if (manager == null)
        {
            Debug.LogError("XR Manager Settings were not found.");
            yield break;
        }

        Debug.Log(
            $"XR automatic loading: " +
            $"{XRGeneralSettings.Instance.InitManagerOnStart}"
        );

        // Only manually initialise when automatic startup is disabled.
        if (!XRGeneralSettings.Instance.InitManagerOnStart &&
            manager.activeLoader == null)
        {
            Debug.Log("Manually initialising XR loader...");

            yield return manager.InitializeLoader();

            if (manager.activeLoader == null)
            {
                Debug.LogError(
                    "XR loader initialisation failed. " +
                    "Check that OpenXR is enabled for Standalone, " +
                    "an OpenXR runtime is installed, and an HMD is available."
                );

                LogConfiguredLoaders(manager);
                yield break;
            }

            manager.StartSubsystems();
        }

        // Give automatically started subsystems a few frames.
        for (int i = 0; i < 10 && manager.activeLoader == null; i++)
            yield return null;

        Debug.Log(
            "Active XR Loader: " +
            (manager.activeLoader != null
                ? manager.activeLoader.name
                : "NULL")
        );

        LogConfiguredLoaders(manager);
        LogSubsystems();

        Debug.Log($"XRSettings.enabled: {XRSettings.enabled}");
        Debug.Log(
            $"XRSettings.loadedDeviceName: " +
            $"'{XRSettings.loadedDeviceName}'"
        );

        LogExtension("XR_META_performance_metrics");
        LogExtension("XR_KHR_D3D11_enable");
        LogExtension("XR_KHR_D3D12_enable");
        LogExtension("XR_EXT_user_presence");
    }

    private static void LogConfiguredLoaders(
        XRManagerSettings manager)
    {
        Debug.Log(
            $"Configured loader count: " +
            $"{manager.loaders.Count}"
        );

        foreach (XRLoader loader in manager.loaders)
        {
            Debug.Log(
                $"Configured XR Loader: " +
                $"{(loader != null ? loader.name : "NULL")}"
            );
        }
    }

    private static void LogSubsystems()
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);

        Debug.Log($"XR display subsystem count: {displays.Count}");

        foreach (XRDisplaySubsystem display in displays)
        {
            Debug.Log(
                $"XR Display: {display.subsystemDescriptor.id}, " +
                $"running={display.running}"
            );
        }

        var inputs = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(inputs);

        Debug.Log($"XR input subsystem count: {inputs.Count}");

        foreach (XRInputSubsystem input in inputs)
        {
            Debug.Log(
                $"XR Input: {input.subsystemDescriptor.id}, " +
                $"running={input.running}"
            );
        }
    }

    private static void LogExtension(string extensionName)
    {
        bool enabled = OpenXRRuntime.IsExtensionEnabled(extensionName);

        Debug.Log(
            $"OpenXR extension '{extensionName}' enabled: {enabled}"
        );
    }
}