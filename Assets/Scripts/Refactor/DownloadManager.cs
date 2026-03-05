using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Threading.Tasks;
using System.Collections.Generic;

public static class DownloadManager
{
    public static async Task ClearAllAsync()
    {
        await Addressables.ClearDependencyCacheAsync(new List<object>(), true).Task;

        Addressables.ClearResourceLocators();
    }
}