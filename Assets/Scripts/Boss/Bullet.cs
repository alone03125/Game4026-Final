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
    [Tooltip("子弹自动销毁时间（秒）")]
    public float lifetime = 10f;

    [Tooltip("子弹飞行速度")]
    public float speed = 15f;

    [Tooltip("命中玩家造成的伤害值")]
    public float defaultDamage = 1f;

    private Vector3 moveDirection;

    void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }
    }

    public void SetDirection(Vector3 direction)
    {
        moveDirection = direction.normalized;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += moveDirection * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Boss")
         || other.CompareTag("BossBullet")
         || other.CompareTag("Crystal"))
            return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            PlayerHealth ph = other.GetComponent<PlayerHealth>();
            if (ph != null)
                ph.TakeDamage(defaultDamage);
            else
                Debug.LogWarning("[Bullet1] 玩家对象上未找到 PlayerHealth 组件！");
        }

        Destroy(gameObject);
    }
}
