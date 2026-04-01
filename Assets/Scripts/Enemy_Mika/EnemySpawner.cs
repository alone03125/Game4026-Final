using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class StageSpawnRange
    {
        public float horizontalRange = 20f;  // 玩家周围水平方向（XZ）的生成半径
        public float upperOffset = 10f;      // 玩家上方的生成范围
        public float lowerOffset = 5f;       // 玩家下方的生成范围
    }

    [System.Serializable]
    public class StageAdjustment
    {
        public int addCount;    // 触发时增加的数量
        public int removeCount; // 触发时删除的数量
    }

    [Header("阶段生成范围（相对玩家）")]
    public StageSpawnRange[] stages;        // 每个阶段相对玩家的生成范围

    [Header("对象池")]
    public ObjectPool enemyPool;            // 对象池引用

    [Header("生成参数")]
    public float spawnInterval = 5f;        // 固定生成间隔（动态模式关闭时使用）
    public int maxEnemyCount = 15;          // 最多同时存在的敌人数

    [Header("动态生成速率")]
    public bool useDynamicSpawnRate = true;          // 是否启用动态速率
    public int targetEnemyCount = 10;                // 期望保持的敌人数
    public float minSpawnInterval = 1f;              // 最快生成间隔（秒）
    public float maxSpawnInterval = 8f;              // 最慢生成间隔（秒）

    [Header("阶段配置（增删）")]
    public StageAdjustment[] stageAdjustments = new StageAdjustment[3]; // 三个阶段的可配置增删

    [Header("摄像机视野")]
    public Camera mainCamera;               // 主摄像机（不设置则自动查找）
    public int maxSpawnAttempts = 30;       // 寻找视野外位置的最大尝试次数

    private int currentStage = 0;
    private List<Enemy> activeEnemies = new List<Enemy>();
    private float spawnTimer;
    private Transform player;

    // 由 GameFlowManager 控制，默认关闭，防止游戏启动时自动生成
    private bool isSpawningEnabled = false;

    void Start()
    {
        if (stages.Length == 0)
            Debug.LogError("没有设置阶段生成范围！");

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
        else
            Debug.LogError("没有找到Player标签的物体！");

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            Debug.LogError("找不到主摄像机，请手动为 EnemySpawner 指定 mainCamera！");

        spawnTimer = 0f;
    }

    /// <summary>开启自动持续生成（由 GameFlowManager 在战斗阶段开始时调用）</summary>
    public void EnableSpawning()
    {
        isSpawningEnabled = true;
        spawnTimer = 0f; // 开启后立即可以生成
    }

    /// <summary>关闭自动持续生成（由 GameFlowManager 在阶段结束时调用）</summary>
    public void DisableSpawning()
    {
        isSpawningEnabled = false;
    }

    void Update()
    {
        if (!isSpawningEnabled) return;

        if (useDynamicSpawnRate)
        {
            // 根据当前敌人数量动态计算间隔
            int currentCount = activeEnemies.Count;
            float ratio = Mathf.Clamp01(currentCount / (float)targetEnemyCount);
            float dynamicInterval = Mathf.Lerp(minSpawnInterval, maxSpawnInterval, ratio);

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f && activeEnemies.Count < maxEnemyCount)
            {
                SpawnEnemy();
                spawnTimer = dynamicInterval;
            }
        }
        else
        {
            // 固定间隔模式
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f && activeEnemies.Count < maxEnemyCount)
            {
                SpawnEnemy();
                spawnTimer = spawnInterval;
            }
        }
    }

    public void SpawnEnemy()
    {
        if (enemyPool == null)
        {
            Debug.LogError("Enemy pool not assigned!");
            return;
        }

        Bounds bounds = GetCurrentBounds();
        Vector3 randomPos = Vector3.zero;
        bool foundOutsideView = false;

        // 尝试在视野外生成
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

        if (!foundOutsideView)
        {
            randomPos = bounds.min;
            Debug.LogWarning("无法找到视野外的生成点，使用边界角落");
        }

        GameObject newEnemy = enemyPool.GetObject(randomPos, Quaternion.identity);
        Enemy enemyScript = newEnemy.GetComponent<Enemy>();
        if (enemyScript != null)
        {
            enemyScript.SetSpawner(this);
            activeEnemies.Add(enemyScript);
        }
    }

    public void SpawnEnemyImmediate()
    {
        SpawnEnemy();
    }

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

        if (enemyPool != null)
        {
            enemyPool.ReturnObject(enemyObj);
        }
        else
        {
            Destroy(enemyObj);
        }
    }

    public void ClearAllEnemies()
    {
        var enemiesToClear = new List<Enemy>(activeEnemies);
        foreach (Enemy enemy in enemiesToClear)
        {
            if (enemy != null)
            {
                DestroyEnemyWithoutNotification(enemy.gameObject);
            }
        }
        activeEnemies.Clear();
    }

    private void DestroyEnemyWithoutNotification(GameObject enemyObj)
    {
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

        StageSpawnRange range = stages[currentStage];
        Vector3 playerPos = player != null ? player.position : Vector3.zero;

        // 以玩家为中心，上下分别偏移，水平方向为半径
        float centerY = playerPos.y + (range.upperOffset - range.lowerOffset) / 2f;
        Vector3 center = new Vector3(playerPos.x, centerY, playerPos.z);
        Vector3 size = new Vector3(range.horizontalRange * 2f, range.upperOffset + range.lowerOffset, range.horizontalRange * 2f);
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

    public void SetSpawnInterval(float interval)
    {
        spawnInterval = interval;
        Debug.Log($"生成间隔已改为 {interval} 秒");
    }

    public void AddEnemies(int count)
    {
        if (count <= 0) return;
        int availableSlots = maxEnemyCount - activeEnemies.Count;
        int toSpawn = Mathf.Min(count, availableSlots);
        for (int i = 0; i < toSpawn; i++)
        {
            SpawnEnemy();
        }
        Debug.Log($"增加了 {toSpawn} 个敌人，当前场上敌人数量：{activeEnemies.Count}");
    }

    public void RemoveEnemies(int count)
    {
        if (count <= 0) return;
        int toRemove = Mathf.Min(count, activeEnemies.Count);
        for (int i = 0; i < toRemove; i++)
        {
            if (activeEnemies.Count == 0) break;
            int index = Random.Range(0, activeEnemies.Count);
            Enemy enemy = activeEnemies[index];
            if (enemy != null)
            {
                DestroyEnemyWithoutNotification(enemy.gameObject);
            }
            activeEnemies.RemoveAt(index);
        }
        Debug.Log($"移除了 {toRemove} 个敌人，当前场上敌人数量：{activeEnemies.Count}");
    }

    public void ApplyStageAdjustment(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= stageAdjustments.Length)
        {
            Debug.LogWarning($"阶段 {stageIndex} 无配置，跳过调整");
            return;
        }
        var adj = stageAdjustments[stageIndex];
        if (adj.addCount > 0)
            AddEnemies(adj.addCount);
        if (adj.removeCount > 0)
            RemoveEnemies(adj.removeCount);
    }
}