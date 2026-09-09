using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A small, encapsulated service-locator style registry that lets otherwise-unrelated
/// systems (camera rig, telemetry, multiplayer setup, etc.) look up well-known
/// GameObjects (e.g. "CameraTarget") by name without needing a direct scene reference.
///
/// The backing dictionary is intentionally private: callers must use Register/Unregister/TryGet
/// so entries are always validated and so registrants can clean up after themselves
/// (see <see cref="GameObjectAssigner"/>) instead of leaving stale references behind.
/// </summary>
public static class GameObjectTarget
{
    private static readonly Dictionary<string, GameObject> _targets = new Dictionary<string, GameObject>();

    /// <summary>Registers (or replaces) the GameObject associated with <paramref name="key"/>.</summary>
    public static void Register(string key, GameObject value)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("GameObjectTarget.Register called with a null or empty key; ignoring.");
            return;
        }

        _targets[key] = value;
    }

    /// <summary>Removes the entry for <paramref name="key"/> if it currently points at <paramref name="value"/>.</summary>
    public static void Unregister(string key, GameObject value)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (_targets.TryGetValue(key, out var existing) && existing == value)
        {
            _targets.Remove(key);
        }
    }

    /// <summary>Attempts to find the GameObject registered under <paramref name="key"/>.</summary>
    public static bool TryGet(string key, out GameObject value)
    {
        if (string.IsNullOrEmpty(key))
        {
            value = null;
            return false;
        }

        return _targets.TryGetValue(key, out value) && value != null;
    }

    /// <summary>Clears every registered entry. Intended for scene/test teardown.</summary>
    public static void Clear()
    {
        _targets.Clear();
    }
}
