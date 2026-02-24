using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using UnityEngine.Profiling;
using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

public class AddressableLoaderTest : MonoBehaviour
{
    [Header("=== Managers & Containers ===")]
    [SerializeField] private ContentSpawner contentSpawner;

    [SerializeField, Tooltip("Parent object containing ALL local upgradable objects")]
    private Transform localContentParent;

    [Header("=== Configuration (replaces hardcoded lists) ===")]
    [SerializeField] private AddressablesConfig configAsset;

    // Class-level fields (populated from config)
    private List<string> remoteVehicleKeys = new List<string>();
    private List<string> remoteTreeKeys = new List<string>();
    private List<string> remotePropKeys = new List<string>();
    private string labelVariantA = "Variant_A";
    private string labelVariantB = "Variant_B";
    private string addressSuffix = "_Remote";

    [Header("=== A/B Variant Testing (Vehicles only - cycle with button) ===")]
    [SerializeField] private bool enableABTesting = false;

    [SerializeField] private bool currentVariantIsA = true;

    [SerializeField, Tooltip("Only this base key will cycle A/B when the special button is clicked")]
    private string abTestBaseKey = "Vehicles/Sedan";

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

    [Header("Visual Progress Bar")]
    [SerializeField] private Slider progressSlider;

    private readonly Dictionary<string, List<GameObject>> localLookup = new();

    private void Awake()
    {
        if (contentSpawner == null)
        {
            contentSpawner = UnityEngine.Object.FindAnyObjectByType<ContentSpawner>();
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

        if (configAsset == null)
        {
            Debug.LogError("AddressablesConfig asset not assigned!");
            return;
        }

        // Read from config (no hardcoding)
        InitializeFromConfig();

        // UI safety checks
        if (txtStatus == null) Debug.LogError("txtStatus is NULL - assign in Inspector!");
        if (txtTime == null) Debug.LogError("txtTime is NULL - assign in Inspector!");
        if (txtMemory == null) Debug.LogError("txtMemory is NULL - assign in Inspector!");
        if (txtSource == null) Debug.LogError("txtSource is NULL - assign in Inspector!");
        if (txtError == null) Debug.LogError("txtError is NULL - assign in Inspector!");
        if (txtProgress == null) Debug.LogError("txtProgress is NULL - assign in Inspector!");
        if (progressSlider == null) Debug.LogWarning("progressSlider not assigned");

        BuildLocalLookup();
    }

    private void InitializeFromConfig()
    {
        remoteVehicleKeys.Clear();
        remoteTreeKeys.Clear();
        remotePropKeys.Clear();

        foreach (var cat in configAsset.categories)
        {
            if (cat.categoryName == "Vehicles") remoteVehicleKeys.AddRange(cat.baseKeys);
            else if (cat.categoryName == "Trees") remoteTreeKeys.AddRange(cat.baseKeys);
            else if (cat.categoryName == "Props") remotePropKeys.AddRange(cat.baseKeys);
        }

        // A/B config
        foreach (var v in configAsset.abVariants)
        {
            if (v.baseKey == abTestBaseKey)
            {
                labelVariantA = v.labelA;
                labelVariantB = v.labelB;
                break;
            }
        }

        // Global suffix fallback
        addressSuffix = configAsset.defaultAddressSuffix;

        Debug.Log("[CONFIG] Loaded from AddressablesConfig asset");
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
            Debug.Log($"[LOOKUP] {kvp.Key}: {kvp.Value.Count} local instances");
        }
    }

    // Original buttons (normal remote load)
    public async void ReplaceVehiclesWithRemoteDLC()
    {
        await ProcessCategory(remoteVehicleKeys, "Vehicles", false);
    }

    public async void ReplaceTreesWithRemoteDLC()
    {
        await ProcessCategory(remoteTreeKeys, "Trees", false);
    }

