using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using UnityEngine.Profiling;
using TMPro;
using System;
using System.Collections.Generic;

public class AddressableLoaderTest : MonoBehaviour
{
    [Header("=== Managers & Containers ===")]
    [SerializeField] private ContentSpawner contentSpawner;

    [SerializeField, Tooltip("Parent object containing ALL local upgradable objects (vehicles, trees, trash, etc.)")]
    private Transform localContentParent;

    [Header("=== Vehicle Upgrade Configuration ===")]
    [SerializeField] private List<string> remoteVehicleKeys = new List<string>
    {
        "Vehicles/Sedan",
        "Vehicles/Truck"
        // Add more base keys here
    };

    [Header("=== Tree Upgrade Configuration ===")]
    [SerializeField] private List<string> remoteTreeKeys = new List<string>
    {
        "Nature/Tree_Pine",
        "Nature/Tree_Oak"
        // Add your tree base keys
    };

    [Header("=== Props Upgrade Configuration ===")]
    [SerializeField] private List<string> remotePropKeys = new List<string>
    {
        "Props/TrashCan"
        // Add more props base keys
    };

    [Header("Retry / Timeout / Resilience")]
    [SerializeField] private int maxRetryAttempts = 3;
    [SerializeField] private float baseRetryDelaySec = 2f;
    [SerializeField] private float timeoutPerAttemptSec = 15f;

    [Header("UI Elements - assign all in Inspector!")]
    [SerializeField] private TextMeshProUGUI txtStatus;
    [SerializeField] private TextMeshProUGUI txtTime;
    [SerializeField] private TextMeshProUGUI txtMemory;
    [SerializeField] private TextMeshProUGUI txtSource;
    [SerializeField] private TextMeshProUGUI txtError;
    [SerializeField] private TextMeshProUGUI txtProgress;

    private readonly Dictionary<string, List<GameObject>> localLookup = new();

    private void Awake()
    {
        if (contentSpawner == null)
        {
            contentSpawner = GameObject.FindAnyObjectByType<ContentSpawner>();
            if (contentSpawner == null)
            {
                Debug.LogError("ContentSpawner missing - assign reference or add to scene");
            }
        }

        if (localContentParent == null)
        {
            Debug.LogError("LocalContentParent not assigned! Drag 'LocalContentContainer' here.");
            return;
        }

        // Early UI validation
        if (txtStatus == null) Debug.LogError("txtStatus is NULL - assign in Inspector!");
        if (txtTime == null) Debug.LogError("txtTime is NULL - assign in Inspector!");
        if (txtMemory == null) Debug.LogError("txtMemory is NULL - assign in Inspector!");
        if (txtSource == null) Debug.LogError("txtSource is NULL - assign in Inspector!");
        if (txtError == null) Debug.LogError("txtError is NULL - assign in Inspector!");
        if (txtProgress == null) Debug.LogError("txtProgress is NULL - assign in Inspector!");

        BuildLocalLookup();
    }

    private void BuildLocalLookup()
    {
        localLookup.Clear();

        var markers = localContentParent.GetComponentsInChildren<LocalContentMarker>(true);

        foreach (var marker in markers)
        {
            if (marker == null || string.IsNullOrEmpty(marker.RemoteKey)) continue;

            if (!localLookup.TryGetValue(marker.RemoteKey, out var list))
            {
                list = new List<GameObject>();
                localLookup[marker.RemoteKey] = list;
            }

            list.Add(marker.gameObject);
        }

        foreach (var kvp in localLookup)
        {
            Debug.Log($"[LOOKUP] {kvp.Key}: {kvp.Value.Count} local instances under {localContentParent.name}");
        }
    }

    // BUTTON: Replace Vehicles
    public async void ReplaceVehiclesWithRemoteDLC()
    {
        await ProcessCategory(remoteVehicleKeys, "Vehicles");
    }

    // BUTTON: Replace Trees
    public async void ReplaceTreesWithRemoteDLC()
    {
        await ProcessCategory(remoteTreeKeys, "Trees");
    }

