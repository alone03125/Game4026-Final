using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// 管理整体游戏流程：
///   等待Trigger → 教程(视角→移动→序列→射击) → 阶段1 → 信标1 → 阶段2 → 信标2 → 阶段3 → 信标3 → Boss阶段 → 游戏结束
/// 依赖：SequenceManager、GameManager（配置数据）、EnemySpawner、BossController、RTShoot
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    // ─────────────────────────────────────────────
    // 流程状态
    // ─────────────────────────────────────────────

    public enum FlowState
    {
        WaitingForTrigger,  // 等待按下 Trigger 开始游戏
        TutorialLook,       // 教程：拉动右摇杆调整视角
        TutorialMove,       // 教程：移动到信标位置
        TutorialSequence,   // 教程：输入 ABBCD 序列
        TutorialShoot,      // 教程：射击
        Stage1,             // 第一战斗阶段
        Beacon1,            // 信标过渡（1→2）
        Stage2,             // 第二战斗阶段
        Beacon2,            // 信标过渡（2→3）
        Stage3,             // 第三战斗阶段
        Beacon3,            // 信标过渡（3→Boss）
        BossStage,          // Boss 阶段
        GameOver            // 游戏结束
    }

    // ─────────────────────────────────────────────
    // Inspector 配置
    // ─────────────────────────────────────────────

    [Header("=== 引用 ===")]
    public GameManager gameManager;
    public EnemySpawner enemySpawner;

    [Header("=== 信标设置 ===")]
    [Tooltip("带有 Trigger 碰撞体的信标 Prefab")]
    public GameObject beaconPrefab;
    [Tooltip("阶段 1 结束后信标生成的世界坐标")]
    public Vector3 beacon1Position = Vector3.zero;
    [Tooltip("阶段 2 结束后信标生成的世界坐标")]
    public Vector3 beacon2Position = Vector3.zero;
    [Tooltip("阶段 3 结束后信标生成的世界坐标")]
    public Vector3 beacon3Position = Vector3.zero;

    [Header("=== 教程信标设置 ===")]
    [Tooltip("教程阶段信标生成的世界坐标")]
    public Vector3 tutorialBeaconPosition = Vector3.zero;

    [Header("=== Boss 设置 ===")]
    [Tooltip("Boss Prefab（需挂载 BossController）")]
    public GameObject bossPrefab;
    [Tooltip("Boss 生成的世界坐标")]
    public Vector3 bossSpawnPosition = Vector3.zero;

    // ★ 新增：Boss 生成特效
    [Header("=== Boss 生成特效 ===")]
    [Tooltip("Boss 生成时在其位置播放的特效 Prefab（如粒子系统）")]
    public GameObject bossSpawnEffect;
    [Tooltip("特效自动销毁的延迟时间（秒），设为 0 则不自动销毁")]
    public float bossSpawnEffectDuration = 3f;

    // ─────────────────────────────────────────────
    // 私有状态
    // ─────────────────────────────────────────────

    private FlowState currentState = FlowState.WaitingForTrigger;
    private int currentStageIndex = 0;  // 0=阶段1，1=阶段2，2=阶段3
    private int currentKills = 0;

    private GameObject activeBeacon;
    private GameObject activeBoss;

    // XR 输入设备
    private InputDevice rightHandDevice;
    private InputDevice leftHandDevice;
    private bool _triggerWasPressed;

    // ─────────────────────────────────────────────
    // Unity 生命周期
    // ─────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        ValidateReferences();

        // 订阅 Boss 死亡事件
        BossController.OnBossDied += OnBossKilled;
        // 订阅转向事件（教程步骤1）
        CockpitSimpleTurn.OnTurnInputDetected += OnTutorialTurnDetected;
    }

    void OnDestroy()
    {
        BossController.OnBossDied -= OnBossKilled;
        RTShoot.OnShotFired -= OnTutorialShotFired;
        CockpitSimpleTurn.OnTurnInputDetected -= OnTutorialTurnDetected;
    }

    void Update()
    {
        TryGetXRDevices();

        switch (currentState)
        {
            case FlowState.WaitingForTrigger:
                UpdateWaitingForTrigger();
                break;
            // TutorialLook — 由 CockpitSimpleTurn.OnTurnInputDetected 事件驱动
            // TutorialMove — 由信标触发回调驱动
            // TutorialSequence — 由 SequenceManager 回调驱动
            // TutorialShoot — 由 RTShoot.OnShotFired 回调驱动
        }
    }

    void TryGetXRDevices()
    {
        if (!rightHandDevice.isValid)
            rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!leftHandDevice.isValid)
            leftHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
    }

    // ─────────────────────────────────────────────
    // 教程：等待 Trigger 开始
    // ─────────────────────────────────────────────

    void UpdateWaitingForTrigger()
    {
        bool triggerPressed = false;
        rightHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed);

        if (triggerPressed && !_triggerWasPressed)
        {
            Debug.Log("[GameFlowManager] Trigger pressed, starting tutorial!");
            currentState = FlowState.TutorialLook;
        }
        _triggerWasPressed = triggerPressed;
    }

    // ─────────────────────────────────────────────
    // 教程步骤 1：调整视角（CockpitSimpleTurn 事件）
    // ─────────────────────────────────────────────

    void OnTutorialTurnDetected()
    {
        if (currentState != FlowState.TutorialLook) return;

        Debug.Log("[GameFlowManager] Tutorial: Turn input detected, proceeding to move step.");
        StartTutorialMove();
    }

    // ─────────────────────────────────────────────
    // 教程步骤 2：移动到信标（左摇杆）
    // ─────────────────────────────────────────────

    void StartTutorialMove()
    {
        currentState = FlowState.TutorialMove;

        if (beaconPrefab == null)
        {
            Debug.LogError("[GameFlowManager] beaconPrefab 未赋值！");
            return;
        }

        activeBeacon = Instantiate(beaconPrefab, tutorialBeaconPosition, Quaternion.identity);
        BeaconTrigger trigger = activeBeacon.GetComponent<BeaconTrigger>();
        if (trigger == null) trigger = activeBeacon.AddComponent<BeaconTrigger>();
        trigger.OnPlayerEntered = OnTutorialBeaconTriggered;

        Debug.Log($"[GameFlowManager] Tutorial beacon spawned at {tutorialBeaconPosition}");
    }

    void OnTutorialBeaconTriggered()
    {
        if (activeBeacon != null)
        {
            Destroy(activeBeacon);
            activeBeacon = null;
        }
        StartTutorialSequence();
    }

    // ─────────────────────────────────────────────
    // 教程步骤 3：输入 ABBCD 序列
    // ─────────────────────────────────────────────

    void StartTutorialSequence()
    {
        currentState = FlowState.TutorialSequence;

        if (SequenceManager.Instance != null)
            SequenceManager.Instance.RegisterPattern("ABBCD", OnTutorialSequenceCompleted);
        else
            Debug.LogError("[GameFlowManager] SequenceManager.Instance 未找到！");
    }

    void OnTutorialSequenceCompleted()
    {
        if (currentState != FlowState.TutorialSequence) return;

        Debug.Log("[GameFlowManager] Tutorial: ABBCD sequence completed!");
        StartTutorialShoot();
    }

    // ─────────────────────────────────────────────
    // 教程步骤 4：射击
    // ─────────────────────────────────────────────

    void StartTutorialShoot()
    {
        currentState = FlowState.TutorialShoot;
        RTShoot.OnShotFired += OnTutorialShotFired;
    }

    void OnTutorialShotFired()
    {
        if (currentState != FlowState.TutorialShoot) return;

        RTShoot.OnShotFired -= OnTutorialShotFired;
        Debug.Log("[GameFlowManager] Tutorial: Shot fired, starting Stage 1!");
        StartCombatStage(0);
    }

    // ─────────────────────────────────────────────
    // 战斗阶段
    // ─────────────────────────────────────────────

    void StartCombatStage(int stageIndex)
    {
        currentStageIndex = stageIndex;
        currentKills = 0;

        currentState = stageIndex == 0 ? FlowState.Stage1
                     : stageIndex == 1 ? FlowState.Stage2
                     :                   FlowState.Stage3;

        // 配置生成器
        enemySpawner.SetStage(stageIndex);
        if (stageIndex < gameManager.spawnIntervalsPerStage.Length)
            enemySpawner.SetSpawnInterval(gameManager.spawnIntervalsPerStage[stageIndex]);

        // 开启持续生成
        enemySpawner.EnableSpawning();

        // 立即生成初始敌人
        int spawnCount = gameManager.enemiesToSpawnOnStageStart[stageIndex];
        for (int i = 0; i < spawnCount; i++)
            enemySpawner.SpawnEnemyImmediate();

        Debug.Log($"[GameFlowManager] 战斗阶段 {stageIndex + 1} 开始，" +
                  $"初始生成 {spawnCount} 个敌人，" +
                  $"需击败 {gameManager.killsRequiredForNextStage[stageIndex]} 个以进入下一阶段");
    }

    // ─────────────────────────────────────────────
    // 击杀计数（由 GameManager.OnEnemyKilled 转发调用）
    // ─────────────────────────────────────────────

    public void OnEnemyKilled()
    {
        bool isCombatState = currentState == FlowState.Stage1
                          || currentState == FlowState.Stage2
                          || currentState == FlowState.Stage3;
        if (!isCombatState) return;

        currentKills++;
        int required = gameManager.killsRequiredForNextStage[currentStageIndex];
        Debug.Log($"[GameFlowManager] 击败敌人 {currentKills}/{required}");

        if (currentKills >= required)
            OnStageKillGoalReached();
    }

    // ─────────────────────────────────────────────
    // 阶段击杀目标达成
    // ─────────────────────────────────────────────

    void OnStageKillGoalReached()
    {
        // 关闭持续生成，再清除场上所有敌人
        enemySpawner.DisableSpawning();
        enemySpawner.ClearAllEnemies();

        // 切换到信标等待状态
        currentState = currentStageIndex == 0 ? FlowState.Beacon1
                     : currentStageIndex == 1 ? FlowState.Beacon2
                     :                          FlowState.Beacon3;

        // 确定信标生成位置
        Vector3 beaconPos = currentStageIndex == 0 ? beacon1Position
                          : currentStageIndex == 1 ? beacon2Position
                          :                          beacon3Position;

        // 生成信标
        if (beaconPrefab == null)
        {
            Debug.LogError("[GameFlowManager] beaconPrefab 未赋值！");
            return;
        }

        activeBeacon = Instantiate(beaconPrefab, beaconPos, Quaternion.identity);

        // 挂载 / 获取触发器组件并注册回调
        BeaconTrigger trigger = activeBeacon.GetComponent<BeaconTrigger>();
        if (trigger == null) trigger = activeBeacon.AddComponent<BeaconTrigger>();
        trigger.OnPlayerEntered = OnBeaconTriggered;

        Debug.Log($"[GameFlowManager] 阶段 {currentStageIndex + 1} 完成，信标已在 {beaconPos} 生成，等待玩家触碰");
    }

    // ─────────────────────────────────────────────
    // 玩家触碰信标
    // ─────────────────────────────────────────────

    void OnBeaconTriggered()
    {
        if (activeBeacon != null)
        {
            Destroy(activeBeacon);
            activeBeacon = null;
        }

        int nextStageIndex = currentStageIndex + 1;

        if (nextStageIndex < 3)
        {
            // 进入下一战斗阶段
            StartCombatStage(nextStageIndex);
        }
        else
        {
            // 全部战斗阶段完成，进入第四阶段（Boss）
            StartBossStage();
        }
    }

    // ─────────────────────────────────────────────
    // Boss 阶段（第四阶段）
    // ─────────────────────────────────────────────

    void StartBossStage()
    {
        currentState = FlowState.BossStage;

        if (bossPrefab == null)
        {
            Debug.LogError("[GameFlowManager] bossPrefab 未赋值！");
            return;
        }

        // ★ 新增：在 Boss 生成位置播放特效
        PlayBossSpawnEffect(bossSpawnPosition);

        activeBoss = Instantiate(bossPrefab, bossSpawnPosition, Quaternion.identity);

        // Boss 阶段持续生成少量敌人干扰玩家
        if (enemySpawner != null && gameManager != null)
        {
            // 切换到 Boss 阶段生成范围（若 useBossStageRange 未启用则自动回退到最后一个普通阶段）
            enemySpawner.SetBossStage();
            enemySpawner.maxEnemyCount = gameManager.bossStageMaxEnemies;
            enemySpawner.SetSpawnInterval(gameManager.bossStageSpawnInterval);
            enemySpawner.EnableSpawning();

            for (int i = 0; i < gameManager.bossStageInitialSpawn; i++)
                enemySpawner.SpawnEnemyImmediate();
        }

        Debug.Log($"[GameFlowManager] Boss 已在 {bossSpawnPosition} 生成，第四阶段（Boss关）开始！");
    }

    // ★ 新增：Boss 生成特效播放方法
    /// <summary>
    /// 在指定位置实例化 Boss 生成特效，并在 bossSpawnEffectDuration 秒后自动销毁。
    /// </summary>
    void PlayBossSpawnEffect(Vector3 position)
    {
        if (bossSpawnEffect == null)
        {
            Debug.LogWarning("[GameFlowManager] bossSpawnEffect 未赋值，跳过特效播放。");
            return;
        }

        GameObject effect = Instantiate(bossSpawnEffect, position, Quaternion.identity);

        if (bossSpawnEffectDuration > 0f)
            Destroy(effect, bossSpawnEffectDuration);

        Debug.Log($"[GameFlowManager] Boss 生成特效已在 {position} 播放，{bossSpawnEffectDuration}s 后销毁。");
    }

    void OnBossKilled()
    {
        if (currentState != FlowState.BossStage) return;

        // 停止生成并清除场上所有小怪
        if (enemySpawner != null)
        {
            enemySpawner.DisableSpawning();
            enemySpawner.ClearAllEnemies();
        }

        currentState = FlowState.GameOver;
        Debug.Log("[GameFlowManager] Boss 已被击败！游戏结束。");
    }

    // ─────────────────────────────────────────────
    // 工具
    // ─────────────────────────────────────────────

    public FlowState GetCurrentState() => currentState;

    public int GetCurrentKills() => currentKills;

    public int GetRequiredKills()
    {
        if (gameManager != null && currentStageIndex < gameManager.killsRequiredForNextStage.Length)
            return gameManager.killsRequiredForNextStage[currentStageIndex];
        return 0;
    }

    public GameObject GetActiveBeacon() => activeBeacon;
    public GameObject GetActiveBoss()   => activeBoss;

    void ValidateReferences()
    {
        if (gameManager == null)
            Debug.LogError("[GameFlowManager] gameManager 未赋值！");
        if (enemySpawner == null)
            Debug.LogError("[GameFlowManager] enemySpawner 未赋值！");
        if (beaconPrefab == null)
            Debug.LogWarning("[GameFlowManager] beaconPrefab 未赋值，信标将无法生成。");
        if (bossPrefab == null)
            Debug.LogWarning("[GameFlowManager] bossPrefab 未赋值，Boss 将无法生成。");
        if (bossSpawnEffect == null)
            Debug.LogWarning("[GameFlowManager] bossSpawnEffect 未赋值，Boss 生成时将没有特效。");
    }
}