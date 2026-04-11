using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD 管理器：读取各系统数据并更新 Canvas 上的 UI 元素。
/// 进度条使用 Image.fillAmount（Image Type 设为 Filled，Fill Method = Horizontal，Fill Origin = Left）。
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ─── 引用 ─────────────────────────────────────

    [Header("=== 数据来源（自动查找，也可手动拖入）===")]
    public PlayerHealth playerHealth;
    public RTShoot rtShoot;
    public GameFlowManager gameFlowManager;

    // ─── 护盾 UI ──────────────────────────────────

    [Header("=== 护盾 ===")]
    [Tooltip("护盾进度条 Image（Filled 类型）")]
    public Image shieldBar;
    [Tooltip("护盾数值文本")]
    public TextMeshProUGUI shieldText;

    // ─── 生命值 UI ────────────────────────────────

    [Header("=== 生命值 ===")]
    [Tooltip("生命值进度条 Image（Filled 类型）")]
    public Image healthBar;
    [Tooltip("生命值数值文本")]
    public TextMeshProUGUI healthText;

    // ─── 过热 UI ──────────────────────────────────

    [Header("=== 武器过热 ===")]
    [Tooltip("过热进度条 Image（Filled 类型）")]
    public Image heatBar;
    [Tooltip("过热百分比文本")]
    public TextMeshProUGUI heatText;
    [Tooltip("过热闪烁速度（次/秒）")]
    public float overheatBlinkSpeed = 4f;
    [Tooltip("闪烁时最低 Alpha")]
    [Range(0f, 1f)]
    public float overheatBlinkMinAlpha = 0.2f;

    // ─── 任务 UI ──────────────────────────────────

    [Header("=== 任务显示 ===")]
    [Tooltip("任务文本（TMP）")]
    public TextMeshProUGUI missionText;

    // ─── Boss UI ──────────────────────────────────

    [Header("=== Boss 生命值 ===")]
    [Tooltip("Boss 血条根物体（Boss 阶段显示，其他阶段隐藏）")]
    public GameObject bossHealthRoot;
    [Tooltip("Boss 生命值进度条 Image（Filled 类型）")]
    public Image bossHealthBar;
    [Tooltip("Boss 生命值数值文本")]
    public TextMeshProUGUI bossHealthText;
    [Tooltip("Boss 名称与阶段文本")]
    public TextMeshProUGUI bossPhaseText;
    [Tooltip("最终阶段闪烁速度（次/秒）")]
    public float phaseBlinkSpeed = 3f;
    [Tooltip("最终阶段闪烁最低 Alpha")]
    [Range(0f, 1f)]
    public float phaseBlinkMinAlpha = 0.2f;

    // ─── 用于查找玩家位置（计算信标距离）──────────

    [Header("=== 玩家位置参考 ===")]
    [Tooltip("用于计算信标距离的玩家 Transform（留空则自动查找 PlayerBody Tag）")]
    public Transform playerTransform;

    // ─── 内部状态 ─────────────────────────────────

    private BossController _bossController;
    private bool _heatBlinking;
    private bool _phaseBlinking;
    private Color _phaseOriginalColor;

    // ─────────────────────────────────────────────
    // 生命周期
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
        // 自动查找未赋值的引用
        if (playerHealth == null) playerHealth = FindObjectOfType<PlayerHealth>();
        if (rtShoot == null) rtShoot = FindObjectOfType<RTShoot>();
        if (gameFlowManager == null) gameFlowManager = GameFlowManager.Instance;

        if (playerTransform == null)
        {
            GameObject pb = GameObject.FindGameObjectWithTag("PlayerBody");
            if (pb != null) playerTransform = pb.transform;
        }

        // Boss UI 默认隐藏
        if (bossHealthRoot != null) bossHealthRoot.SetActive(false);
    }

    void Update()
    {
        UpdateShieldUI();
        UpdateHealthUI();
        UpdateHeatUI();
        UpdateMissionUI();
        UpdateBossUI();
    }

    // ─────────────────────────────────────────────
    // 护盾
    // ─────────────────────────────────────────────

    void UpdateShieldUI()
    {
        if (playerHealth == null) return;

        int current = playerHealth.GetShieldCount();
        int max = playerHealth.GetMaxShieldCount();
        float ratio = max > 0 ? (float)current / max : 0f;

        if (shieldBar != null) shieldBar.fillAmount = ratio;
        if (shieldText != null) shieldText.text = $"{current} / {max}";
    }

    // ─────────────────────────────────────────────
    // 生命值
    // ─────────────────────────────────────────────

    void UpdateHealthUI()
    {
        if (playerHealth == null) return;

        float current = playerHealth.GetCurrentHealth();
        float max = playerHealth.maxHealth;
        float ratio = max > 0f ? current / max : 0f;

        if (healthBar != null) healthBar.fillAmount = ratio;
        if (healthText != null) healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    // ─────────────────────────────────────────────
    // 过热
    // ─────────────────────────────────────────────

    void UpdateHeatUI()
    {
        if (rtShoot == null) return;

        float heatPercent = rtShoot.GetHeatPercent();
        bool overheated = rtShoot.IsOverheated();

        if (heatBar != null) heatBar.fillAmount = heatPercent;
        if (heatText != null) heatText.text = $"{Mathf.RoundToInt(heatPercent * 100f)}%";

        // 过热闪烁
        if (overheated)
        {
            float alpha = Mathf.Lerp(overheatBlinkMinAlpha, 1f,
                (Mathf.Sin(Time.time * overheatBlinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f);

            SetAlpha(heatBar, alpha);
            SetAlpha(heatText, alpha);
            _heatBlinking = true;
        }
        else if (_heatBlinking)
        {
            // 冷却完成，恢复常态
            SetAlpha(heatBar, 1f);
            SetAlpha(heatText, 1f);
            _heatBlinking = false;
        }
    }

    // ─────────────────────────────────────────────
    // 任务
    // ─────────────────────────────────────────────

    void UpdateMissionUI()
    {
        if (gameFlowManager == null || missionText == null) return;

        var state = gameFlowManager.GetCurrentState();

        switch (state)
        {
            case GameFlowManager.FlowState.WaitingForSequence:
                missionText.gameObject.SetActive(true);
                missionText.text = "Mission: Activate the system [ABBBBCD]";
                break;

            case GameFlowManager.FlowState.Stage1:
            case GameFlowManager.FlowState.Stage2:
            case GameFlowManager.FlowState.Stage3:
                missionText.gameObject.SetActive(true);
                int kills = gameFlowManager.GetCurrentKills();
                int required = gameFlowManager.GetRequiredKills();
                missionText.text = $"Mission: Defeat the enemy [{kills}/{required}]";
                break;

            case GameFlowManager.FlowState.Beacon1:
            case GameFlowManager.FlowState.Beacon2:
            case GameFlowManager.FlowState.Beacon3:
                missionText.gameObject.SetActive(true);
                float dist = GetBeaconDistance();
                missionText.text = dist >= 0f
                    ? $"Mission: Find the pivot [{dist:F0}m]"
                    : "Mission: Find the pivot";
                break;

            case GameFlowManager.FlowState.BossStage:
                // Boss 阶段隐藏任务文本
                missionText.gameObject.SetActive(false);
                break;

            case GameFlowManager.FlowState.GameOver:
                missionText.gameObject.SetActive(true);
                missionText.text = "Mission: Complete";
                break;
        }
    }

    // ─────────────────────────────────────────────
    // Boss
    // ─────────────────────────────────────────────

    void UpdateBossUI()
    {
        var state = gameFlowManager != null ? gameFlowManager.GetCurrentState() : GameFlowManager.FlowState.GameOver;

        bool showBoss = state == GameFlowManager.FlowState.BossStage;
        if (bossHealthRoot != null && bossHealthRoot.activeSelf != showBoss)
            bossHealthRoot.SetActive(showBoss);

        if (!showBoss) { _bossController = null; return; }

        // 懒查找 BossController
        if (_bossController == null)
        {
            GameObject boss = gameFlowManager.GetActiveBoss();
            if (boss != null) _bossController = boss.GetComponent<BossController>();
        }
        if (_bossController == null) return;

        float current = _bossController.GetCurrentHealth();
        float max = _bossController.GetMaxHealth();
        float ratio = max > 0f ? current / max : 0f;

        if (bossHealthBar != null) bossHealthBar.fillAmount = ratio;
        if (bossHealthText != null)
            bossHealthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";

        // 阶段名称显示
        int phase = _bossController.GetCurrentPhase();
        if (bossPhaseText != null)
        {
            bossPhaseText.text = $"TAMER MACHINE 2000 - Phase {phase}";

            // 最终阶段（Phase 3）变红并持续闪烁
            if (phase >= 3)
            {
                if (!_phaseBlinking)
                {
                    _phaseOriginalColor = bossPhaseText.color;
                    bossPhaseText.color = Color.red;
                    _phaseBlinking = true;
                }
                float alpha = Mathf.Lerp(phaseBlinkMinAlpha, 1f,
                    (Mathf.Sin(Time.time * phaseBlinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f);
                SetAlpha(bossPhaseText, alpha);
            }
            else if (_phaseBlinking)
            {
                bossPhaseText.color = _phaseOriginalColor;
                SetAlpha(bossPhaseText, 1f);
                _phaseBlinking = false;
            }
        }
    }

    // ─────────────────────────────────────────────
    // 工具
    // ─────────────────────────────────────────────

    float GetBeaconDistance()
    {
        if (playerTransform == null || gameFlowManager == null) return -1f;

        GameObject beacon = gameFlowManager.GetActiveBeacon();
        if (beacon == null) return -1f;

        // XZ 平面距离
        Vector3 pPos = playerTransform.position;
        Vector3 bPos = beacon.transform.position;
        float dx = pPos.x - bPos.x;
        float dz = pPos.z - bPos.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    static void SetAlpha(TextMeshProUGUI tmp, float alpha)
    {
        if (tmp == null) return;
        Color c = tmp.color;
        c.a = alpha;
        tmp.color = c;
    }
}
