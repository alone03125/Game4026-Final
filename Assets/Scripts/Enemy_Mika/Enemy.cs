using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Enemy : MonoBehaviour
{
    // ── 全局活跃敌人列表，供 AttackWarningUI 轮询 ──
    public static readonly List<Enemy> ActiveEnemies = new List<Enemy>();

    /// <summary>当前是否处于蓄力预警阶段（供 UI 系统读取）</summary>
    public bool IsCharging => _warningActive || _aimLineActive;
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

    [Header("预警 —— 球状光晕特效")]
    public float warningDuration = 0.5f;          // 开枪前多少秒显示预警球
    public GameObject warningEffect;              // 球状特效子物体（在 Inspector 中拖入）
    public float warningPulseSpeed = 6f;          // 脉冲缩放速度
    public float warningMinScale   = 0.8f;        // 脉冲最小倍率
    public float warningMaxScale   = 1.2f;        // 脉冲最大倍率

    [Header("预警 —— 激光瞄准线")]
    public float aimLineDuration = 0.6f;          // 显示瞄准线的提前时间（秒）
    public Color aimLineStartColor  = new Color(1f, 0.2f, 0.2f, 1f);   // 起点颜色
    public Color aimLineEndColor    = new Color(1f, 0.2f, 0.2f, 0f);   // 终点颜色（渐隐）
    public float aimLineWidth       = 0.05f;      // 线宽
    public Material aimLineMaterial;              // 瞄准线材质（拖入 Sprites/Default 材质）
    public ParticleSystem aimLineParticle;        // 挂在敌人子物体上的粒子特效（沿线散发）

    [Header("预警 —— 蓄力音效")]
    public AudioClip chargeSound;                 // 蓄力音效（开始预警时播放）
    public AudioClip shootSound;                  // 开枪音效（可不赋値）
    [Range(0f, 1f)] public float chargeSoundVolume = 0.8f;

    private Transform player;
    private Vector3 velocity;                  // 当前速度向量
    private Vector3 currentDesiredDir;         // 当前期望方向（用于混合随机）
    private float directionTimer;
    private float shootTimer;

    private EnemySpawner spawner;              // 生成器引用

    private bool _warningActive  = false;
    private Vector3 _warningBaseScale;

    private LineRenderer _aimLine;
    private bool _aimLineActive = false;

    private bool _isShooting = false;   // 协程射击锁，防止重复启动
    private float _chargeStartTime;      // 蓄力开始的时间戳
    private float _chargeDuration;       // 当前蓄力持续时长
    private bool  _initialized = false;  // 是否已完成 Start() 初始化

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
        else
            Debug.LogError("没有找到Player标签的物体！");

        ResetState(); // 初始化状态

        // 缓存球体初始屚度并默认隐藏
        if (warningEffect != null)
        {
            _warningBaseScale = warningEffect.transform.localScale;
            warningEffect.SetActive(false);
        }

        // 初始化瞄准线 LineRenderer
        _aimLine = gameObject.AddComponent<LineRenderer>();
        _aimLine.positionCount  = 2;
        _aimLine.startWidth     = aimLineWidth;
        _aimLine.endWidth       = aimLineWidth * 0.2f;
        // Inspector 有赋值则用赋值材质，否则用内置 Sprites/Default（支持顶点色）
        _aimLine.material       = aimLineMaterial != null
            ? aimLineMaterial
            : new Material(Shader.Find("Sprites/Default"));
        _aimLine.startColor     = aimLineStartColor;
        _aimLine.endColor       = aimLineEndColor;
        _aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _aimLine.receiveShadows = false;
        _aimLine.enabled        = false;

        // 粒子特效默认停止
        if (aimLineParticle != null)
            aimLineParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 启动射击协程
        StartCoroutine(ShootLoop());
        _initialized = true;
        ActiveEnemies.Add(this);
    }

    void OnEnable()
    {
        // 对象池复用时重新加入列表并重启协程
        if (_initialized)
        {
            ActiveEnemies.Add(this);
            StartCoroutine(ShootLoop());
        }
    }

    void OnDisable()
    {
        // 禁用时停止所有协程，防止域重载时加载对象导致未定义行为
        ActiveEnemies.Remove(this);
        StopAllCoroutines();
        _warningActive = false;
        _aimLineActive = false;
        // 立即隐藏所有预警特效
        if (warningEffect != null) warningEffect.SetActive(false);
        if (_aimLine != null)      _aimLine.enabled = false;
        if (aimLineParticle != null)
            aimLineParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

        // 瞄准线每帧更新端点（激活时跟随移动）
        if (_aimLineActive)
            UpdateAimLine();

        // 球状光晕脉冲每帧更新
        if (_warningActive && warningEffect != null)
            UpdateWarningEffect();
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
            baseDir = (awayDir * 1.5f + randomOffset * 0.3f).normalized;
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
        // 射击逻辑已移至协程 ShootLoop()，此方法保留为空以兼容旧调用
    }

    // ── 核心射击协程 ──────────────────────────────────────────
    // 流程：等待间隔 → 同时触发所有预警特效+音效 → 等待蓄力时间 → 发射子弹 → 清理特效 → 循环
    System.Collections.IEnumerator ShootLoop()
    {
        // 随机错开敌人之间的齐射节奏
        yield return new WaitForSeconds(Random.Range(0f, shootInterval));

        while (true)
        {
            // ── 1. 等待下次攻击间隔（减去蓄力时间）──
            float waitTime = shootInterval - Mathf.Max(warningDuration, aimLineDuration);
            if (waitTime > 0f) yield return new WaitForSeconds(waitTime);

            if (player == null) { yield return null; continue; }

            // ── 2. 蓄力预警阶段：同时激活球、线、音效 ──
            float chargeDuration = Mathf.Max(warningDuration, aimLineDuration);
            _chargeStartTime = Time.time;
            _chargeDuration  = chargeDuration;

            // 球状光晕
            if (warningEffect != null)
            {
                _warningActive = true;
                warningEffect.SetActive(true);
            }
            // 激光瞄准线
            if (_aimLine != null)
            {
                _aimLineActive = true;
                _aimLine.enabled = true;
                if (aimLineParticle != null) aimLineParticle.Play();
            }
            // 蓄力音效
            if (chargeSound != null)
                AudioSource.PlayClipAtPoint(chargeSound, transform.position, chargeSoundVolume);

            // ── 3. 等待蓄力时间结束 ──
            yield return new WaitForSeconds(chargeDuration);

            // ── 4. 发射子弹 ──
            if (shootSound != null)
                AudioSource.PlayClipAtPoint(shootSound, transform.position, chargeSoundVolume);

            if (enemyBulletPrefab != null && player != null)
            {
                Vector3 direction = (player.position - transform.position).normalized;
                GameObject bullet = Instantiate(enemyBulletPrefab, transform.position, Quaternion.LookRotation(direction));
                bullet.tag = "EnemyBullet";
                EnemyBullet bulletScript = bullet.GetComponent<EnemyBullet>();
                if (bulletScript != null)
                    bulletScript.speed = bulletSpeed;
            }

            // ── 5. 清理所有预警特效 ──
            _warningActive = false;
            if (warningEffect != null)
            {
                warningEffect.transform.localScale = _warningBaseScale;
                warningEffect.SetActive(false);
            }
            HideAimLine();
        }
    }

    // 球状光晕脉冲动画：在预警期间让球体呼吸式缩放
    void UpdateWarningEffect()
    {
        float pulse = (Mathf.Sin(Time.time * warningPulseSpeed) + 1f) * 0.5f; // 0~1
        float scale = Mathf.Lerp(warningMinScale, warningMaxScale, pulse);
        warningEffect.transform.localScale = _warningBaseScale * scale;
    }

    // 每帧更新激光线两端位置，并让线宽随蓄力进度增粗（越临近开枪越粗）
    void UpdateAimLine()
    {
        if (player == null) return;
        _aimLine.SetPosition(0, transform.position);
        _aimLine.SetPosition(1, player.position);

        // t: 0=刚开始蓄力  1=即将开枪
        float t = _chargeDuration > 0f
            ? Mathf.Clamp01((Time.time - _chargeStartTime) / _chargeDuration)
            : 1f;
        float currentWidth = Mathf.Lerp(aimLineWidth * 0.3f, aimLineWidth, t);
        _aimLine.startWidth = currentWidth;
        _aimLine.endWidth   = currentWidth * 0.2f;

        // 颜色 Alpha 也随 t 增强
        Color start = aimLineStartColor;
        start.a = Mathf.Lerp(0.2f, aimLineStartColor.a, t);
        _aimLine.startColor = start;
    }

    void HideAimLine()
    {
        _aimLineActive   = false;
        _aimLine.enabled = false;
        if (aimLineParticle != null)
            aimLineParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void ClampPosition()
    {
        if (spawner != null)
        {
            Bounds bounds = spawner.GetCurrentBounds();
            Vector3 pos = transform.position;

            // 给边界留一点安全内缩，避免敌人贴边抖动
            const float edgePadding = 0.1f;
            float minX = bounds.min.x + edgePadding;
            float maxX = bounds.max.x - edgePadding;
            float minY = bounds.min.y + edgePadding;
            float maxY = bounds.max.y - edgePadding;
            float minZ = bounds.min.z + edgePadding;
            float maxZ = bounds.max.z - edgePadding;

            bool hitMinX = pos.x <= minX;
            bool hitMaxX = pos.x >= maxX;
            bool hitMinY = pos.y <= minY;
            bool hitMaxY = pos.y >= maxY;
            bool hitMinZ = pos.z <= minZ;
            bool hitMaxZ = pos.z >= maxZ;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

            // 命中边界时，清除继续向外的速度分量，防止下一帧继续挤压边界
            if (hitMinX && velocity.x < 0f) velocity.x = 0f;
            if (hitMaxX && velocity.x > 0f) velocity.x = 0f;
            if (hitMinY && velocity.y < 0f) velocity.y = 0f;
            if (hitMaxY && velocity.y > 0f) velocity.y = 0f;
            if (hitMinZ && velocity.z < 0f) velocity.z = 0f;
            if (hitMaxZ && velocity.z > 0f) velocity.z = 0f;

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

        if (d <= 0f) return Vector3.zero;

        float dxMin = pos.x - bounds.min.x;
        float dxMax = bounds.max.x - pos.x;
        float dyMin = pos.y - bounds.min.y;
        float dyMax = bounds.max.y - pos.y;
        float dzMin = pos.z - bounds.min.z;
        float dzMax = bounds.max.z - pos.z;

        // 越靠近边界，推力按二次曲线增强，避免“临边时推不回来”
        if (dxMin < d) repulsion.x += Mathf.Pow(1f - dxMin / d, 2f);
        if (dxMax < d) repulsion.x -= Mathf.Pow(1f - dxMax / d, 2f);
        if (dyMin < d) repulsion.y += Mathf.Pow(1f - dyMin / d, 2f);
        if (dyMax < d) repulsion.y -= Mathf.Pow(1f - dyMax / d, 2f);
        if (dzMin < d) repulsion.z += Mathf.Pow(1f - dzMin / d, 2f);
        if (dzMax < d) repulsion.z -= Mathf.Pow(1f - dzMax / d, 2f);

        return repulsion;
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