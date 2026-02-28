using UnityEngine;

public class LocalContentMarker : MonoBehaviour
{
    public string RemoteKey = "";

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(RemoteKey))
        {
            RemoteKey = gameObject.name.Replace("_Local", "").Replace("(Clone)", "").Trim();
        }
    }
}