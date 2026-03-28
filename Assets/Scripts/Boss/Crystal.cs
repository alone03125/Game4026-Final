using UnityEngine;

/// <summary>
/// 护盾水晶逻辑
/// 
/// 使用说明：
///   1. 创建一个水晶外形的 GameObject 作为水晶预制体。
///   2. 添加 Collider 组件，勾选 Is Trigger = true。
///   3. 将此脚本挂载到预制体上。
///   4. 为预制体设置 Tag = "Crystal"。
///   5. 将预制体拖入 BossController 的 Crystal Prefab 字段。
///
/// 被玩家攻击命中的方式：
///   - 玩家近战攻击碰撞体设 Tag = "PlayerAttack"，并挂载 DamageSource.cs
///   - 玩家子弹设 Tag = "PlayerBullet"，并挂载 DamageSource.cs
///
/// 水晶只会在 Boss 血量降到 200（phase2HealthThreshold）时由 BossController 生成，
/// 因此"只有 Boss 血量下降到 200 才会显现"由 BossController 保证。
/// </summary>
[RequireComponent(typeof(Collider))]
public class Crystal : MonoBehaviour
{
    [Header("=== 水晶属性 ===")]
    [Tooltip("水晶最大血量")]
    public float maxHealth = 30f;

    [Header("=== 视觉效果 ===")]
    [Tooltip("水晶正常颜色")]
    public Color normalColor = new Color(0f, 1f, 1f, 0.8f);   // 青色半透明
    [Tooltip("水晶濒死颜色（血量归零时显示此颜色）")]
    public Color damagedColor = Color.red;
    [Tooltip("被摧毁时播放的粒子效果预制体（可选）")]
    public ParticleSystem destroyEffectPrefab;

    // ─────────────────────────────────────────────
    // 私有状态
    // ─────────────────────────────────────────────
    private float          currentHealth;
    private BossController registeredBoss;
    private Renderer       crystalRenderer;

    // ─────────────────────────────────────────────
    // Unity 生命周期
    // ─────────────────────────────────────────────

    void Awake()
    {
        currentHealth   = maxHealth;
        crystalRenderer = GetComponentInChildren<Renderer>();

        // 初始化颜色
        if (crystalRenderer != null)
            crystalRenderer.material.color = normalColor;
    }

    // ─────────────────────────────────────────────
    // 注册所属 Boss（由 BossController 在生成水晶后调用）
    // ─────────────────────────────────────────────

    /// <summary>
    /// 绑定管理此水晶的 Boss，Boss 销毁水晶时需要通知。
    /// </summary>
    public void RegisterBoss(BossController boss)
    {
        registeredBoss = boss;
    }

    // ─────────────────────────────────────────────
    // 伤害处理
    // ─────────────────────────────────────────────

    /// <summary>
    /// 对水晶造成伤害（公开接口，也可由外部系统直接调用）。
    /// </summary>
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth  = Mathf.Max(currentHealth, 0f);

        Debug.Log($"[Crystal] 受到伤害 {damage}，剩余血量：{currentHealth}/{maxHealth}");

        UpdateVisual();

        if (currentHealth <= 0f)
            Explode();
    }

    // ─────────────────────────────────────────────
    // 视觉反馈
    // ─────────────────────────────────────────────

    void UpdateVisual()
    {
        if (crystalRenderer == null) return;

        // 根据血量百分比在正常色和濒死色之间插值
        float t = currentHealth / maxHealth;
        crystalRenderer.material.color = Color.Lerp(damagedColor, normalColor, t);
    }

    // ─────────────────────────────────────────────
    // 爆碎销毁
    // ─────────────────────────────────────────────

    void Explode()
    {
        Debug.Log("[Crystal] 水晶被摧毁！");

        // 播放爆炸粒子效果（若有）
        if (destroyEffectPrefab != null)
        {
            ParticleSystem fx = Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax + 0.5f);
        }

        // 通知 Boss 这颗水晶已被摧毁
        if (registeredBoss != null)
            registeredBoss.OnCrystalDestroyed(this);

        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────
    // 碰撞检测（接受玩家攻击）
    // ─────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        // 玩家近战攻击碰撞体（Tag = "PlayerAttack"，需挂载 DamageSource）
        if (other.CompareTag("PlayerAttack"))
        {
            DamageSource ds = other.GetComponent<DamageSource>();
            TakeDamage(ds != null ? ds.damage : 1f);
        }
        // 玩家子弹（Tag = "PlayerBullet"，需挂载 DamageSource）
        else if (other.CompareTag("PlayerBullet"))
        {
            DamageSource ds = other.GetComponent<DamageSource>();
            TakeDamage(ds != null ? ds.damage : 1f);
            Destroy(other.gameObject);   // 子弹命中后销毁
        }
    }

    // ─────────────────────────────────────────────
    // 公开查询
    // ─────────────────────────────────────────────

    public float GetCurrentHealth()  => currentHealth;
    public float GetHealthPercent()  => currentHealth / maxHealth;
}
