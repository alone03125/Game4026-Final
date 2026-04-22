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
                Debug.Log($"[BeaconTrigger] 玩家进入信标范围（XZ距离={delta.magnitude:F2}m）");

                // ★ 播放触碰特效
                PlayTriggerEffect();

                OnPlayerEntered?.Invoke();
                return;
            }
        }
    }

    void CreateBeamEffect()
    {
        _beam = gameObject.AddComponent<LineRenderer>();
        _beam.positionCount = 2;
        _beam.SetPosition(0, transform.position);
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
    /// 在信标自身位置实例化触碰特效，并在 triggerEffectDuration 秒后自动销毁。
    /// triggerEffect 为 null 时静默跳过。
    /// </summary>
    void PlayTriggerEffect()
    {
        if (triggerEffect == null) return;

        GameObject fx = Instantiate(triggerEffect, transform.position, Quaternion.identity);

        if (triggerEffectDuration > 0f)
            Destroy(fx, triggerEffectDuration);

        Debug.Log($"[BeaconTrigger] 触碰特效 [{triggerEffect.name}] 已在 {transform.position} 播放。");
    }
}