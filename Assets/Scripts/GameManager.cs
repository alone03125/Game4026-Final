using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("重力参数")]
    public float gravityBase = 1f;               // 当前重力值
    public float gravityChangeInterval = 5f;     // 重力变化间隔（秒）
    public float gravityMin = 0.5f;
    public float gravityMax = 2f;

    [Header("阶段参数")]
    public int[] enemiesToSpawnOnStageStart = { 3, 6, 9 };
    public int[] killsRequiredForNextStage = { 10, 15, 20 };
    public float[] spawnIntervalsPerStage = { 5f, 4f, 3f };

    [Header("组件引用")]
    public EnemySpawner enemySpawner;             // 必须拖入场景中的 EnemySpawner

    private int currentStage = 0;                 // 0,1,2
    private int currentKills = 0;
    private float gravityTimer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);            // 可选，跨场景保留
    }

    void Start()
    {
        if (enemySpawner == null)
        {
            Debug.LogError("GameManager: 未设置 enemySpawner 引用！");
        }

        gravityTimer = gravityChangeInterval;
        StartStage(0);
    }

    void Update()
    {
        gravityTimer -= Time.deltaTime;
        if (gravityTimer <= 0f)
        {
            ChangeGravityRandomly();
            gravityTimer = gravityChangeInterval;
        }
    }

    void ChangeGravityRandomly()
    {
        gravityBase = Random.Range(gravityMin, gravityMax);
        Debug.Log($"重力已改变：{gravityBase:F2}");
    }

    void StartStage(int stageIndex)
    {
        currentStage = stageIndex;
        currentKills = 0;

        if (enemySpawner != null)
        {
            enemySpawner.SetStage(stageIndex);
            // 新增：设置当前阶段的生成间隔
            if (stageIndex < spawnIntervalsPerStage.Length)
            {
                enemySpawner.SetSpawnInterval(spawnIntervalsPerStage[stageIndex]);
            }
        }

        int count = enemiesToSpawnOnStageStart[stageIndex];
        for (int i = 0; i < count; i++)
        {
            enemySpawner.SpawnEnemyImmediate();
        }

        Debug.Log($"进入阶段 {stageIndex + 1}，生成间隔 {spawnIntervalsPerStage[stageIndex]} 秒，初始生成 {count} 个敌人，需击败 {killsRequiredForNextStage[stageIndex]} 个敌人进入下一阶段");
    }

    // 敌人死亡时调用（由 Enemy 调用）
    public void OnEnemyKilled()
    {
        currentKills++;
        Debug.Log($"击败敌人 {currentKills}/{killsRequiredForNextStage[currentStage]}");

        if (currentKills >= killsRequiredForNextStage[currentStage])
        {
            int nextStage = currentStage + 1;
            if (nextStage < enemiesToSpawnOnStageStart.Length)
            {
                // 清除所有敌人（不触发击杀计数）
                if (enemySpawner != null)
                {
                    enemySpawner.ClearAllEnemies();
                }

                StartStage(nextStage);
            }
            else
            {
                Debug.Log("游戏通关！所有阶段已完成。");
                // 可在此添加游戏胜利逻辑
            }
        }
    }

    // 供 Enemy 获取当前重力值
    public float GetCurrentGravity()
    {
        return gravityBase;
    }

    public int GetCurrentStage()
    {
        return currentStage;
    }


}