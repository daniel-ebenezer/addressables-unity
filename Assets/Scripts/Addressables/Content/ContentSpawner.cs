using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;

public class ContentSpawner : MonoBehaviour
{

    private readonly List<GameObject> activeObjects = new();
    private readonly Dictionary<string, ObjectPool<GameObject>> prefabPools = new();

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, string source = "")
    {
        if (prefab == null) return null;

        string prefabKey = prefab.name;

        if (!prefabPools.TryGetValue(prefabKey, out var pool))
        {
            pool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(prefab),
                actionOnGet: obj => obj.SetActive(true),
                actionOnRelease: obj => obj.SetActive(false),
                actionOnDestroy: Destroy,
                collectionCheck: false,
                defaultCapacity: 5,
                maxSize: 20
            );
            prefabPools[prefabKey] = pool;
        }

        var instance = pool.Get();
        instance.transform.SetPositionAndRotation(position, rotation);

        instance.name = $"{prefab.name}_{source}";
        activeObjects.Add(instance);

        //Debug.Log($"Spawned: {instance.name} from {source}");

        return instance;
    }

    public void ClearAll()
    {
        foreach (var obj in activeObjects)
        {
            if (obj != null && prefabPools.TryGetValue(obj.name.Split('_')[0], out var pool))
            {
                pool.Release(obj);
            }
            else if (obj != null)
            {
                Destroy(obj);
            }
        }
        activeObjects.Clear();
    }

    private void OnDestroy()
    {
        foreach (var pool in prefabPools.Values)
            pool.Clear();
        prefabPools.Clear();
    }
}