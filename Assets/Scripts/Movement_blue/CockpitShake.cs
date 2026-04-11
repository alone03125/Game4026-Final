using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VR 镜头震动系统，支持多种震动事件叠加。
/// 挂载在 Camera Offset（XR Origin 下的子物体）上。
/// XR Tracking 控制 Main Camera 的 localPosition，
/// 而本脚本修改 Camera Offset 的 localPosition，两者叠加实现镜头抖动。
/// </summary>
public class CockpitShake : MonoBehaviour
{
    public static CockpitShake Instance { get; private set; }

    [System.Serializable]
    public class ShakeProfile
    {
        [Tooltip("震动幅度（米）")]
        public float amplitude = 0.02f;
        [Tooltip("震动频率（Hz）")]
        public float frequency = 20f;
        [Tooltip("震动持续时间（秒）")]
        public float duration = 0.2f;
    }

    [Header("走路震动")]
    public ShakeProfile walkShake = new ShakeProfile
    {
        amplitude = 0.005f,
        frequency = 8f,
        duration = 0.15f
    };

    [Header("射击震动")]
    public ShakeProfile shootShake = new ShakeProfile
    {
        amplitude = 0.015f,
        frequency = 25f,
        duration = 0.1f
    };

    [Header("受击震动")]
    public ShakeProfile hitShake = new ShakeProfile
    {
        amplitude = 0.04f,
        frequency = 15f,
        duration = 0.3f
    };

    // 震动来源标识，用于防止同种类型重复触发
    public enum ShakeSource { Walk, Shoot, Hit, Custom }

    // 活跃的震动实例列表（支持不同类型叠加）
    private readonly List<ActiveShake> _activeShakes = new List<ActiveShake>();

    // 上一帧施加的偏移量，用于在下一帧开头撤销，确保震动结束后回归原位
    private Vector3 _lastAppliedOffset = Vector3.zero;

    private class ActiveShake
    {
        public float amplitude;
        public float frequency;
        public float duration;
        public float elapsed;
        public Vector3 seed;
        public ShakeSource source;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 不再在 Start 里锁定位置，避免和 XR 追踪系统冲突

    void LateUpdate()
    {
        // 先撤销上一帧施加的偏移，让 localPosition 回到 XR 追踪的真实值
        transform.localPosition -= _lastAppliedOffset;

        if (_activeShakes.Count == 0)
        {
            _lastAppliedOffset = Vector3.zero;
            return;
        }

        Vector3 totalOffset = Vector3.zero;

        for (int i = _activeShakes.Count - 1; i >= 0; i--)
        {
            ActiveShake s = _activeShakes[i];
            s.elapsed += Time.deltaTime;

            if (s.elapsed >= s.duration)
            {
                _activeShakes.RemoveAt(i);
                continue;
            }

            // 线性衰减
            float decay = 1f - (s.elapsed / s.duration);
            float t = s.elapsed * s.frequency * Mathf.PI * 2f;

            // 用 Perlin Noise + sin 组合产生自然抖动
            Vector3 offset = new Vector3(
                Mathf.PerlinNoise(s.seed.x + t * 0.1f, 0f) * 2f - 1f,
                Mathf.PerlinNoise(0f, s.seed.y + t * 0.1f) * 2f - 1f,
                Mathf.PerlinNoise(s.seed.z + t * 0.1f, s.seed.x) * 2f - 1f
            );
            offset *= s.amplitude * decay;

            totalOffset += offset;
        }

        // 施加本帧偏移并记录，下一帧开头会撤销
        transform.localPosition += totalOffset;
        _lastAppliedOffset = totalOffset;
    }

    // ─── 公开接口 ───────────────────────────────────

    /// <summary>检查某种来源的震动是否正在进行</summary>
    private bool IsShakeActive(ShakeSource source)
    {
        for (int i = 0; i < _activeShakes.Count; i++)
            if (_activeShakes[i].source == source) return true;
        return false;
    }

    /// <summary>使用自定义参数触发一次震动</summary>
    public void Shake(ShakeProfile profile, ShakeSource source = ShakeSource.Custom)
    {
        // 同种来源正在震动时，忽略新的触发
        if (IsShakeActive(source)) return;

        _activeShakes.Add(new ActiveShake
        {
            amplitude = profile.amplitude,
            frequency = profile.frequency,
            duration  = profile.duration,
            elapsed   = 0f,
            seed      = new Vector3(Random.value * 100f, Random.value * 100f, Random.value * 100f),
            source    = source
        });
    }

    /// <summary>使用自定义数值触发一次震动</summary>
    public void Shake(float amplitude, float frequency, float duration, ShakeSource source = ShakeSource.Custom)
    {
        if (IsShakeActive(source)) return;

        _activeShakes.Add(new ActiveShake
        {
            amplitude = amplitude,
            frequency = frequency,
            duration  = duration,
            elapsed   = 0f,
            seed      = new Vector3(Random.value * 100f, Random.value * 100f, Random.value * 100f),
            source    = source
        });
    }

    /// <summary>触发走路震动</summary>
    public void ShakeWalk()    => Shake(walkShake, ShakeSource.Walk);

    /// <summary>触发射击震动</summary>
    public void ShakeShoot()   => Shake(shootShake, ShakeSource.Shoot);

    /// <summary>触发受击震动</summary>
    public void ShakeHit()     => Shake(hitShake, ShakeSource.Hit);

    // ─── 静态便捷方法 ──────────────────────────────

    public static void TriggerWalk()
    {
        if (Instance != null) Instance.ShakeWalk();
    }

    public static void TriggerShoot()
    {
        if (Instance != null) Instance.ShakeShoot();
    }

    public static void TriggerHit()
    {
        if (Instance != null) Instance.ShakeHit();
    }
}
