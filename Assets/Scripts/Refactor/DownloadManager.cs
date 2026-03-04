using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Threading.Tasks;
using System.Collections.Generic;

public static class DownloadManager
{
    public static async Task ClearAllAsync()
    {
        Debug.Log("[DOWNLOAD MGR] Clearing dependency cache...");
        await Addressables.ClearDependencyCacheAsync(new List<object>(), true).Task;

        Debug.Log("[DOWNLOAD MGR] Clearing resource locators...");
        Addressables.ClearResourceLocators();
    }
}