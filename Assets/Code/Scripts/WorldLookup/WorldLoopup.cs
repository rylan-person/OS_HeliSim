using System.Collections.Generic;
using UnityEngine;

public class WorldLookup : MonoBehaviour
{
    // Singleton instance
    public static WorldLookup Instance { get; private set; }

    [Header("Lookup Entries")]
    [SerializeField] private List<WorldLookupEntry> lookupEntries = new();
    private Dictionary<WorldLookupType, GameObject> lookupDictionary;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple instances of WorldLookup detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildLookupDictionary();
    }

    private void BuildLookupDictionary()
    {
        lookupDictionary = new Dictionary<WorldLookupType, GameObject>();

        foreach (WorldLookupEntry entry in lookupEntries)
        {
            if (entry.prefab == null)
            {
                Debug.LogWarning($"Lookup entry for {entry.lookupType} has no prefab assigned.");
                continue;
            }

            if (lookupDictionary.ContainsKey(entry.lookupType))
            {
                Debug.LogWarning($"Duplicate lookup type found: {entry.lookupType}");
                continue;
            }

            lookupDictionary[entry.lookupType] = entry.prefab;
        }
    }

    public GameObject GetPrefab(WorldLookupType lookupType)
    {
        if (lookupDictionary.TryGetValue(lookupType, out GameObject prefab))
        {
            return prefab;
        }
        Debug.LogWarning($"Prefab not found for lookup type: {lookupType}");
        return null;
    }
}