    // BUTTON: Replace Props
    public async void ReplacePropsWithRemoteDLC()
    {
        await ProcessCategory(remotePropKeys, "Props");
    }

    private async Task ProcessCategory(List<string> keys, string categoryName)
    {
        ResetUI();
        UpdateUIStatus($"Upgrading {categoryName} to remote DLC...");

        float startTime = Time.realtimeSinceStartup;
        long startMem = Profiler.usedHeapSizeLong;

        int totalReplaced = 0;
        int totalKeys = keys.Count;

        for (int i = 0; i < totalKeys; i++)
        {
            string baseKey = keys[i];
            string remoteAddr = baseKey + "_Remote";

            UpdateUIStatus($"Upgrading {categoryName}: {baseKey} ({i+1}/{totalKeys})");
            UpdateUIProgress($"{i}/{totalKeys} ({(float)i / totalKeys * 100:F0}%)");

            int replacedThisKey = await TryReplaceAllInstances(baseKey, remoteAddr);
            totalReplaced += replacedThisKey;
        }

        float duration = Time.realtimeSinceStartup - startTime;
        float deltaMB = (Profiler.usedHeapSizeLong - startMem) / (1024f * 1024f);

        UpdateUIStatus($"Upgrade complete: {totalReplaced} {categoryName.ToLower()} replaced");
        UpdateUITime($"Total Time: {duration:F3} s");
        UpdateUIMemory($"Memory Δ: +{deltaMB:F2} MB");
        UpdateUIProgress("100% - Done");
    }

