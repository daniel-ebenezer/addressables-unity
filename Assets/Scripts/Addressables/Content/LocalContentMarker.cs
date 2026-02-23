using UnityEngine;

public class LocalContentMarker : MonoBehaviour
{
    [Tooltip("Base key matching the remote address without '_Remote' suffix.\n" +
             "Example: for 'Vehicles/Sedan_Remote' → 'Vehicles/Sedan'")]
    public string RemoteKey = "";

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(RemoteKey))
        {
            RemoteKey = gameObject.name.Replace("_Local", "").Replace("(Clone)", "").Trim();
        }
    }
}