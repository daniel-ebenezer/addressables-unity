using UnityEngine;
using System.Collections.Generic;

public class AddressableService : MonoBehaviour
{
    public static AddressableService Instance { get; private set; }

    [SerializeField] private AddressablesConfig configAsset;

    private Dictionary<string, string> categorySuffixCache = new();
    private Dictionary<string, (string addrA, string addrB, string lblA, string lblB)> abCache = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (configAsset == null)
        {
            Debug.LogError("AddressablesConfig is missing!");
            return;
        }

        InitializeCache();
    }

    private void InitializeCache()
    {
        categorySuffixCache.Clear();
        foreach (var cat in configAsset.categories)
        {
            categorySuffixCache[cat.categoryName] = cat.addressSuffix;
        }

        abCache.Clear();
        foreach (var v in configAsset.abVariants)
        {
            abCache[v.baseKey] = (v.remoteAddressA, v.remoteAddressB, v.labelA, v.labelB);
        }
    }

    public string GetRemoteAddress(string baseKey, string categoryName)
    {
        if (categorySuffixCache.TryGetValue(categoryName, out var suffix))
            return baseKey + suffix;

        return baseKey + configAsset.defaultAddressSuffix;
    }

    public (string address, string label) GetVariantInfo(string baseKey, bool isA)
    {
        if (abCache.TryGetValue(baseKey, out var info))
        {
            return isA ? (info.addrA, info.lblA) : (info.addrB, info.lblB);
        }

        string defaultAddr = baseKey + configAsset.defaultAddressSuffix;
        string defaultLabel = isA ? configAsset.defaultLabelA : configAsset.defaultLabelB;
        return (defaultAddr, defaultLabel);
    }
}