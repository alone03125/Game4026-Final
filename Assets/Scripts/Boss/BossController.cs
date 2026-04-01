using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 控制器
/// 管理 Boss 的血量、三个战斗阶段、射击逻辑和关卡时间限制。
///
/// 使用说明：
///   1. 将此脚本挂载到 Boss GameObject 上。
///   2. 在 Inspector 中为各字段赋值（子弹预制体、发射点、水晶预制体等）。
///   3. Boss GameObject 需要有 Tag = "Boss"。
///   4. 玩家 GameObject 需要有 Tag = "Player" 并挂载 PlayerHealth 组件。
///   5. 若要让玩家攻击命中 Boss，可在玩家攻击物体上挂载 DamageSource 组件
///      并设置 Tag = "PlayerAttack"（近战）或 "PlayerBullet"（远程子弹）。
/// </summary>
public class BossController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // 事件
    // ─────────────────────────────────────────────

    /// <summary>Boss 死亡时触发，供 GameFlowManager 订阅。</summary>
    public static event Action OnBossDied;

    // ─────────────────────────────────────────────
    // 受击来源配置（Inspector 中可添加多条）
    // ─────────────────────────────────────────────

    [System.Serializable]
    public class DamageSourceConfig
    {
        [Tooltip("触发伤害的碰撞体 Tag")]
        public string tag = "PlayerBullet";
        [Tooltip("命中后是否销毁该物体")]
        public bool destroyOnHit = true;
    }

    // ─────────────────────────────────────────────
    // Inspector 可配置字段
    // ─────────────────────────────────────────────

    [Header("=== 受击来源配置 ===")]
    [Tooltip("可在此添加所有能对 Boss 造成伤害的来源，每条单独设置 Tag 和是否销毁")]
    public List<DamageSourceConfig> damageSources = new List<DamageSourceConfig>
    {
        new DamageSourceConfig { tag = "PlayerBullet", destroyOnHit = true }
    };

    [Header("=== Boss 基础属性 ===")]
    public float maxHealth = 300f;

    [Header("=== 阶段血量阈值 ===")]
    [Tooltip("进入第二阶段的血量（默认 200）")]
    public float phase2HealthThreshold = 200f;
    [Tooltip("进入第三阶段（狂暴）的血量（默认 100）")]
    public float phase3HealthThreshold = 100f;

    [Header("=== 子弹通用设置 ===")]
    [Tooltip("子弹预制体，需要挂载 Bullet1.cs 并有 Collider（Is Trigger = true）")]
    public GameObject bullet1Prefab;
    [Tooltip("子弹发射点（留空则从 Boss 自身位置发射）")]
    public Transform firePoint;
    [Tooltip("子弹飞行速度")]
    public float bullet1Speed = 15f;

    [Header("=== 第一阶段 —— 普通射击 ===")]
    [Tooltip("每秒发射子弹数")]
    public float phase1FireRate = 2f;
    [Tooltip("子弹伤害值")]
    public float phase1Bullet1Damage = 1f;

    [Header("=== 第二阶段 —— 护盾与水晶 ===")]
    [Tooltip("进入第二阶段时的无敌持续时间（秒）")]
    public float immunityDuration = 5f;
    [Tooltip("水晶预制体，每个水晶需要挂载 Crystal.cs")]
    public GameObject crystalPrefab;
    [Tooltip("4 个水晶生成点；若留空则自动在 Boss 前方生成")]
    public Transform[] crystalSpawnPoints;
    [Tooltip("自动生成模式下水晶距离 Boss 中心的半径（单位），默认 8）")]
    public float crystalSpawnRadius = 8f;
    [Tooltip("护盾存在时的伤害减免比例（0.5 = 伤害减半）")]
    [Range(0f, 1f)]
    public float shieldDamageReduction = 0.5f;
    [Tooltip("护盾激活时 Boss 身上的点光源（可选，留空忽略）")]
    public Light bossGlowLight;
    [Tooltip("护盾激活时 Boss 表面变成的颜色")]
    public Color shieldColor = new Color(0f, 1f, 1f);
    [Tooltip("护盾颜色淡入淡出速度")]
    public float shieldFadeSpeed = 3f;

    [Header("=== 第三阶段 —— 狂暴 ===")]
    [Tooltip("狂暴状态每秒发射子弹数")]
    public float phase3FireRate = 5f;
    [Tooltip("狂暴状态子弹伤害值")]
    public float phase3Bullet1Damage = 2f;
    [Tooltip("狂暴时的粒子效果（需预先设置好红色粒子系统）")]
    public ParticleSystem berserkParticleEffect;

    [Header("=== 关卡时间限制（可调整）===")]
    [Tooltip("超过此时间（秒）后 Boss 伤害翻倍，默认 300 秒 = 5 分钟")]
    public float doubleDamageTimeLimit = 300f;
    [Tooltip("超过此时间（秒）后 Boss 强制进入狂暴，默认 480 秒 = 8 分钟")]
    public float forceBerserkTimeLimit = 480f;

    // ─────────────────────────────────────────────
    // 私有状态变量
    // ─────────────────────────────────────────────

    // 血量
    private float currentHealth;

    // 阶段状态
    private int  currentPhase        = 1;
    private bool phase2Triggered     = false;
    private bool phase3Triggered     = false;

    // 护盾 / 免疫
    private bool isImmune            = false;
    private bool shieldActive        = false;

    // 狂暴
    private bool isBerserk           = false;

    // 时间限制
    private float bossStartTime;
    private bool  doubleDamageTriggered = false;
    private bool  forceBerserkTriggered = false;

    // 射击计时器
    private float fireTimer = 0f;

    // 护盾颜色渐变
    private Color bossOriginalColor;
    private Color bossColorTarget;

    // 引用
    private Transform     playerTransform;
    private List<Crystal> activeCrystals = new List<Crystal>();
    private Renderer      bossRenderer;

    // ─────────────────────────────────────────────
    // Unity 生命周期
    // ─────────────────────────────────────────────

    void Start()
    {
        currentHealth = maxHealth;
        bossStartTime = Time.time;

        // 寻找玩家
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;
        else
            Debug.LogWarning("[Boss] 未找到 Tag=Player 的玩家对象！");

        // 获取渲染器（用于发光效果）
        bossRenderer = GetComponentInChildren<Renderer>();

        // 记录 Boss 原始颜色
        if (bossRenderer != null)
        {
            bossOriginalColor = bossRenderer.material.color;
            bossColorTarget   = bossOriginalColor;
        }

        // 确保粒子效果和灯光默认关闭
        if (berserkParticleEffect != null) berserkParticleEffect.Stop();
        if (bossGlowLight          != null) bossGlowLight.enabled = false;
    }

    void Update()
    {
        HandleTimeLimits();
        HandleFacePlayer();
        HandleShooting();
        HandleShieldFade();
    }

    // ─────────────────────────────────────────────
    // 关卡时间限制
    // ─────────────────────────────────────────────

    void HandleTimeLimits()
    {
        float elapsed = Time.time - bossStartTime;

        // 超过 doubleDamageTimeLimit → Boss 伤害翻倍
        if (!doubleDamageTriggered && elapsed >= doubleDamageTimeLimit)
        {
            doubleDamageTriggered = true;
            phase1Bullet1Damage   *= 2f;
            phase3Bullet1Damage   *= 2f;
            Debug.Log($"[Boss] ⚠ 超时 {doubleDamageTimeLimit}s！Boss 伤害翻倍！");
        }

        // 超过 forceBerserkTimeLimit → 强制狂暴
        if (!forceBerserkTriggered && elapsed >= forceBerserkTimeLimit)
        {
            forceBerserkTriggered = true;
            if (!isBerserk)
            {
                EnterBerserkMode();
                Debug.Log($"[Boss] ⚠ 超时 {forceBerserkTimeLimit}s！Boss 强制进入狂暴！");
            }
        }
    }

    // ─────────────────────────────────────────────
    // 朝向玩家旋转
    // ─────────────────────────────────────────────

    void HandleFacePlayer()
    {
        if (playerTransform == null) return;

        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 5f);
        }
    }

    // ─────────────────────────────────────────────
    // 射击逻辑
    // ─────────────────────────────────────────────

    void HandleShooting()
    {
        if (playerTransform == null) return;
        if (isImmune)  return;   // 免疫期间停止射击

        float rate     = isBerserk ? phase3FireRate : phase1FireRate;
        float interval = 1f / rate;

        fireTimer += Time.deltaTime;
        if (fireTimer >= interval)
        {
            fireTimer = 0f;
            ShootAtPlayer();
        }
    }

    void ShootAtPlayer()
    {
        if (bullet1Prefab == null) return;

        Transform origin = (firePoint != null) ? firePoint : transform;
        Vector3   dir    = (playerTransform.position - origin.position).normalized;

        SpawnBullet(origin.position, dir);
    }

    void SpawnBullet(Vector3 position, Vector3 direction)
    {
        GameObject bullet1Obj = Instantiate(bullet1Prefab, position, Quaternion.LookRotation(direction));
        bullet1Obj.tag = "BossBullet";

        Bullet1 bullet1 = bullet1Obj.GetComponent<Bullet1>();
        if (bullet1 != null)
            bullet1.SetDirection(direction);
    }

    // ─────────────────────────────────────────────
    // 伤害与血量
    // ─────────────────────────────────────────────

    /// <summary>
    /// 对 Boss 造成伤害（外部调用入口）。
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isImmune) { Debug.Log("[Boss] 免疫中，无效伤害。"); return; }

        float actualDamage = damage;

        // 护盾减伤
        if (shieldActive)
        {
            actualDamage *= shieldDamageReduction;
            Debug.Log($"[Boss] 护盾减伤！原伤害 {damage} → 实际伤害 {actualDamage}");
        }

        currentHealth -= actualDamage;
        currentHealth  = Mathf.Clamp(currentHealth, 0f, maxHealth);

        Debug.Log($"[Boss] 受到伤害 {actualDamage}，当前血量：{currentHealth}/{maxHealth}");

        CheckPhaseTransitions();

        if (currentHealth <= 0f) Die();
    }

    void CheckPhaseTransitions()
    {
        if (!phase2Triggered && currentHealth <= phase2HealthThreshold)
        {
            phase2Triggered = true;
            StartCoroutine(Phase2Routine());
        }

        if (!phase3Triggered && currentHealth <= phase3HealthThreshold)
        {
            phase3Triggered = true;
            EnterPhase3();
        }
    }

    // ─────────────────────────────────────────────
    // 第二阶段：免疫 → 护盾 → 水晶
    // ─────────────────────────────────────────────

    IEnumerator Phase2Routine()
    {
        currentPhase = 2;
        Debug.Log("[Boss] ─── 进入第二阶段！开始 5 秒无敌期 ───");

        isImmune = true;
        yield return new WaitForSeconds(immunityDuration);
        isImmune = false;

        Debug.Log("[Boss] 无敌期结束，激活护盾与水晶！");
        ActivateShield();
    }

    void ActivateShield()
    {
        shieldActive = true;

        // ── 发光效果 ──────────────────────
        if (bossGlowLight != null)
        {
            bossGlowLight.enabled = true;
            bossGlowLight.color   = shieldColor;
        }

        // ── Boss 表面颜色渐变至护盾色 ─────
        bossColorTarget = shieldColor;

        // 启用材质自发光（Standard / URP Lit Shader）
        if (bossRenderer != null)
        {
            bossRenderer.material.EnableKeyword("_EMISSION");
            bossRenderer.material.SetColor("_EmissionColor", shieldColor * 2f);
        }

        // ── 生成水晶 ──────────────────────
        SpawnCrystals();

        Debug.Log("[Boss] 护盾已激活，4 个水晶已生成！");
    }

    void SpawnCrystals()
    {
        activeCrystals.Clear();

        if (crystalPrefab == null)
        {
            Debug.LogError("[Boss] 未设置 crystalPrefab！");
            return;
        }

        const int CRYSTAL_COUNT = 4;

        if (crystalSpawnPoints != null && crystalSpawnPoints.Length >= CRYSTAL_COUNT)
        {
            // 使用 Inspector 中预设的生成点
            for (int i = 0; i < CRYSTAL_COUNT; i++)
            {
                if (crystalSpawnPoints[i] != null)
                    CreateCrystal(crystalSpawnPoints[i].position, crystalSpawnPoints[i].rotation);
            }
        }
        else
        {
            // 自动在 Boss 周围5格的地方扇形排布
            Debug.LogWarning("[Boss] 未设置足够的 crystalSpawnPoints，将自动生成在 Boss 周围。");
            
            // 获取 Boss 的碰撞体半径
            Collider bossCollider = GetComponent<Collider>();
            float bossRadius = 1f; // 默认半径
            if (bossCollider != null)
            {
                // 使用碰撞体边界的最大范围作为Boss半径
                bossRadius = bossCollider.bounds.extents.magnitude;
            }
            
            // Boss 边界到水晶的距离为 5 格
            float spawnDistance = bossRadius + 5f;
            
            for (int i = 0; i < CRYSTAL_COUNT; i++)
            {
                float   angle    = (i - (CRYSTAL_COUNT - 1) * 0.5f) * 90f;  // 改为90度间隔，均匀分布4个
                Vector3 offset   = Quaternion.Euler(0f, angle, 0f) * transform.forward * spawnDistance;
                Vector3 spawnPos = transform.position + offset + Vector3.up * 0.5f;
                CreateCrystal(spawnPos, Quaternion.identity);
            }
        }
    }

    void CreateCrystal(Vector3 position, Quaternion rotation)
    {
        GameObject obj    = Instantiate(crystalPrefab, position, rotation);
        Crystal    crystal = obj.GetComponent<Crystal>();
        if (crystal != null)
        {
            crystal.RegisterBoss(this);
            activeCrystals.Add(crystal);
        }
    }

    /// <summary>
    /// 当某个水晶被摧毁时，由 Crystal.cs 回调此方法。
    /// </summary>
    public void OnCrystalDestroyed(Crystal crystal)
    {
        activeCrystals.Remove(crystal);
        Debug.Log($"[Boss] 水晶被摧毁！剩余：{activeCrystals.Count} 个");

        if (activeCrystals.Count <= 0)
            DeactivateShield();
    }

    void DeactivateShield()
    {
        shieldActive = false;

        if (bossGlowLight != null) bossGlowLight.enabled = false;

        if (bossRenderer != null)
            bossRenderer.material.DisableKeyword("_EMISSION");

        // Boss 表面颜色渐变回原色
        bossColorTarget = bossOriginalColor;

        Debug.Log("[Boss] 所有水晶被摧毁，护盾解除！");
    }

    // ─────────────────────────────────────────────
    // 护盾颜色淡入 / 淡出
    // ─────────────────────────────────────────────

    void HandleShieldFade()
    {
        if (bossRenderer == null) return;

        // 每帧平滑插值 Boss 表面颜色
        Color current = bossRenderer.material.color;
        if (current != bossColorTarget)
            bossRenderer.material.color = Color.Lerp(current, bossColorTarget, shieldFadeSpeed * Time.deltaTime);
    }

    // ─────────────────────────────────────────────
    // 第三阶段：狂暴
    // ─────────────────────────────────────────────

    void EnterPhase3()
    {
        currentPhase = 3;
        Debug.Log("[Boss] ─── 进入第三阶段！─── ");
        EnterBerserkMode();
    }

    void EnterBerserkMode()
    {
        if (isBerserk) return;
        isBerserk = true;

        if (berserkParticleEffect != null)
        {
            // 将粒子颜色改为红色（如已在粒子系统中配置则忽略此步）
            var main = berserkParticleEffect.main;
            main.startColor = Color.red;
            berserkParticleEffect.Play();
        }

        Debug.Log("[Boss] 进入狂暴！攻击频率 → 5发/秒，伤害 → 2");
    }

    // ─────────────────────────────────────────────
    // 死亡
    // ─────────────────────────────────────────────

    void Die()
    {
        Debug.Log("[Boss] Boss 被击败！");

        // 清除所有水晶
        foreach (Crystal c in activeCrystals)
            if (c != null) Destroy(c.gameObject);
        activeCrystals.Clear();

        if (berserkParticleEffect != null) berserkParticleEffect.Stop();
        if (bossGlowLight          != null) bossGlowLight.enabled = false;

        // TODO：播放死亡动画、掉落奖励、触发过场动画等
        OnBossDied?.Invoke();
        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    // 被玩家攻击命中（通过碰撞体检测）
    // ─────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        foreach (DamageSourceConfig source in damageSources)
        {
            bool matched = false;
            try { matched = other.CompareTag(source.tag); }
            catch { Debug.LogWarning($"[Boss] Tag '{source.tag}' 未定义，跳过。"); continue; }

            if (matched)
            {
                DamageSource ds = other.GetComponent<DamageSource>();
                float dmg = (ds != null) ? ds.damage : 1f;
                TakeDamage(dmg);

                if (source.destroyOnHit)
                    Destroy(other.gameObject);

                break;
            }
        }
    }

    // ─────────────────────────────────────────────
    // 公开查询接口
    // ─────────────────────────────────────────────

    public float GetCurrentHealth()  => currentHealth;
    public float GetMaxHealth()      => maxHealth;
    public float GetHealthPercent()  => currentHealth / maxHealth;
    public int   GetCurrentPhase()   => currentPhase;
    public bool  IsShieldActive()    => shieldActive;
    public bool  IsBerserk()         => isBerserk;
    public bool  IsImmune()          => isImmune;

    /// <summary>返回关卡已经过的时间（秒）</summary>
    public float GetElapsedTime()    => Time.time - bossStartTime;

    /// <summary>返回距离伤害翻倍的剩余秒数（已触发则返回 0）</summary>
    public float GetTimeUntilDoubleDamage()
        => doubleDamageTriggered ? 0f : Mathf.Max(0f, doubleDamageTimeLimit - GetElapsedTime());

    /// <summary>返回距离强制狂暴的剩余秒数（已触发则返回 0）</summary>
    public float GetTimeUntilForceBerserk()
        => forceBerserkTriggered ? 0f : Mathf.Max(0f, forceBerserkTimeLimit - GetElapsedTime());
}
