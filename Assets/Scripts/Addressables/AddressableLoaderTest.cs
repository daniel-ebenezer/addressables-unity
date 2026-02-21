using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets.ResourceLocators;
using System.Threading.Tasks;
using UnityEngine.Profiling;
using TMPro;
using System.Collections.Generic;

public class AddressableLoaderTest : MonoBehaviour
{
    [Header("Addresses - must match exactly in Addressables Groups")]
    [SerializeField] private string remoteAddress = "Rifles/Rifle2.prefab";          // CCD / remote version
    [SerializeField] private string localFallbackAddress = "Rifles/Rifle2.prefab";   // Built-in local fallback

    [Header("UI References - assign in Inspector")]
    [SerializeField] private TextMeshProUGUI txtStatus;
    [SerializeField] private TextMeshProUGUI txtTime;
    [SerializeField] private TextMeshProUGUI txtMemory;
    [SerializeField] private TextMeshProUGUI txtSource;
    [SerializeField] private TextMeshProUGUI txtError;

    private async void Start()
    {
        ResetUI();
        UpdateUIStatus("Starting...");

        long startMemory = Profiler.GetTotalAllocatedMemoryLong();
        float startTime = Time.realtimeSinceStartup;

        Debug.Log($"[START] Attempting remote load from CCD: {remoteAddress}");

        // 1. Initialize Addressables
        var initHandle = Addressables.InitializeAsync(autoReleaseHandle: false);
        bool initOK = false;

        try
        {
            await initHandle.Task;

            if (initHandle.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log("[INIT] Success - Addressables initialized");
                initOK = true;
                UpdateUIStatus("Initialized");
            }
            else
            {
                Debug.LogWarning($"[INIT] Status: {initHandle.Status}");
                UpdateUIStatus("Init failed");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[INIT] Exception: {ex.Message}");
            UpdateUIStatus("Init error");
            UpdateUIError(ex.Message);
        }

        Addressables.Release(initHandle);

        // 2. Try remote load
        await TryLoadRemote(startTime, startMemory);
    }

    private async Task TryLoadRemote(float startTime, long startMemory)
    {
        AsyncOperationHandle<GameObject> handle = default;

        try
        {
            Debug.Log($"[REMOTE TRY] Address: {remoteAddress} | Time: {Time.realtimeSinceStartup:F3}s");
            UpdateUIStatus("Loading from CCD...");

            handle = Addressables.LoadAssetAsync<GameObject>(remoteAddress);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var loadedPrefab = handle.Result;

                float duration = Time.realtimeSinceStartup - startTime;
                long endMemory = Profiler.GetTotalAllocatedMemoryLong();
                long deltaKB = (endMemory - startMemory) / 1024;

                Debug.Log($"[REMOTE SUCCESS] Loaded prefab name: {loadedPrefab.name} | Address: {remoteAddress}");
                Debug.Log($"[REMOTE SUCCESS] Load time: {duration:F3}s | Memory delta: +{deltaKB:F1} KB");

                UpdateUIStatus("Success - CCD Remote");
                UpdateUITime($"Load Time: {duration:F3} s");
                UpdateUIMemory($"Memory Delta: +{deltaKB:F1} KB");
                UpdateUISource("Source: CCD Remote");

                var instance = Instantiate(loadedPrefab, Vector3.zero, Quaternion.identity);
                instance.name = $"Rifle2_CCD_{loadedPrefab.name}";

                Debug.Log($"[INSTANTIATE] Remote instance created: {instance.name} (prefab source: {loadedPrefab.name})");

                var releaser = instance.AddComponent<AddressableReleaser>();
                releaser.SetHandle(handle);

                CheckMeshAndMaterial(instance, "CCD");
            }
            else
            {
                Debug.LogError($"[REMOTE FAIL] Status: {handle.Status} | {handle.OperationException?.Message}");
                UpdateUIStatus("Remote failed – using local");
                UpdateUIError(handle.OperationException?.Message ?? "Unknown");

                await TryLoadLocal(startTime, startMemory);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[REMOTE EXCEPTION] {ex.Message}");
            UpdateUIStatus("Remote error – using local");
            UpdateUIError(ex.Message);

            await TryLoadLocal(startTime, startMemory);
        }
    }

    private async Task TryLoadLocal(float startTime, long startMemory)
    {
        AsyncOperationHandle<GameObject> handle = default;

        try
        {
            Debug.Log($"[LOCAL TRY] Address: {localFallbackAddress} | Time: {Time.realtimeSinceStartup:F3}s");
            UpdateUIStatus("Loading local fallback...");

            handle = Addressables.LoadAssetAsync<GameObject>(localFallbackAddress);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var loadedPrefab = handle.Result;

                float duration = Time.realtimeSinceStartup - startTime;
                long endMemory = Profiler.GetTotalAllocatedMemoryLong();
                long deltaKB = (endMemory - startMemory) / 1024;

                Debug.Log($"[LOCAL SUCCESS] Loaded prefab name: {loadedPrefab.name} | Address: {localFallbackAddress}");
                Debug.Log($"[LOCAL SUCCESS] Load time: {duration:F3}s | Memory delta: +{deltaKB:F1} KB");

                UpdateUIStatus("Success - Local Fallback");
                UpdateUITime($"Load Time: {duration:F3} s");
                UpdateUIMemory($"Memory Delta: +{deltaKB:F1} KB");
                UpdateUISource("Source: Local Fallback");

                var instance = Instantiate(loadedPrefab, Vector3.zero, Quaternion.identity);
                instance.name = $"Rifle2_Local_{loadedPrefab.name}";

                Debug.Log($"[INSTANTIATE] Local fallback instance created: {instance.name} (prefab source: {loadedPrefab.name})");

                var releaser = instance.AddComponent<AddressableReleaser>();
                releaser.SetHandle(handle);

                CheckMeshAndMaterial(instance, "LOCAL FALLBACK");
            }
            else
            {
                Debug.LogError($"[LOCAL FAIL] {handle.OperationException?.Message}");
                UpdateUIStatus("All loads failed");
                UpdateUIError(handle.OperationException?.Message ?? "Unknown");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LOCAL EXCEPTION] {ex.Message}");
            UpdateUIStatus("All loads failed");
            UpdateUIError(ex.Message);
        }
    }

    private void CheckMeshAndMaterial(GameObject instance, string source)
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

    private void ResetUI()
    {
        UpdateUIStatus("Initializing...");
        UpdateUITime("Load Time: -");
        UpdateUIMemory("Memory Delta: -");
        UpdateUISource("Source: -");
        UpdateUIError("");
    }

    private void UpdateUIStatus(string msg) => SafeSetText(txtStatus, msg);
    private void UpdateUITime(string msg) => SafeSetText(txtTime, msg);
    private void UpdateUIMemory(string msg) => SafeSetText(txtMemory, msg);
    private void UpdateUISource(string msg) => SafeSetText(txtSource, msg);
    private void UpdateUIError(string msg) => SafeSetText(txtError, msg);

    private void SafeSetText(TextMeshProUGUI text, string msg)
    {
        if (text != null) text.text = msg;
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