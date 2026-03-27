using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class StageBounds
    {
        public Vector3 min;
        public Vector3 max;
    }

    public StageBounds[] stages;            // 每个阶段的生成边界
    public ObjectPool enemyPool;            // 对象池引用
    public float spawnInterval = 5f;        // 生成间隔（秒）
    public int maxEnemyCount = 15;          // 最多同时存在的敌人数
    public Camera mainCamera;               // 主摄像机（不设置则自动查找）
    public int maxSpawnAttempts = 30;       // 寻找视野外位置的最大尝试次数

    private int currentStage = 0;
    private List<Enemy> activeEnemies = new List<Enemy>();
    private float spawnTimer;

    void Start()
    {
        if (stages.Length == 0)
            Debug.LogError("没有设置阶段边界！");

        // 获取主摄像机
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            Debug.LogError("找不到主摄像机，请手动为 EnemySpawner 指定 mainCamera！");

        spawnTimer = spawnInterval;
    }

    void Update()
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f && activeEnemies.Count < maxEnemyCount)
        {
            SpawnEnemy();
            spawnTimer = spawnInterval;
        }
    }

    void SpawnEnemy()
    {
        if (enemyPool == null)
        {
            Debug.LogError("Enemy pool not assigned!");
            return;
        }

        Bounds bounds = GetCurrentBounds();
        Vector3 randomPos = Vector3.zero;
        bool foundOutsideView = false;

        // 多次尝试，寻找摄像机视野外的位置
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            randomPos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );

            if (!IsPositionInCameraView(randomPos))
            {
                foundOutsideView = true;
                break;
            }
        }

        // 如果始终找不到视野外的点，则使用边界的一个角（最后手段）
        if (!foundOutsideView)
        {
            randomPos = bounds.min;
            Debug.LogWarning("无法找到视野外的生成点，使用边界角落");
        }

        // 从池中获取敌人
        GameObject newEnemy = enemyPool.GetObject(randomPos, Quaternion.identity);
        Enemy enemyScript = newEnemy.GetComponent<Enemy>();
        if (enemyScript != null)
        {
            enemyScript.SetSpawner(this);
            activeEnemies.Add(enemyScript);
        }
    }

    /// <summary>
    /// 判断世界坐标是否在主摄像机的视野内（视口坐标0~1且深度>0）
    /// </summary>
    bool IsPositionInCameraView(Vector3 worldPos)
    {
        if (mainCamera == null) return false;

        Vector3 viewportPos = mainCamera.WorldToViewportPoint(worldPos);
        return viewportPos.x >= 0 && viewportPos.x <= 1 &&
               viewportPos.y >= 0 && viewportPos.y <= 1 &&
               viewportPos.z > 0;
    }

    public void OnEnemyDied(GameObject enemyObj)
    {
        Enemy enemy = enemyObj.GetComponent<Enemy>();
        if (enemy != null && activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
        }

        // 返回对象池
        if (enemyPool != null)
        {
            enemyPool.ReturnObject(enemyObj);
        }
        else
        {
            Destroy(enemyObj);
        }
    }

    public Bounds GetCurrentBounds()
    {
        if (currentStage < 0 || currentStage >= stages.Length)
            return new Bounds(Vector3.zero, Vector3.zero);

        StageBounds sb = stages[currentStage];
        Vector3 center = (sb.min + sb.max) / 2;
        Vector3 size = sb.max - sb.min;
        return new Bounds(center, size);
    }

    public void SetStage(int stageIndex)
    {
        if (stageIndex >= 0 && stageIndex < stages.Length)
        {
            currentStage = stageIndex;
            Debug.Log($"切换至阶段 {currentStage}");
        }
        else
        {
            Debug.LogWarning("无效的阶段索引");
        }
    }
}