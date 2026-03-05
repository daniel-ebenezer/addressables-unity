using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;

public static class LoadManager
{
    public static async Task<GameObject> InstantiateSafeAsync(string address, Vector3 pos, Quaternion rot)
    {
        var handle = Addressables.InstantiateAsync(address, pos, rot, null, true);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            var instance = handle.Result;
            var releaser = instance.AddComponent<AddressableReleaser>();
            releaser.SetHandle(handle);
            return instance;
        }

        return null;
    }
}