using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;

public class AddressableLoaderTest : MonoBehaviour
{
    [SerializeField] string address = "Rifles/Rifle2.prefab";

    async void Start()
    {
        // Initialize Addressables (loads catalog if remote profile active)
        var initHandle = Addressables.InitializeAsync();  // Returns AsyncOperationHandle<IResourceLocator>

        try
        {
            await initHandle.Task;

            // Check status RIGHT after await (before potential auto-release)
            if (initHandle.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log("Addressables initialized successfully! Catalog ready.");
                // Optional: Debug.Log("Loaded locator: " + initHandle.Result?.LocatorId); // if you want to inspect
            }
            else
            {
                Debug.LogWarning($"Addressables init completed with status: {initHandle.Status}");
                // Can add fallback logic here if needed
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Addressables initialization exception: {ex.Message}");
            return;  // Bail out if fatal
        }

        // Release init handle (safe even if auto-released)
        Addressables.Release(initHandle);

        // Proceed to asset load
        var loadHandle = Addressables.LoadAssetAsync<GameObject>(address);

        try
        {
            await loadHandle.Task;

            if (loadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"Successfully loaded asset from address: {address}");
                Instantiate(loadHandle.Result, Vector3.zero, Quaternion.identity);
            }
            else
            {
                Debug.LogError($"Asset load failed: {loadHandle.OperationException?.Message ?? "Unknown error"}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Asset load threw exception: {ex.Message}");
        }
        finally
        {
            Addressables.Release(loadHandle);  // MUST release to avoid memory leaks
        }
    }
}