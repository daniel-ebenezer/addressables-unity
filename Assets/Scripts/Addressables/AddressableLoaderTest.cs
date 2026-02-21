using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using UnityEngine.Profiling;

public class AddressableLoaderTest : MonoBehaviour
{
    [Header("Addresses - must match exactly in Addressables Groups")]
    [SerializeField] private string remoteAddress = "Rifles/Rifle2.prefab";      // From Remote_DLC_Rifles group (CCD)
    [SerializeField] private string localFallbackAddress = "Rifles/Rifle2.prefab"; // Same address in Default Local Group

    //[Header("Debug / UI (optional - add Text component later)")]
    //[SerializeField] private UnityEngine.UI.Text statusText;

    private async void Start()
    {
        long startMemory = Profiler.GetTotalAllocatedMemoryLong();
        float startTime = Time.realtimeSinceStartup;

        Debug.Log($"[TEST START] Attempting remote load from CCD: {remoteAddress}");

        // 1. Initialize Addressables (downloads remote catalog from CCD if needed)
        var initHandle = Addressables.InitializeAsync(autoReleaseHandle: false);

        bool initOK = false;

        try
        {
            await initHandle.Task;

            if (initHandle.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log("[INIT] Success - CCD remote catalog loaded.");
                initOK = true;
            }
            else
            {
                Debug.LogWarning($"[INIT] Completed but status: {initHandle.Status}. " +
                                 $"Details: {initHandle.OperationException?.Message}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[INIT] Exception: {ex.Message}\nStack: {ex.StackTrace}");
        }

        Addressables.Release(initHandle);

        if (!initOK)
        {
            Debug.LogWarning("[INIT] Issues detected - attempting load anyway (may fallback)");
        }

        // 2. Try remote load first (CCD)
        AsyncOperationHandle<GameObject> remoteHandle = default;

        try
        {
            Debug.Log($"[REMOTE] Loading address: {remoteAddress}");
            remoteHandle = Addressables.LoadAssetAsync<GameObject>(remoteAddress);

            await remoteHandle.Task;

            if (remoteHandle.Status == AsyncOperationStatus.Succeeded)
            {
                float duration = Time.realtimeSinceStartup - startTime;
                long endMemory = Profiler.GetTotalAllocatedMemoryLong();
                long memoryDeltaKB = (endMemory - startMemory) / 1024;

                Debug.Log($"[SUCCESS - REMOTE/CCD] Loaded {remoteAddress} in {duration:F3} seconds. " +
                          $"Memory delta: {memoryDeltaKB:F1} KB");

                // Instantiate and keep handle alive
                var prefab = remoteHandle.Result;
                var instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
                instance.name = "Rifle2_Remote_CCD";

                // Attach releaser so we release only when destroyed
                var releaser = instance.AddComponent<AddressableReleaser>();
                releaser.SetHandle(remoteHandle);

                // Debug check for mesh
                CheckMesh(instance, "REMOTE");
            }
            else
            {
                Debug.LogError($"[REMOTE FAIL] {remoteHandle.OperationException?.Message ?? "Unknown"}");
                await TryLocalFallback(startTime, startMemory);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[REMOTE EXCEPTION] {ex.Message}");
            await TryLocalFallback(startTime, startMemory);
        }
        // DO NOT release remoteHandle here - releaser component handles it
    }

    private async Task TryLocalFallback(float originalStartTime, long originalStartMemory)
    {
        AsyncOperationHandle<GameObject> localHandle = default;

        try
        {
            Debug.Log($"[FALLBACK] Trying local address: {localFallbackAddress}");
            localHandle = Addressables.LoadAssetAsync<GameObject>(localFallbackAddress);

            await localHandle.Task;

            if (localHandle.Status == AsyncOperationStatus.Succeeded)
            {
                float duration = Time.realtimeSinceStartup - originalStartTime;
                long endMemory = Profiler.GetTotalAllocatedMemoryLong();
                long memoryDeltaKB = (endMemory - originalStartMemory) / 1024;

                Debug.Log($"[SUCCESS - LOCAL FALLBACK] Loaded in {duration:F3} seconds. " +
                          $"Memory delta: {memoryDeltaKB:F1} KB");

                var prefab = localHandle.Result;
                var instance = Instantiate(prefab, Vector3.zero, Quaternion.identity);
                instance.name = "Rifle2_Local_Fallback";

                var releaser = instance.AddComponent<AddressableReleaser>();
                releaser.SetHandle(localHandle);

                CheckMesh(instance, "LOCAL FALLBACK");
            }
            else
            {
                Debug.LogError($"[LOCAL FAIL] {localHandle.OperationException?.Message ?? "Unknown"}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LOCAL EXCEPTION] {ex.Message}");
        }
        // Releaser component will release localHandle when instance is destroyed
    }

    private void CheckMesh(GameObject instance, string source)
    {
        var meshFilter = instance.GetComponentInChildren<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Debug.Log($"[{source}] Mesh OK: {meshFilter.sharedMesh.name}");
        }
        else
        {
            Debug.LogWarning($"[{source}] Mesh MISSING or null MeshFilter!");
        }

        // Optional: Check material (pink = shader issue)
        var renderer = instance.GetComponentInChildren<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            Debug.Log($"[{source}] Material OK: {renderer.sharedMaterial.name}");
        }
        else
        {
            Debug.LogWarning($"[{source}] Material MISSING or pink shader issue!");
        }
    }
}

public class AddressableReleaser : MonoBehaviour
{
    private AsyncOperationHandle<GameObject> handle;

    public void SetHandle(AsyncOperationHandle<GameObject> h)
    {
        handle = h;
    }

    private void OnDestroy()
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
            Debug.Log($"[RELEASER] Released handle for {gameObject.name} on destroy.");
        }
    }
}