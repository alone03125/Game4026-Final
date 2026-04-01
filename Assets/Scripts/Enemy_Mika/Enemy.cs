using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("移动参数")]
    public float baseSpeed = 2f;               // 基础速度
    public float gravitySensitivity = 1f;      // 重力对速度的影响系数
    public float randomMoveWeight = 0.5f;      // 随机移动的权重（0=纯追踪，1=完全随机）
    public float directionChangeInterval = 0.5f; // 改变随机方向的时间间隔

    [Header("平滑移动")]
    public float maxSpeed = 5f;                // 最大速度
    public float acceleration = 8f;            // 加速度（转向速度）
    public float steeringForce = 2f;           // 转向力强度（平滑转向）
    public float verticalMovementScale = 2f;   // 随机方向中Y轴的放大系数（>1使敌人更常飞起来）

    [Header("行为限制")]
    public float minDistanceToPlayer = 10f;     // 距离玩家小于此值时强制远离
    public float boundaryRepulsionDistance = 5f; // 距边界多近时开始被推离（平滑离开）

    [Header("射击参数")]
    public GameObject enemyBulletPrefab;
    public float shootInterval = 1.5f;
    public float bulletSpeed = 8f;

    private Transform player;
    private Vector3 velocity;                  // 当前速度向量
    private Vector3 currentDesiredDir;         // 当前期望方向（用于混合随机）
    private float directionTimer;
    private float shootTimer;

    private EnemySpawner spawner;              // 生成器引用

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
        else
            Debug.LogError("没有找到Player标签的物体！");

        ResetState(); // 初始化状态
    }

    void Update()
    {
        if (player == null) return;

        // 平滑移动
        MoveSmooth();

        // 射击
        Shoot();

        // 边界限制（包含Y轴）
        ClampPosition();

        // 面向玩家
        FacePlayer();
    }

    void MoveSmooth()
    {
        // 1. 获取期望的方向（基于距离判断）
        Vector3 desiredDirection = GetDesiredDirection();

        // 2. 计算转向力：将当前速度逐渐转向期望方向
        Vector3 steering = (desiredDirection * maxSpeed - velocity) * steeringForce * Time.deltaTime;
        // 限制最大转向力，避免突变
        steering = Vector3.ClampMagnitude(steering, acceleration * Time.deltaTime);

        // 3. 应用转向力更新速度
        velocity += steering;
        // 限制速度不超过最大速度
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

        // 4. 重力影响速度大小
        float currentGravity = GameManager.Instance != null ? GameManager.Instance.GetCurrentGravity() : 1f;
        float speedScale = 1 + gravitySensitivity * currentGravity;
        Vector3 finalVelocity = velocity * speedScale;

        // 5. 移动
        transform.position += finalVelocity * Time.deltaTime;
    }

    Vector3 GetDesiredDirection()
    {
        // 定时更新基础混合方向
        directionTimer += Time.deltaTime;
        if (directionTimer >= directionChangeInterval)
        {
            UpdateDesiredDirection();
            directionTimer = 0;
        }

        // 检查与玩家的距离
        float distance = Vector3.Distance(transform.position, player.position);
        Vector3 baseDir;
        if (distance < minDistanceToPlayer)
        {
            // 距离太近：强制远离玩家，并加入随机扰动
            Vector3 awayDir = (transform.position - player.position).normalized;
            Vector3 randomOffset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f)).normalized;
            baseDir = (awayDir + randomOffset * 0.3f).normalized;
        }
        else
        {
            // 正常情况：使用混合方向（追踪+随机）
            baseDir = currentDesiredDir;
        }

        // 边界排斥：靠近边界时平滑地混入一个朝向中心的推力
        Vector3 repulsion = GetBoundaryRepulsion();
        if (repulsion != Vector3.zero)
            baseDir = (baseDir + repulsion).normalized;

        return baseDir;
    }

    void UpdateDesiredDirection()
    {
        if (player == null) return;

        // 指向玩家的方向
        Vector3 toPlayer = (player.position - transform.position).normalized;
        // 随机方向（全3D）
        Vector3 randomDir = Random.onUnitSphere;
        // 放大Y分量使敌人更频繁地上下飞行，verticalMovementScale > 1 越大越活跃
        randomDir.y *= verticalMovementScale;
        // randomDir.y += 0.6f; // 稍微增加一点向上的倾向，避免完全水平飞行
        randomDir.Normalize();

        Vector3 mixed = (toPlayer * (1 - randomMoveWeight) + randomDir * randomMoveWeight).normalized;
        currentDesiredDir = mixed;
    }

    void Shoot()
    {
        shootTimer += Time.deltaTime;
        if (shootTimer >= shootInterval)
        {
            shootTimer = 0;
            if (enemyBulletPrefab != null && player != null)
            {
                Vector3 direction = (player.position - transform.position).normalized;
                GameObject bullet = Instantiate(enemyBulletPrefab, transform.position, Quaternion.LookRotation(direction));
                bullet.tag = "EnemyBullet";
                EnemyBullet bulletScript = bullet.GetComponent<EnemyBullet>();
                if (bulletScript != null)
                    bulletScript.speed = bulletSpeed;
            }
        }
    }

    void ClampPosition()
    {
        if (spawner != null)
        {
            Bounds bounds = spawner.GetCurrentBounds();
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, bounds.min.x, bounds.max.x);
            pos.y = Mathf.Clamp(pos.y, bounds.min.y, bounds.max.y);
            pos.z = Mathf.Clamp(pos.z, bounds.min.z, bounds.max.z);
            transform.position = pos;
        }
    }

    // 根据与边界的距离计算排斥力方向（越近排斥越强，在 boundaryRepulsionDistance 外为零）
    Vector3 GetBoundaryRepulsion()
    {
        if (spawner == null) return Vector3.zero;
        Bounds bounds = spawner.GetCurrentBounds();
        Vector3 pos = transform.position;
        Vector3 repulsion = Vector3.zero;
        float d = boundaryRepulsionDistance;

        float dxMin = pos.x - bounds.min.x;
        float dxMax = bounds.max.x - pos.x;
        float dyMin = pos.y - bounds.min.y;
        float dyMax = bounds.max.y - pos.y;
        float dzMin = pos.z - bounds.min.z;
        float dzMax = bounds.max.z - pos.z;

        if (dxMin < d) repulsion.x += (1f - dxMin / d);
        if (dxMax < d) repulsion.x -= (1f - dxMax / d);
        if (dyMin < d) repulsion.y += (1f - dyMin / d);
        if (dyMax < d) repulsion.y -= (1f - dyMax / d);
        if (dzMin < d) repulsion.z += (1f - dzMin / d);
        if (dzMax < d) repulsion.z -= (1f - dzMax / d);

        return repulsion.normalized * repulsion.magnitude;
    }

    void FacePlayer()
    {
        if (player == null) return;

        // 方向向量
        Vector3 direction = player.position - transform.position;
        if (direction == Vector3.zero) return;

        // 计算目标旋转
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        // 如果希望只绕 Y 轴旋转（保持水平），取消下面注释
        // targetRotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);

        // 平滑旋转（每秒旋转速度）
        float rotationSpeed = 360f; // 度/秒，可根据需要调整
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    public void Die()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnEnemyKilled();
        if (spawner != null)
            spawner.OnEnemyDied(gameObject);
        else
            Destroy(gameObject);
    }

    public void SetSpawner(EnemySpawner sp)
    {
        spawner = sp;
    }

    public void ResetState()
    {
        // 重置速度
        velocity = Vector3.zero;
        directionTimer = 0f;
        shootTimer = Random.Range(0f, shootInterval);
        UpdateDesiredDirection(); // 重置期望方向
    }
}