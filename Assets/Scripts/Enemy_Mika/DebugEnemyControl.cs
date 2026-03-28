using UnityEngine;

/// <summary>
/// 调试脚本：测试敌人的增删功能
/// 按 E：根据当前阶段配置的 addCount 增加敌人
/// 按 F：根据当前阶段配置的 removeCount 删除敌人
/// 按 T：清除所有敌人（不触发击杀计数）
/// </summary>
public class DebugEnemyControl : MonoBehaviour
{
    [Header("组件引用")]
    public EnemySpawner enemySpawner;       // 敌人生成器
    public GameManager gameManager;         // 游戏管理器（用于获取当前阶段，可选）

    [Header("测试模式")]
    public bool useManualStage = false;     // 手动指定测试阶段（忽略 GameManager）
    public int manualStage = 0;             // 手动阶段索引（0,1,2）

    [Header("按键设置")]
    public KeyCode addKey = KeyCode.E;      // 增加敌人按键
    public KeyCode removeKey = KeyCode.F;   // 删除敌人按键
    public KeyCode clearAllKey = KeyCode.T; // 清除所有敌人按键

    private int currentStageIndex = 0;

    void Start()
    {
        if (enemySpawner == null)
            enemySpawner = FindObjectOfType<EnemySpawner>();

        if (enemySpawner == null)
            Debug.LogError("DebugEnemyControl: 未找到 EnemySpawner！");

        if (gameManager == null && !useManualStage)
            gameManager = FindObjectOfType<GameManager>();
    }

    void Update()
    {
        if (enemySpawner == null) return;

        // 获取当前阶段索引
        if (!useManualStage && gameManager != null)
        {
            currentStageIndex = gameManager.GetCurrentStage(); // 假设 GameManager 有此方法
        }
        else if (useManualStage)
        {
            currentStageIndex = manualStage;
        }
        else
        {
            // 如果没有 GameManager 且未手动指定，则默认为0并警告
            currentStageIndex = 0;
            Debug.LogWarning("DebugEnemyControl: 未找到 GameManager 且未启用手动阶段，使用阶段0");
        }

        // 确保阶段索引有效
        if (currentStageIndex < 0 || currentStageIndex >= enemySpawner.stageAdjustments.Length)
        {
            Debug.LogWarning($"DebugEnemyControl: 阶段 {currentStageIndex} 超出配置范围，使用阶段0");
            currentStageIndex = 0;
        }

        // 按 E：增加当前阶段预设的敌人数量
        if (Input.GetKeyDown(addKey))
        {
            int addCount = enemySpawner.stageAdjustments[currentStageIndex].addCount;
            if (addCount > 0)
            {
                enemySpawner.AddEnemies(addCount);
                Debug.Log($"【调试】增加 {addCount} 个敌人（阶段{currentStageIndex}预设）");
            }
            else
            {
                Debug.Log($"【调试】当前阶段预设 addCount 为 0，无变化");
            }
        }

        // 按 F：减少当前阶段预设的敌人数量
        if (Input.GetKeyDown(removeKey))
        {
            int removeCount = enemySpawner.stageAdjustments[currentStageIndex].removeCount;
            if (removeCount > 0)
            {
                enemySpawner.RemoveEnemies(removeCount);
                Debug.Log($"【调试】删除 {removeCount} 个敌人（阶段{currentStageIndex}预设）");
            }
            else
            {
                Debug.Log($"【调试】当前阶段预设 removeCount 为 0，无变化");
            }
        }

        // 按住 T：清除所有敌人（按下即清除，不会连续触发）
        if (Input.GetKeyDown(clearAllKey))
        {
            enemySpawner.ClearAllEnemies();
            Debug.Log("【调试】已清除所有敌人");
        }
    }
}