    public async void ReplacePropsWithRemoteDLC()
    {
        await ProcessCategory(remotePropKeys, "Props", false);
    }

    // Cycle A/B button – only affects the A/B test key
    public async void CycleABVariantVehicle()
    {
        if (!enableABTesting)
        {
            UpdateUIStatus("A/B testing is disabled in Inspector");
            return;
        }

        currentVariantIsA = !currentVariantIsA;
        UpdateUIStatus($"Cycling to Variant {(currentVariantIsA ? "A" : "B")}");

        Debug.Log($"[CYCLE DEBUG] Cycle button clicked | Variant: {(currentVariantIsA ? "A" : "B")} | Key: {abTestBaseKey}");
        await ProcessCategory(new List<string> { abTestBaseKey }, "A/B Test Vehicle", true);
    }

    private async Task ProcessCategory(List<string> keys, string categoryName, bool useVariants = false)
    {
        ResetUI();
        UpdateUIStatus($"Upgrading {categoryName} to remote DLC...");

        if (progressSlider != null)
        {
            progressSlider.value = 0f;
            progressSlider.gameObject.SetActive(true);
        }

        float startTime = Time.realtimeSinceStartup;
        long startMem = Profiler.usedHeapSizeLong;

        int totalReplaced = 0;
        int totalKeys = keys.Count;

        // Get suffix from config (per category or default)
        string suffix = configAsset.defaultAddressSuffix;
        foreach (var cat in configAsset.categories)
        {
            if (cat.categoryName == categoryName)
            {
                suffix = cat.addressSuffix;
                break;
            }
        }

        for (int i = 0; i < totalKeys; i++)
        {
            string baseKey = keys[i];
            string remoteAddr = baseKey + suffix;
        Debug.Log($"[CATEGORY DEBUG] Processing {categoryName} | BaseKey: {baseKey} | RemoteAddr: {remoteAddr} | UseVariants: {useVariants}");

            UpdateUIStatus($"Upgrading {categoryName}: {baseKey} ({i+1}/{totalKeys})");
            UpdateUIProgress($"{i+1}/{totalKeys} ({(float)(i+1) / totalKeys * 100:F0}%)");

            if (progressSlider != null)
                progressSlider.value = (float)(i + 1) / totalKeys;

            int replacedThisKey = await TryReplaceAllInstances(baseKey, remoteAddr, useVariants);
            totalReplaced += replacedThisKey;
        }

        if (progressSlider != null)
        {
            progressSlider.value = 1f;
            await Task.Delay(1500);
            progressSlider.gameObject.SetActive(false);
        }

        float duration = Time.realtimeSinceStartup - startTime;
        float deltaMB = (Profiler.usedHeapSizeLong - startMem) / (1024f * 1024f);

        UpdateUIStatus($"Upgrade complete: {totalReplaced} {categoryName.ToLower()} replaced");
        UpdateUITime($"Total Time: {duration:F3} s");
        UpdateUIMemory($"Memory Δ: +{deltaMB:F2} MB");
        UpdateUIProgress("100% - Done");
    }

