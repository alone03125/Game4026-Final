using UnityEngine;

public class Bullet : MonoBehaviour
{
    public enum BulletType { Player, Enemy }  // 区分子弹来源
    public BulletType type;
    public float speed = 10f;
    public int damage = 10;
    public float lifeTime = 3f;

    void Start()
    {
        Destroy(gameObject, lifeTime); // 超过时间自动销毁
    }

    void Update()
    {
        // 沿自身前方移动（如果是2D/3D根据需要调整方向）
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("子弹碰到: " + other.name);
        // 玩家子弹击中怪物
        if (type == BulletType.Player && other.CompareTag("Enemy"))
        {
            Enemy enemy = other.GetComponent<Enemy>();
            if (enemy != null)
                enemy.Die();
            Destroy(gameObject);
        }
        // 怪物子弹击中玩家
        else if (type == BulletType.Enemy && other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.TakeDamage(damage);
            Destroy(gameObject);
        }
        // 碰到其他物体（如墙壁）也可销毁
        else if (!other.CompareTag("Enemy") && !other.CompareTag("Player"))
        {
            Destroy(gameObject);
        }
    }
}