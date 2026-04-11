using UnityEngine;

/// <summary>
/// 玩家子弹移动器
/// 
/// 玩家发射的子弹直线飞行，命中 Boss 或水晶后触发伤害。
/// 此脚本会由 PlayerController.Shoot() 自动添加，无需手动挂载。
/// 当然也可以手动挂在子弹预制体上以提高性能（避免 AddComponent 开销）。
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlayerBulletMover : MonoBehaviour
{
    [Tooltip("子弹最大飞行时间（秒）")]
    public float lifetime = 8f;

    private Vector3 direction;
    private float   speed;
    private bool    initialized = false;

    void Awake()
    {
        // 确保 Rigidbody 不干扰运动
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }
    }

    /// <summary>由 PlayerController 调用，注入飞行方向和速度</summary>
    public void Initialize(Vector3 dir, float spd)
    {
        direction   = dir.normalized;
        speed       = spd;
        initialized = true;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (!initialized) return;
        transform.position += direction * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        // 忽略玩家自身和其他玩家子弹
        if (other.CompareTag("Player") || other.CompareTag("PlayerBullet") || other.CompareTag("PlayerAttack"))
            return;

        // 命中普通敌人
        if (other.CompareTag("Enemy"))
        {
            Enemy enemy = other.GetComponent<Enemy>();
            if (enemy != null)
            {
                DamageSource ds = GetComponent<DamageSource>();
                float dmg = (ds != null) ? ds.damage : 10f;
                enemy.TakeDamage(dmg);
            }
            Destroy(gameObject);
            return;
        }

        // 命中 Boss
        if (other.CompareTag("Boss"))
        {
            BossController boss = other.GetComponent<BossController>();
            if (boss != null)
            {
                DamageSource ds = GetComponent<DamageSource>();
                float dmg = (ds != null) ? ds.damage : 10f;
                boss.TakeDamage(dmg);
            }
            Destroy(gameObject);
            return;
        }

        // 命中水晶
        if (other.CompareTag("Crystal"))
        {
            Crystal crystal = other.GetComponent<Crystal>();
            if (crystal != null)
            {
                DamageSource ds = GetComponent<DamageSource>();
                float dmg = (ds != null) ? ds.damage : 10f;
                crystal.TakeDamage(dmg);
            }
            Destroy(gameObject);
            return;
        }

        // 命中其他障碍物（如场景墙壁）也销毁
        Destroy(gameObject);
    }
}
