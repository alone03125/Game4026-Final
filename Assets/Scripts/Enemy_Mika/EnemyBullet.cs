using UnityEngine;

/// <summary>
/// 敌方子弹
/// - 碰到玩家子弹（Tag = "PlayerBullet"）：双方同时销毁
/// - 碰到玩家（Tag = "Player"）：调用 TakeDamage，自身销毁
/// - 忽略 Enemy 自身
/// - 碰到其他物体：自身销毁
/// </summary>
public class EnemyBullet : MonoBehaviour
{
    public float speed    = 8f;
    public float damage   = 1f;
    public float lifeTime = 3f;

    private bool _hasHit = false;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;

        // 与玩家子弹对消
        if (other.CompareTag("PlayerBullet"))
        {
            _hasHit = true;
            Destroy(other.gameObject);
            Destroy(gameObject);
            return;
        }

        // 命中任意玩家子碰撞体：只要能解析到 PlayerHealth 就判定为击中玩家
        PlayerHealth playerHealth = ResolvePlayerHealth(other);
        if (playerHealth != null)
        {
            _hasHit = true;
            playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // 忽略 Enemy和 Boss 自身
        if (other.CompareTag("Enemy") || other.CompareTag("Boss")) return;

        // 碰到其他物体（如墙壁）销毁
        _hasHit = true;
        Destroy(gameObject);
    }

    private PlayerHealth ResolvePlayerHealth(Collider other)
    {
        PlayerHealth ph = other.GetComponent<PlayerHealth>();
        if (ph != null) return ph;

        ph = other.GetComponentInChildren<PlayerHealth>(true);
        if (ph != null) return ph;

        ph = other.GetComponentInParent<PlayerHealth>();
        if (ph != null) return ph;

        Transform root = other.transform.root;
        if (root != null)
        {
            ph = root.GetComponent<PlayerHealth>();
            if (ph != null) return ph;

            ph = root.GetComponentInChildren<PlayerHealth>(true);
            if (ph != null) return ph;
        }

        if (other.attachedRigidbody != null)
        {
            ph = other.attachedRigidbody.GetComponent<PlayerHealth>();
            if (ph != null) return ph;

            ph = other.attachedRigidbody.GetComponentInParent<PlayerHealth>();
            if (ph != null) return ph;
        }

        return null;
    }
}
