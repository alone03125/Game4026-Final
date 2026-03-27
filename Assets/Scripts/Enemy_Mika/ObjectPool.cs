using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    public GameObject prefab;           // The enemy prefab
    public int initialSize = 10;        // Number of enemies to pre-create
    public int maxSize = 20;             // Maximum pool size (optional)

    private Queue<GameObject> pool = new Queue<GameObject>();

    private void Start()
    {
        // Pre-instantiate objects
        for (int i = 0; i < initialSize; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    // Get an object from the pool
    public GameObject GetObject(Vector3 position, Quaternion rotation)
    {
        GameObject obj;
        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);
        }
        else
        {
            // Optional: if max size reached, you might want to create a new one or handle differently
            obj = Instantiate(prefab, position, rotation);
            Debug.LogWarning("Pool exhausted, instantiating new object.");
        }

        // Reset any necessary components (e.g., enemy state)
        ResetEnemy(obj);
        return obj;
    }

    // Return an object to the pool
    public void ReturnObject(GameObject obj)
    {
        obj.SetActive(false);
        // Optionally, you can limit pool size and destroy if over maxSize
        if (pool.Count < maxSize)
        {
            pool.Enqueue(obj);
        }
        else
        {
            Destroy(obj);
        }
    }

    // Reset the enemy's state (e.g., health, timers, etc.)
    private void ResetEnemy(GameObject obj)
    {
        Enemy enemy = obj.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.ResetState(); // We'll add this method
        }
    }
}