using System;
using UnityEngine;

/// <summary>
/// 挂载在信标 GameObject 上，当 Tag 为 "PlayerBody" 的物体在 XZ 平面上进入指定半径时触发回调。
/// 忽略 Y 轴高度差。使用 LineRenderer 生成向上的光束（参考敌人攻击预警风格）。
/// </summary>
public class BeaconTrigger : MonoBehaviour
{
    [Tooltip("XZ 平面触发半径（米）")]
    public float triggerRadius = 3f;

    [Header("光束特效")]
    [Tooltip("光束底部颜色")]
    public Color beamStartColor = new Color(0.3f, 0.8f, 1f, 1f);
    [Tooltip("光束顶部颜色（渐隐）")]
    public Color beamEndColor   = new Color(0.3f, 0.8f, 1f, 0f);
    [Tooltip("光束高度（米）")]
    public float beamHeight = 30f;
    [Tooltip("光束底部宽度")]
    public float beamStartWidth = 0.5f;
    [Tooltip("光束顶部宽度")]
    public float beamEndWidth   = 0.1f;
    [Tooltip("光束材质（留空则使用 Sprites/Default）")]
    public Material beamMaterial;

    [Header("脉冲动画")]
    public float pulseSpeed = 4f;
    [Range(0f, 0.5f)]
    public float pulseAmount = 0.3f;

    // ★ 新增：触碰特效
    [Header("触碰特效")]
    [Tooltip("玩家触碰信标时播放的特效 Prefab（粒子系统等）")]
    public GameObject triggerEffect;
    [Tooltip("特效自动销毁延迟（秒），设为 0 则不自动销毁）")]
    public float triggerEffectDuration = 3f;

    /// <summary>玩家进入信标范围时的回调，由 GameFlowManager 赋值。</summary>
    public Action OnPlayerEntered;

    private bool _triggered;
    private LineRenderer _beam;
    private float _baseStartWidth;
    private float _baseEndWidth;

    void Start()
    {
        CreateBeamEffect();
    }

    void Update()
    {
        // 脉冲动画
        if (_beam != null && _beam.enabled)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            _beam.startWidth = _baseStartWidth * pulse;
            _beam.endWidth   = _baseEndWidth * pulse;
        }

        if (_triggered) return;

        // 查找场景中所有 Tag 为 PlayerBody 的物体
        GameObject[] players = GameObject.FindGameObjectsWithTag("PlayerBody");
        foreach (GameObject player in players)
        {
            Vector3 delta = player.transform.position - transform.position;
            delta.y = 0f;  // 忽略 Y 轴
            if (delta.sqrMagnitude <= triggerRadius * triggerRadius)
            {
                _triggered = true;

                //play SFX
                AudioManager.Instance?.PlaySfxAtPoint(SfxId.BeaconReached, transform.position, 1f);
                
                Debug.Log($"[BeaconTrigger] 玩家进入信标范围（XZ距离={delta.magnitude:F2}m）");

                // ★ 在玩家面前6格远2格高播放触碰特效
                Vector3 effectPosition = player.transform.position + player.transform.forward * 6f + Vector3.up * 2f;
                PlayTriggerEffect(effectPosition);

                OnPlayerEntered?.Invoke();
                return;
            }
        }
    }

    void CreateBeamEffect()
    {
        _beam = gameObject.AddComponent<LineRenderer>();
        _beam.positionCount = 2;
        _beam.SetPosition(0, transform.position + Vector3.up * 1f);  // 从稍微高于地面的点开始，避免穿地面
        _beam.SetPosition(1, transform.position + Vector3.up * beamHeight);
        _beam.startWidth = beamStartWidth;
        _beam.endWidth   = beamEndWidth;
        _beam.material   = beamMaterial != null
            ? beamMaterial
            : new Material(Shader.Find("Sprites/Default"));
        _beam.startColor = beamStartColor;
        _beam.endColor   = beamEndColor;
        _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _beam.receiveShadows = false;
        _beam.useWorldSpace = true;

        _baseStartWidth = beamStartWidth;
        _baseEndWidth   = beamEndWidth;
    }

    // ★ 新增：触碰特效播放方法
    /// <summary>
    /// 在信标自身位置实例化触碰特效，显式播放层级内所有 ParticleSystem，
    /// 并根据粒子实际时长（或 triggerEffectDuration）自动销毁。
    /// triggerEffect 为 null 时静默跳过。
    /// </summary>
    void PlayTriggerEffect(Vector3? positionOverride = null)
    {
        if (triggerEffect == null) return;

        Vector3 spawnPosition = positionOverride ?? transform.position;
        GameObject fx = Instantiate(triggerEffect, spawnPosition, Quaternion.identity);

        // 显式播放所有粒子系统（覆盖 Play On Awake 未勾选的情况）
        ParticleSystem[] allPs = fx.GetComponentsInChildren<ParticleSystem>(true);
        float maxDuration = triggerEffectDuration > 0f ? triggerEffectDuration : 2f;

        foreach (ParticleSystem ps in allPs)
        {
            ps.gameObject.SetActive(true);
            ps.Play(true);

            // 若未指定固定时长，则从粒子自身参数推算最大存活时间
            if (triggerEffectDuration <= 0f)
            {
                var lt = ps.main.startLifetime;
                float lifetime = lt.mode == ParticleSystemCurveMode.Constant
                    ? lt.constant
                    : Mathf.Max(lt.constantMin, lt.constantMax);
                float total = ps.main.duration + lifetime;
                if (total > maxDuration) maxDuration = total;
            }
        }

        // 无粒子系统时保底 5 秒销毁
        if (allPs.Length == 0 && triggerEffectDuration <= 0f)
            maxDuration = 5f;

        Destroy(fx, maxDuration + 0.2f);   // 额外留 0.2s 缓冲

        Debug.Log($"[BeaconTrigger] 触碰特效 [{triggerEffect.name}] 已在 {spawnPosition} 播放，将在 {maxDuration + 0.2f:F1}s 后销毁。");
    }
}