    private async Task<int> TryReplaceAllInstances(string baseKey, string remoteAddress)
    {
        if (!localLookup.TryGetValue(baseKey, out var instances) || instances.Count == 0)
        {
            Debug.LogWarning($"No local instances registered for key: {baseKey}");
            return 0;
        }

        AsyncOperationHandle<GameObject> handle = default;
        int replacedCount = 0;

        int attempt = 0;
        while (attempt < maxRetryAttempts && replacedCount == 0)
        {
            attempt++;

            try
            {
                Debug.Log($"[LOAD ATTEMPT {attempt}] {remoteAddress}");

                handle = Addressables.LoadAssetAsync<GameObject>(remoteAddress);

                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutPerAttemptSec));
                if (await Task.WhenAny(handle.Task, timeoutTask) == timeoutTask)
                {
                    if (!handle.IsDone) Addressables.Release(handle);
                    throw new TimeoutException($"Timeout loading {remoteAddress}");
                }

                await handle.Task;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    var downloadStatus = handle.GetDownloadStatus();
                    string source = downloadStatus.DownloadedBytes > 0 
                        ? "Fresh Remote Download" 
                        : "Cached Remote";

                    Debug.Log($"[UPGRADE SUCCESS] {source} - {baseKey} - replacing {instances.Count} instances");

                    // Update UI with source (this was missing!)
                    UpdateUISource(source);
                    UpdateUIStatus($"Success: {source} - replacing {instances.Count} {baseKey}");

                    var tempInstances = new List<GameObject>(instances);

                    foreach (var existingLocal in tempInstances)
                    {
                        Vector3 pos = existingLocal.transform.position;
                        Quaternion rot = existingLocal.transform.rotation;
                        Destroy(existingLocal);

                        var newInstance = Instantiate(handle.Result, pos, rot);
                        newInstance.name = $"{handle.Result.name}_Remote";

                        var releaser = newInstance.AddComponent<AddressableReleaser>();
                        releaser.SetHandle(handle);

                        CheckMeshAndMaterial(newInstance, source);

                        replacedCount++;
                    }

                    localLookup[baseKey].Clear();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Attempt {attempt} failed for {remoteAddress}: {ex.Message}");
                UpdateUIError($"Failed attempt {attempt}: {ex.Message}");

                if (attempt >= maxRetryAttempts)
                {
                    Debug.LogError($"Failed to upgrade {baseKey} after {maxRetryAttempts} attempts");
                    return 0;
                }

                await Task.Delay(TimeSpan.FromSeconds(baseRetryDelaySec * Mathf.Pow(2, attempt - 1)));
            }
            finally
            {
                if (handle.IsValid() && replacedCount == 0)
                    Addressables.Release(handle);
            }
        }

        return replacedCount;
    }

    // UI Helpers with debug logs
    private void ResetUI()
    {
        UpdateUIStatus("Ready");
        UpdateUITime("Time: -");
        UpdateUIMemory("Memory: -");
        UpdateUISource("Source: -");
        UpdateUIError("");
        UpdateUIProgress("Progress: -");
    }

    private void UpdateUIStatus(string msg)
    {
        Debug.Log($"[UI STATUS] → '{msg}' | txtStatus: {(txtStatus != null ? "VALID" : "NULL")}");
        SafeSetText(txtStatus, msg);
    }

    private void UpdateUITime(string msg)
    {
        Debug.Log($"[UI TIME] → '{msg}' | txtTime: {(txtTime != null ? "VALID" : "NULL")}");
        SafeSetText(txtTime, msg);
    }

    private void UpdateUIMemory(string msg)
    {
        Debug.Log($"[UI MEMORY] → '{msg}' | txtMemory: {(txtMemory != null ? "VALID" : "NULL")}");
        SafeSetText(txtMemory, msg);
    }

    private void UpdateUISource(string msg)
    {
        Debug.Log($"[UI SOURCE] → '{msg}' | txtSource: {(txtSource != null ? "VALID" : "NULL")}");
        SafeSetText(txtSource, msg);
    }

    private void UpdateUIError(string msg)
    {
        Debug.Log($"[UI ERROR] → '{msg}' | txtError: {(txtError != null ? "VALID" : "NULL")}");
        SafeSetText(txtError, msg);
    }

    private void UpdateUIProgress(string msg)
    {
        Debug.Log($"[UI PROGRESS] → '{msg}' | txtProgress: {(txtProgress != null ? "VALID" : "NULL")}");
        SafeSetText(txtProgress, msg);
    }

    private void SafeSetText(TextMeshProUGUI text, string msg)
    {
        if (text != null)
        {
            text.text = msg;
        }
        else
        {
            Debug.LogWarning("SafeSetText called on null TextMeshProUGUI!");
        }
    }

    private void CheckMeshAndMaterial(GameObject instance, string source)
    {
        var mf = instance.GetComponentInChildren<MeshFilter>();
        if (mf?.sharedMesh != null)
            Debug.Log($"[{source}] Mesh OK: {mf.sharedMesh.name}");
        else
            Debug.LogWarning($"[{source}] No mesh!");

        var rend = instance.GetComponentInChildren<Renderer>();
        if (rend?.sharedMaterial != null)
            Debug.Log($"[{source}] Material OK: {rend.sharedMaterial.name}");
        else
            Debug.LogWarning($"[{source}] No material!");
    }

    public async void CheckForContentUpdate()
    {
        UpdateUIStatus("Checking for updates...");
        var checkHandle = Addressables.CheckForCatalogUpdates(false);

        try
        {
            await checkHandle.Task;
            if (checkHandle.Status == AsyncOperationStatus.Succeeded && checkHandle.Result?.Count > 0)
            {
                UpdateUIStatus($"Applying {checkHandle.Result.Count} updates...");
                var updateHandle = Addressables.UpdateCatalogs(checkHandle.Result, false);
                await updateHandle.Task;
                Addressables.Release(updateHandle);
                UpdateUIStatus("Updated! Re-click category buttons to apply.");
            }
            else
            {
                UpdateUIStatus("Up to date");
            }
        }
        catch (Exception ex)
        {
            UpdateUIError(ex.Message);
        }
        finally
        {
            Addressables.Release(checkHandle);
        }
    }
}

public class AddressableReleaser : MonoBehaviour
{
    private AsyncOperationHandle<GameObject> handle;

    public void SetHandle(AsyncOperationHandle<GameObject> h) => handle = h;

    private void OnDestroy()
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
            Debug.Log($"Released handle for {gameObject.name}");
        }
    }
}