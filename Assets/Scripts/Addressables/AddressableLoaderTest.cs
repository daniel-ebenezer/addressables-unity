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
    [Header("Managers & Config")]
    [SerializeField] private ContentSpawner contentSpawner;
    [SerializeField] private Transform localContentParent;
    [SerializeField] private AddressablesConfig configAsset;

    [Header("Debug & Testing Tools")]
    [SerializeField] private bool forceFreshRemoteAlways = false;
    [SerializeField] private Toggle toggleForceFresh;

    [Header("A/B Variant Testing")]
    [SerializeField] private bool enableABTesting = false;
    [SerializeField] private bool currentVariantIsA = true;
    [SerializeField] private string abTestBaseKey = "Vehicles/Sedan";

    [Header("Retry Settings")]
    [SerializeField] private int maxRetryAttempts = 3;
    [SerializeField] private float baseRetryDelaySec = 2f;
    [SerializeField] private float timeoutPerAttemptSec = 15f;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI memoryTakenText;
    [SerializeField] private TextMeshProUGUI sourceText;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Slider progressSlider;

    private readonly Dictionary<string, List<GameObject>> localLookup = new();
    private readonly Dictionary<GameObject, GameObject> replacementMap = new Dictionary<GameObject, GameObject>();

    private List<string> remoteVehicleKeys = new List<string>();
    private List<string> remoteTreeKeys = new List<string>();
    private List<string> remotePropKeys = new List<string>();
    private string labelVariantA = "Variant_A";
    private string labelVariantB = "Variant_B";
    private string addressSuffix = "_Remote";

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
            Debug.LogError("LocalContentParent not assigned!");
            return;
        }

        if (configAsset == null)
        {
            Debug.LogError("AddressablesConfig asset not assigned!");
            return;
        }

        if (toggleForceFresh != null)
        {
            toggleForceFresh.isOn = forceFreshRemoteAlways;
            toggleForceFresh.onValueChanged.AddListener(OnForceFreshToggled);
        }

        InitializeFromConfig();

        if (statusText == null) Debug.LogError("statusText is NULL - assign in Inspector!");
        if (timeText == null) Debug.LogError("timeText is NULL - assign in Inspector!");
        if (memoryTakenText == null) Debug.LogError("memoryTakenText is NULL - assign in Inspector!");
        if (sourceText == null) Debug.LogError("sourceText is NULL - assign in Inspector!");
        if (errorText == null) Debug.LogError("errorText is NULL - assign in Inspector!");
        if (progressText == null) Debug.LogError("progressText is NULL - assign in Inspector!");
        if (progressSlider == null) Debug.LogWarning("progressSlider not assigned");

        BuildLocalLookup();
    }

    private void OnForceFreshToggled(bool isOn)
    {
        forceFreshRemoteAlways = isOn;
        UpdateUIStatus($"Force Fresh Remote: {(isOn ? "ENABLED" : "DISABLED")} - next load will be {(isOn ? "fresh from remote" : "cached if available")}");
    }

    private void InitializeFromConfig()
    {
        remoteVehicleKeys.Clear();
        remoteTreeKeys.Clear();
        remotePropKeys.Clear();

        foreach (var category in configAsset.categories)
        {
            if (category.categoryName == "Vehicles") remoteVehicleKeys.AddRange(category.baseKeys);
            else if (category.categoryName == "Trees") remoteTreeKeys.AddRange(category.baseKeys);
            else if (category.categoryName == "Props") remotePropKeys.AddRange(category.baseKeys);
        }

        foreach (var variant in configAsset.abVariants)
        {
            if (variant.baseKey == abTestBaseKey)
            {
                labelVariantA = variant.labelA;
                labelVariantB = variant.labelB;
                break;
            }
        }

        addressSuffix = configAsset.defaultAddressSuffix;
    }

    private string GetRemoteAddress(string baseKey, string categoryName)
    {
        string suffix = configAsset.defaultAddressSuffix;
        foreach (var category in configAsset.categories)
        {
            if (category.categoryName == categoryName)
            {
                suffix = category.addressSuffix;
                break;
            }
        }
        return baseKey + suffix;
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
    }

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

    public async void CycleABVariantVehicle()
    {
        if (!enableABTesting)
        {
            UpdateUIStatus("A/B testing is disabled in Inspector");
            return;
        }

        currentVariantIsA = !currentVariantIsA;
        UpdateUIStatus($"Cycling to Variant {(currentVariantIsA ? "A" : "B")}");

        await ProcessCategory(new List<string> { abTestBaseKey }, "A/B Test Vehicle", true);
    }

    public async void ClearCacheAndForceFresh()
    {
        UpdateUIStatus("Clearing cache...");

        await DownloadManager.ClearAllAsync();

        UpdateUIStatus("Cache cleared! Click replace for fresh remote load");
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

        for (int i = 0; i < totalKeys; i++)
        {
            string baseKey = keys[i];
            string remoteAddr = GetRemoteAddress(baseKey, categoryName);

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
        long deltaBytes = Profiler.usedHeapSizeLong - startMem;
        float deltaMB = deltaBytes / (1024f * 1024f);

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
                    existingLocal.SetActive(false);

                    var newInstance = await LoadManager.InstantiateSafeAsync(remoteAddress, pos, rot);

                    if (newInstance != null)
                    {
                        newInstance.name = $"{newInstance.name}_{source.Replace(" ", "_")}";

                        var releaser = newInstance.AddComponent<AddressableReleaser>();
                        releaser.SetHandle(loadHandle);

                        replacementMap[newInstance] = existingLocal;

                        replacedCount++;
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

    private void ResetUI()
    {
        UpdateUIStatus("Ready");
        UpdateUITime("Time: -");
        UpdateUIMemory("Memory: -");
        UpdateUISource("Source: -");
        UpdateUIError("");
        UpdateUIProgress("Progress: -");
    }

    private void UpdateUIStatus(string msg) => SafeSetText(statusText, msg);
    private void UpdateUITime(string msg) => SafeSetText(timeText, msg);
    private void UpdateUIMemory(string msg) => SafeSetText(memoryTakenText, msg);
    private void UpdateUISource(string msg) => SafeSetText(sourceText, msg);
    private void UpdateUIError(string msg) => SafeSetText(errorText, msg);
    private void UpdateUIProgress(string msg) => SafeSetText(progressText, msg);

    private void SafeSetText(TextMeshProUGUI text, string msg)
    {
        if (text != null) text.text = msg;
    }

    public async void ClearCacheAndResetToLocal()
{
    UpdateUIStatus("Clearing cache & resetting to local...");

    await Addressables.ClearDependencyCacheAsync(new List<object>(), true).Task;
    //Addressables.ClearResourceLocators();

    foreach (var pair in replacementMap)
    {
        var remote = pair.Key;
        var local = pair.Value;

        if (remote != null)
        {
            Addressables.ReleaseInstance(remote);
            Destroy(remote);
        }

        if (local != null)
        {
            local.SetActive(true);
        }
    }

    replacementMap.Clear();
    BuildLocalLookup(); 

    UpdateUIStatus("Reset complete! Click replace for fresh remote load.");
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
        }
    }
}