    private async Task<int> TryReplaceAllInstances(string baseKey, string remoteAddress, bool useVariants = false)
{
    if (!localLookup.TryGetValue(baseKey, out var instances) || instances.Count == 0)
    {
        Debug.LogWarning($"No local instances registered for key: {baseKey}");
        return 0;
    }

    AsyncOperationHandle<GameObject> loadHandle = default;
    int replacedCount = 0;

    string source = "Remote";

    if (useVariants)
    {
        string activeLabel = currentVariantIsA ? labelVariantA : labelVariantB;
        Debug.Log($"[A/B] Using label: {activeLabel} for {baseKey}");
        Debug.Log($"[A/B DEBUG] Searching for address '{remoteAddress}' + label '{activeLabel}'");
        var locationHandle = Addressables.LoadResourceLocationsAsync(
            new List<object> { remoteAddress, activeLabel },
            Addressables.MergeMode.Intersection
        );

        await locationHandle.Task;

        if (locationHandle.Status != AsyncOperationStatus.Succeeded || locationHandle.Result.Count == 0)
        {
            Debug.LogWarning($"[A/B] No location found for {remoteAddress} + {activeLabel} - falling back to normal");
            Addressables.Release(locationHandle);
        }
        else
        {
            var primaryLocation = locationHandle.Result[0];
            loadHandle = Addressables.LoadAssetAsync<GameObject>(primaryLocation);
            source += $" ({activeLabel})";
            Addressables.Release(locationHandle);
        }
    }

    if (!loadHandle.IsValid())
    {
        loadHandle = Addressables.LoadAssetAsync<GameObject>(remoteAddress);
    }

    try
    {
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutPerAttemptSec));
        if (await Task.WhenAny(loadHandle.Task, timeoutTask) == timeoutTask)
        {
            if (!loadHandle.IsDone) Addressables.Release(loadHandle);
            throw new TimeoutException($"Timeout loading {remoteAddress}");
        }

        await loadHandle.Task;

        if (loadHandle.Status == AsyncOperationStatus.Succeeded)
        {
            var downloadStatus = loadHandle.GetDownloadStatus();
            source = downloadStatus.DownloadedBytes > 0 
                ? $"Fresh {source}" 
                : $"Cached {source}";

            UpdateUISource(source);
            UpdateUIStatus($"Success: {source} - replacing {instances.Count} {baseKey}");

            var tempInstances = new List<GameObject>(instances);

            foreach (var existingLocal in tempInstances)
            {
                Vector3 pos = existingLocal.transform.position;
                Quaternion rot = existingLocal.transform.rotation;
                Destroy(existingLocal);

                // FIXED: Use InstantiateAsync to wait for ALL dependencies
                var instantiateHandle = Addressables.InstantiateAsync(
                    remoteAddress,  // or primaryLocation if using variants
                    pos,
                    rot,
                    null,           // no parent
                    true            // worldSpace = true
                );

                await instantiateHandle.Task;

                if (instantiateHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    var newInstance = instantiateHandle.Result;
                    newInstance.name = $"{newInstance.name}_{source.Replace(" ", "_")}";

                    var releaser = newInstance.AddComponent<AddressableReleaser>();
                    releaser.SetHandle(instantiateHandle); // IMPORTANT: release instantiate handle

                    CheckMeshAndMaterial(newInstance, source);

                    replacedCount++;
                }
                else
                {
                    Debug.LogError($"[INSTANTIATE FAILED] {instantiateHandle.OperationException?.Message}");
                }
            }

            localLookup[baseKey].Clear();
        }
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"Load failed for {remoteAddress}: {ex.Message}");
        UpdateUIError($"Load failed: {ex.Message}");
    }
    finally
    {
        if (loadHandle.IsValid())
            Addressables.Release(loadHandle);
    }

    return replacedCount;
}   

    // UI Helpers (unchanged)
    private void ResetUI()
    {
        UpdateUIStatus("Ready");
        UpdateUITime("Time: -");
        UpdateUIMemory("Memory: -");
        UpdateUISource("Source: -");
        UpdateUIError("");
        UpdateUIProgress("Progress: -");
    }

    private void UpdateUIStatus(string msg) => SafeSetText(txtStatus, msg);
    private void UpdateUITime(string msg) => SafeSetText(txtTime, msg);
    private void UpdateUIMemory(string msg) => SafeSetText(txtMemory, msg);
    private void UpdateUISource(string msg) => SafeSetText(txtSource, msg);
    private void UpdateUIError(string msg) => SafeSetText(txtError, msg);
    private void UpdateUIProgress(string msg) => SafeSetText(txtProgress, msg);

    private void SafeSetText(TextMeshProUGUI text, string msg)
    {
        if (text != null) text.text = msg;
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