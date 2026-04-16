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

    [System.Serializable]
    public class MechWalkSettings
    {
        [Tooltip("步频（步/秒），可通过 CockpitShake.SetStepRate() 或 Inspector 调整")]
        public float stepRate = 0.7f;
        [Tooltip("垂直起伏幅度（米）。推荐值：轻型机甲 0.035 / 重型机甲 0.06")]
        public float verticalAmplitude = -0.02f;
        [Tooltip("左右摇摆幅度（米）。推荐值：轻型机甲 0.02 / 重型机甲 0.04")]
        public float lateralAmplitude = 0.05f;
        [Tooltip("踏步触地下沉幅度（米）。推荐值：轻型机甲 0.04 / 重型机甲 0.08")]
        public float impactAmplitude = 0.06f;
        [Tooltip("踏步冲击衰减速度（越大消散越快）")]
        public float impactDecay = 2.1f;
        [Tooltip("停止行走后效果淡出时间（秒）")]
        public float fadeOutTime = 0.18f;
    }

    [Header("机甲行走效果")]
    public MechWalkSettings mechWalk = new MechWalkSettings();

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

    // ── 机甲行走状态 ──
    private float _walkPhase    = 0f;   // 行走相位累积
    private float _walkBlend    = 0f;   // 0=静止 1=全速行走（平滑渐入渐出）
    private bool  _walkRequested = false; // 本帧是否收到行走信号
    private float _impactOffset = 0f;  // 踏步冲击 Y 偏移

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

    void LateUpdate()
    {
        // 先撤销上一帧施加的偏移，让 localPosition 回到 XR 追踪的真实值
        transform.localPosition -= _lastAppliedOffset;

        Vector3 totalOffset = Vector3.zero;

        // ── 随机震动叠加（射击 / 受击等） ──
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

        // ── 机甲行走周期性效果 ──
        float walkTarget = _walkRequested ? 1f : 0f;
        _walkBlend = Mathf.MoveTowards(_walkBlend, walkTarget,
                         Time.deltaTime / Mathf.Max(0.01f, mechWalk.fadeOutTime));
        _walkRequested = false; // 消耗本帧信号，下帧须重新触发

        if (_walkBlend > 0.001f)
        {
            float prevPhase = _walkPhase;
            _walkPhase += mechWalk.stepRate * Mathf.PI * 2f * Time.deltaTime;

            // 踏步触地检测：每过半周期（π）算一步
            int prevStep = Mathf.FloorToInt(prevPhase / Mathf.PI);
            int currStep = Mathf.FloorToInt(_walkPhase / Mathf.PI);
            if (currStep > prevStep)
                _impactOffset = mechWalk.impactAmplitude; // 触地瞬间上升

            // 冲击指数衰减回零
            _impactOffset = Mathf.Lerp(_impactOffset, 0f, mechWalk.impactDecay * Time.deltaTime);

            // 垂直起伏：+|sin| → 步间中段（腿伸直）最高，触地瞬间（phase=nπ）回到零
            // 配合 impactOffset 触地上升脉冲，完整节律：触地(0)→上升脉冲→中间最高→下降→触地
            float vertBob = Mathf.Abs(Mathf.Sin(_walkPhase)) * mechWalk.verticalAmplitude;
            // 左右摇摆：sin(phase/2) 使完整摆动跨越两步
            float latSway = Mathf.Sin(_walkPhase * 1f) * mechWalk.lateralAmplitude;

            totalOffset += new Vector3(latSway, vertBob + _impactOffset, 0f) * _walkBlend;
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

    /// <summary>标记本帧正在行走（每帧调用一次；停止调用后自动淡出）</summary>
    public void RequestMechWalk() => _walkRequested = true;

    /// <summary>触发射击震动</summary>
    public void ShakeShoot()   => Shake(shootShake, ShakeSource.Shoot);

    /// <summary>触发受击震动</summary>
    public void ShakeHit()     => Shake(hitShake, ShakeSource.Hit);

    // ─── 静态便捷方法 ──────────────────────────────

    /// <summary>动态调整机甲步频（步/秒）。也可直接在 Inspector 修改 mechWalk.stepRate。</summary>
    public static void SetStepRate(float stepsPerSecond)
    {
        if (Instance != null) Instance.mechWalk.stepRate = stepsPerSecond;
    }

    public static void TriggerWalk()
    {
        if (Instance != null) Instance.RequestMechWalk();
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
