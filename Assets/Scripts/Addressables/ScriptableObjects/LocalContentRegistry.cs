using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LocalContentRegistry", menuName = "ContentManagement/Local Content Registry", order = 1)]
public class LocalContentRegistry : ScriptableObject
{       
    [Serializable]
public class Entry
{
    public string RemoteKey = "";
    public string Category = ""; // optional
    // No GameObject field anymore
}

    [Header("All local content that can be upgraded via remote DLC")]
    public List<Entry> entries = new List<Entry>();

#if UNITY_EDITOR
    // Optional: Quick validation button in Inspector
    [ContextMenu("Validate Entries")]
    private void Validate()
    {
        int invalid = 0;
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.RemoteKey))
            {
                Debug.LogWarning($"Invalid entry: {entry.RemoteKey}", this);
                invalid++;
            }
        }
        Debug.Log($"Validation: {entries.Count} entries, {invalid} invalid", this);
    }
#endif
}