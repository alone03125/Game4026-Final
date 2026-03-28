using UnityEngine;

/// <summary>
/// 子弹逻辑（Boss 发射的子弹）
/// 
/// 使用说明：
///   1. 创建一个 Capsule 或 Sphere GameObject 作为子弹预制体。
///   2. 添加 Collider 组件，勾选 Is Trigger = true。
///   3. 添加 Rigidbody 组件，勾选 Is Kinematic = true（防止受重力影响）。
///   4. 将此脚本挂载到预制体上。
///   5. 为预制体设置 Tag = "BossBullet"。
///   6. 将预制体拖入 BossController 的 Bullet1 Prefab 字段。
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class Bullet1 : MonoBehaviour
{
    [Tooltip("子弹自动销毁时间（秒），防止子弹飞出去永不销毁")]
    public float lifetime = 10f;

    // ─────────────────────────────────────────────
    // 私有运动参数（由 Initialize 注入）
    // ─────────────────────────────────────────────
    private Vector3 moveDirection;
    private float   moveSpeed;
    private float   damage;
    private bool    initialized = false;

    void Awake()
    {
        // 确保 Rigidbody 不受物理引擎影响，由我们手动移动
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic  = true;
            rb.useGravity   = false;
        }
    }

    /// <summary>
    /// 由 BossController 在实例化后立即调用，传入飞行参数。
    /// </summary>
    /// <param name="direction">飞行方向（会自动归一化）</param>
    /// <param name="speed">飞行速度（Units/s）</param>
    /// <param name="dmg">命中玩家造成的伤害值</param>
    public void Initialize(Vector3 direction, float speed, float dmg)
    {
        moveDirection = direction.normalized;
        moveSpeed     = speed;
        damage        = dmg;
        initialized   = true;

        // 自动销毁，防止内存泄漏
        Destroy(gameObject, lifetime);
    }

    // ─────────────────────────────────────────────
    // 直线移动
    // ─────────────────────────────────────────────

    void Update()
    {
        if (!initialized) return;

        // 每帧沿固定方向匀速平移
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    // ─────────────────────────────────────────────
    // 碰撞检测
    // ─────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        // ── 忽略 ──────────────────────────────────
        // 不与 Boss 本体、其他 Boss 子弹、水晶发生交互
        // （水晶在 Boss 前方，子弹需要穿过水晶打到玩家）
        if (other.CompareTag("Boss")
         || other.CompareTag("BossBullet")
         || other.CompareTag("Crystal"))
            return;

        // ── 命中玩家 ─────────────────────────────
        if (other.CompareTag("Player"))
        {
            PlayerHealth ph = other.GetComponent<PlayerHealth>();
            if (ph != null)
                ph.TakeDamage((int)damage);
            else
                Debug.LogWarning("[Bullet1] 玩家对象上未找到 PlayerHealth 组件！");
        }

        // 任何非豁免碰撞（包括命中玩家）都销毁子弹
        Destroy(gameObject);
    }
}
