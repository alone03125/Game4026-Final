using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 控制器
/// 管理 Boss 的血量、三个战斗阶段、射击逻辑和关卡时间限制。
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
    [Tooltip("每秒发射子弹数（子弹生成频率）")]
    public float phase1FireRate = 2f;
    [Tooltip("Attack01 动画前摇延迟（秒）：从动画开始到子弹实际发射的等待时间")]
    public float phase1FireDelay = 0.5f;
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
    [Tooltip("狂暴状态每秒发射子弹数（子弹生成频率）")]
    public float phase3FireRate = 5f;
    [Tooltip("Attack02 动画前摇延迟（秒）：第二/三阶段从动画开始到子弹实际发射的等待时间")]
    public float phase2FireDelay = 0.3f;
    [Tooltip("狂暴状态子弹伤害值")]
    public float phase3Bullet1Damage = 2f;
    [Tooltip("狂暴时的粒子效果（需预先设置好红色粒子系统）")]
    public ParticleSystem berserkParticleEffect;

    [Header("=== 关卡时间限制（可调整）===")]
    [Tooltip("超过此时间（秒）后 Boss 伤害翻倍，默认 300 秒 = 5 分钟")]
    public float doubleDamageTimeLimit = 300f;
    [Tooltip("超过此时间（秒）后 Boss 强制进入狂暴，默认 480 秒 = 8 分钟")]
    public float forceBerserkTimeLimit = 480f;

    // ★ 新增：阶段 / 死亡特效
    [Header("=== 阶段 / 死亡特效 ===")]
    [Tooltip("进入第二阶段时播放的特效 Prefab")]
    public GameObject phase2Effect;
    [Tooltip("进入第三阶段（狂暴）时播放的特效 Prefab")]
    public GameObject phase3Effect;
    [Tooltip("Boss 死亡时播放的特效 Prefab")]
    public GameObject deathEffect;
    [Tooltip("以上三种特效自动销毁的延迟时间（秒），设为 0 则不自动销毁）")]
    public float phaseFxDuration = 3f;

    // ─────────────────────────────────────────────
    // 私有状态变量
    // ─────────────────────────────────────────────

    private float currentHealth;

    private int  currentPhase        = 1;
    private bool phase2Triggered     = false;
    private bool phase3Triggered     = false;

    private bool isImmune            = false;
    private bool shieldActive        = false;

    private bool isBerserk           = false;

    private float bossStartTime;
    private bool  doubleDamageTriggered = false;
    private bool  forceBerserkTriggered = false;

    private float fireTimer = 0f;

    private Color bossOriginalColor;
    private Color bossColorTarget;

    private Transform     playerTransform;
    private List<Crystal> activeCrystals = new List<Crystal>();
    private Renderer      bossRenderer;

    // ─────────────────────────────────────────────
    // 动画
    // ─────────────────────────────────────────────

    private Animator animator;
    private bool  isDead = false;

    private const string ANIM_IDLE            = "Idle";
    private const string ANIM_ATTACK01        = "Attack01";
    private const string ANIM_ATTACK02        = "Attack02";
    private const string ANIM_BEEN_ATTACKED   = "BeenAttacked";
    private const string ANIM_BEEN_ATTACKED01 = "BeenAttacked01";
    private const string ANIM_BEEN_ATTACKED02 = "BeenAttacked02";
    private const string ANIM_TURN90          = "Turn90";
    private const string ANIM_DEATH           = "Death";

    private const float ANIM_FADE_TIME = 0.15f;

    private void PlayAnim(string stateName, float fadeTime = ANIM_FADE_TIME)
    {
        if (animator == null || isDead) return;
        animator.CrossFadeInFixedTime(stateName, fadeTime);
    }

    private IEnumerator PlayAnimThenIdle(string stateName, float fadeTime = ANIM_FADE_TIME)
    {
        PlayAnim(stateName, fadeTime);
        yield return null;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        yield return new WaitForSeconds(info.length);
        if (!isDead)
            PlayAnim(ANIM_IDLE);
    }

    // ─────────────────────────────────────────────
    // Unity 生命周期
    // ─────────────────────────────────────────────

    void Start()
    {
        currentHealth = maxHealth;
        bossStartTime = Time.time;

        animator = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogWarning("[Boss] 未找到 Animator 组件！动画将无法播放。");

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;
        else
            Debug.LogWarning("[Boss] 未找到 Tag=Player 的玩家对象！");

        bossRenderer = GetComponentInChildren<Renderer>();

        if (bossRenderer != null)
        {
            bossOriginalColor = bossRenderer.material.color;
            bossColorTarget   = bossOriginalColor;
        }

        if (berserkParticleEffect != null) berserkParticleEffect.Stop();
        if (bossGlowLight          != null) bossGlowLight.enabled = false;

        // Play SFX
        AudioManager.Instance?.PlaySfxAttachedOnce(SfxId.BossSpawn, transform, 1f);
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

        if (!doubleDamageTriggered && elapsed >= doubleDamageTimeLimit)
        {
            doubleDamageTriggered = true;
            phase1Bullet1Damage   *= 2f;
            phase3Bullet1Damage   *= 2f;
            Debug.Log($"[Boss] ⚠ 超时 {doubleDamageTimeLimit}s！Boss 伤害翻倍！");
        }

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
        if (isDead) return;
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
        if (isImmune)  return;

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

        string attackAnim = (currentPhase >= 2) ? ANIM_ATTACK02 : ANIM_ATTACK01;
        StartCoroutine(ShootAfterWindUp(attackAnim));
    }

    IEnumerator ShootAfterWindUp(string attackAnim)
    {
        PlayAnim(attackAnim);
        yield return null;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        float windUpDelay = (currentPhase >= 2) ? phase2FireDelay : phase1FireDelay;
        yield return new WaitForSeconds(windUpDelay);

        if (isDead || playerTransform == null) yield break;

        string originName = (currentPhase >= 2) ? "Bossbullet2" : "Bossbullet1";
        Transform bulletOrigin = transform.Find(originName);
        Vector3 spawnPos = (bulletOrigin != null) ? bulletOrigin.position : transform.position;
        Vector3 dir      = (playerTransform.position - spawnPos).normalized;

        SpawnBullet(spawnPos, dir);

        float remainingDelay = Mathf.Max(0f, info.length - windUpDelay);
        yield return new WaitForSeconds(remainingDelay);
        if (!isDead)
            PlayAnim(ANIM_IDLE);
    }

    void SpawnBullet(Vector3 position, Vector3 direction)
    {
        GameObject bullet1Obj = Instantiate(bullet1Prefab, position, Quaternion.LookRotation(direction));
        bullet1Obj.tag = "BossBullet";

        // Play SFX
        AudioManager.Instance?.PlaySfxAtPoint(SfxId.BossShoot, position, 0.95f);
        
        Collider bulletCol = bullet1Obj.GetComponent<Collider>();
        if (bulletCol != null)
        {
            foreach (Collider bossCol in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(bulletCol, bossCol);
        }

        Bullet1 bullet1 = bullet1Obj.GetComponent<Bullet1>();
        if (bullet1 != null)
            bullet1.SetDirection(direction);
    }

    // ─────────────────────────────────────────────
    // 伤害与血量
    // ─────────────────────────────────────────────

    public void TakeDamage(float damage)
    {
        if (isImmune) { Debug.Log("[Boss] 免疫中，无效伤害。"); return; }

        float actualDamage = damage;

        if (shieldActive)
        {
            actualDamage *= shieldDamageReduction;
            Debug.Log($"[Boss] 护盾减伤！原伤害 {damage} → 实际伤害 {actualDamage}");
        }

        currentHealth -= actualDamage;
        currentHealth  = Mathf.Clamp(currentHealth, 0f, maxHealth);

        Debug.Log($"[Boss] 受到伤害 {actualDamage}，当前血量：{currentHealth}/{maxHealth}");

        if (animator != null)
        {
            string[] hitAnims = { ANIM_BEEN_ATTACKED, ANIM_BEEN_ATTACKED01, ANIM_BEEN_ATTACKED02 };
            StartCoroutine(PlayAnimThenIdle(hitAnims[UnityEngine.Random.Range(0, hitAnims.Length)]));
        }

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
        // Play SFX
        AudioManager.Instance?.PlaySfxAttachedOnce(SfxId.BossPhaseChange, transform, 1f);

        currentPhase = 2;
        Debug.Log("[Boss] ─── 进入第二阶段！开始 5 秒无敌期 ───");

        // ★ 在 Boss 当前位置播放第二阶段特效
        PlayEffect(phase2Effect, transform.position);

        yield return StartCoroutine(PlayAnimThenIdle(ANIM_TURN90));

        isImmune = true;
        yield return new WaitForSeconds(immunityDuration);
        isImmune = false;

        Debug.Log("[Boss] 无敌期结束，激活护盾与水晶！");
        ActivateShield();
    }

    void ActivateShield()
    {
        shieldActive = true;

        if (bossGlowLight != null)
        {
            bossGlowLight.enabled = true;
            bossGlowLight.color   = shieldColor;
        }

        bossColorTarget = shieldColor;

        if (bossRenderer != null)
        {
            bossRenderer.material.EnableKeyword("_EMISSION");
            bossRenderer.material.SetColor("_EmissionColor", shieldColor * 2f);
        }

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
            for (int i = 0; i < CRYSTAL_COUNT; i++)
            {
                if (crystalSpawnPoints[i] != null)
                    CreateCrystal(crystalSpawnPoints[i].position, crystalSpawnPoints[i].rotation);
            }
        }
        else
        {
            Debug.LogWarning("[Boss] 未设置足够的 crystalSpawnPoints，将自动生成在 Boss 周围。");

            Collider bossCollider = GetComponent<Collider>();
            float bossRadius = 1f;
            if (bossCollider != null)
                bossRadius = bossCollider.bounds.extents.magnitude;

            float spawnDistance = bossRadius + 5f;

            for (int i = 0; i < CRYSTAL_COUNT; i++)
            {
                float   angle    = (i - (CRYSTAL_COUNT - 1) * 0.5f) * 90f;
                Vector3 offset   = Quaternion.Euler(0f, angle, 0f) * transform.forward * spawnDistance;
                Vector3 spawnPos = transform.position + offset + Vector3.up * 0.5f;
                CreateCrystal(spawnPos, Quaternion.identity);
            }
        }
    }

    void CreateCrystal(Vector3 position, Quaternion rotation)
    {
        GameObject obj    = Instantiate(crystalPrefab, position, rotation * Quaternion.Euler(90f, 0f, 0f));
        Crystal    crystal = obj.GetComponent<Crystal>();
        if (crystal != null)
        {
            crystal.RegisterBoss(this);
            activeCrystals.Add(crystal);
        }
    }

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

        bossColorTarget = bossOriginalColor;

        Debug.Log("[Boss] 所有水晶被摧毁，护盾解除！");
    }

    // ─────────────────────────────────────────────
    // 护盾颜色淡入 / 淡出
    // ─────────────────────────────────────────────

    void HandleShieldFade()
    {
        if (bossRenderer == null) return;

        Color current = bossRenderer.material.color;
        if (current != bossColorTarget)
            bossRenderer.material.color = Color.Lerp(current, bossColorTarget, shieldFadeSpeed * Time.deltaTime);
    }

    // ─────────────────────────────────────────────
    // 第三阶段：狂暴
    // ─────────────────────────────────────────────

    void EnterPhase3()
    {
        //play SFX
        AudioManager.Instance?.PlaySfxAttachedOnce(SfxId.BossPhaseChange, transform, 1f);

        currentPhase = 3;
        Debug.Log("[Boss] ─── 进入第三阶段！─── ");

        // ★ 在 Boss 当前位置播放第三阶段特效
        PlayEffect(phase3Effect, transform.position);

        EnterBerserkMode();
    }

    void EnterBerserkMode()
    {
        if (isBerserk) return;
        isBerserk = true;

        StartCoroutine(PlayAnimThenIdle(ANIM_TURN90));

        if (berserkParticleEffect != null)
        {
            var main = berserkParticleEffect.main;
            main.startColor = Color.red;
            berserkParticleEffect.Play();
        }

        Debug.Log("[Boss] 进入狂暴！攻击频率 → 5发/秒，伤害 → 2");
    }

    // ─────────────────────────────────────────────
    // 死亡
    // ─────────────────────────────────────────────

    void OnDisable()
    {
        StopAllCoroutines();
    }

    void Die()
    {
        Debug.Log("[Boss] Boss 被击败！");

        //Play SFX
        AudioManager.Instance?.PlaySfxAttachedOnce(SfxId.BossDeath, transform, 1f);

        PlayAnim(ANIM_DEATH);
        isDead = true;

        // ★ 在 Boss 当前位置播放死亡特效
        PlayEffect(deathEffect, transform.position);

        foreach (Crystal c in activeCrystals)
            if (c != null) Destroy(c.gameObject);
        activeCrystals.Clear();

        if (berserkParticleEffect != null) berserkParticleEffect.Stop();
        if (bossGlowLight          != null) bossGlowLight.enabled = false;

        OnBossDied?.Invoke();

        StartCoroutine(DeathDelayRoutine());
    }

    IEnumerator DeathDelayRoutine()
    {
        yield return new WaitForSeconds(4f);
        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    // ★ 新增：特效播放通用方法
    // ─────────────────────────────────────────────

    /// <summary>
    /// 在指定世界坐标实例化特效 Prefab，并在 phaseFxDuration 秒后自动销毁。
    /// effectPrefab 为 null 时静默跳过，不报错。
    /// </summary>
    private void PlayEffect(GameObject effectPrefab, Vector3 position)
    {
        if (effectPrefab == null) return;

        GameObject fx = Instantiate(effectPrefab, position, Quaternion.identity);

        if (phaseFxDuration > 0f)
            Destroy(fx, phaseFxDuration);

        Debug.Log($"[Boss] 特效 [{effectPrefab.name}] 已在 {position} 播放，{phaseFxDuration}s 后销毁。");
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

    public float GetElapsedTime()    => Time.time - bossStartTime;

    public float GetTimeUntilDoubleDamage()
        => doubleDamageTriggered ? 0f : Mathf.Max(0f, doubleDamageTimeLimit - GetElapsedTime());

    public float GetTimeUntilForceBerserk()
        => forceBerserkTriggered ? 0f : Mathf.Max(0f, forceBerserkTimeLimit - GetElapsedTime());
}