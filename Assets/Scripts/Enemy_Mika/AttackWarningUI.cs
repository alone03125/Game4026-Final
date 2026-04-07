using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VR 屏幕边缘攻击预警方向指示器
///
/// 使用说明：
///   1. 在 VR 摄像机下新建子物体，命名 WarningCanvas。
///   2. 挂载 Canvas 组件，Render Mode = World Space。
///   3. 将此脚本挂载在 WarningCanvas 上。
///   4. 设置 Canvas 的 Local Position = (0, 0, 1)，让它在玩家正前方 1 米处。
///   5. 设置 Canvas 的 Width/Height（单位：米），大约等于视野边缘范围，
///      推荐：Width = 1.6，Height = 0.9。
///   6. 创建箭头 Prefab（一个 Image，带三角形/方块 Sprite），赋给 arrowPrefab 槽位。
/// </summary>
[RequireComponent(typeof(Canvas))]
public class AttackWarningUI : MonoBehaviour
{
    [Header("箭头预制体")]
    [Tooltip("箭头 UI 预制体，需含 Image 组件，默认朝上为「指向」方向")]
    public GameObject arrowPrefab;

    [Tooltip("最多同时显示多少个箭头（对应最多同时蓄力的敌人数）")]
    public int maxArrows = 8;

    [Header("位置与大小")]
    [Tooltip("箭头距画布边缘的内缩距离（画布单位，例如 0.05 = 5cm）")]
    public float edgePadding = 0.05f;

    [Tooltip("箭头的 UI 尺寸（画布单位）")]
    public float arrowSize = 0.08f;

    [Header("视觉样式")]
    public Color arrowColor        = new Color(1f, 0.25f, 0.25f, 0.9f);
    public Color arrowChargingColor = new Color(1f, 1f, 0f, 1f);  // 蓄力时的闪烁颜色（默认黄色）

    [Header("脉冲动画")]
    [Range(0f, 1f)]
    public float normalAlpha = 0.7f;  // 非蓄力时箭头的固定透明度

    [Header("蓄力闪烁")]
    public float blinkSpeed   = 8f;   // 闪烁频率（每秒闪烁次数）
    [Range(0f, 1f)]
    public float blinkMinAlpha = 0.1f; // 闪烁最低透明度

    // ─── 私有 ───────────────────────────────────────────────
    private Camera         _cam;
    private RectTransform  _canvasRect;
    private readonly List<RectTransform> _arrows      = new List<RectTransform>();
    private readonly List<Image>         _arrowImages = new List<Image>();

    void Awake()
    {
        _canvasRect = GetComponent<RectTransform>();

        // 找到父级摄像机（VR 相机）
        _cam = GetComponentInParent<Camera>();
        if (_cam == null) _cam = Camera.main;

        if (arrowPrefab == null)
        {
            Debug.LogError("[AttackWarningUI] 请在 Inspector 中赋值 arrowPrefab！");
            enabled = false;
            return;
        }

        // 预创建箭头对象池
        for (int i = 0; i < maxArrows; i++)
        {
            GameObject go  = Instantiate(arrowPrefab, transform);
            var rt  = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();

            // 强制设置 anchor 和 pivot为画布中心，否则 anchoredPosition 的原点会错位
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = Vector2.one * arrowSize;
            rt.localScale       = Vector3.one;
            rt.localRotation    = Quaternion.identity;
            rt.anchoredPosition3D = Vector3.zero;  // 明确重置 X/Y/Z
            if (img != null) img.color = arrowColor;

            go.SetActive(false);
            _arrows.Add(rt);
            _arrowImages.Add(img);
        }
    }

    void Start()
    {
        // 清理已销毁的 stale 引用（编辑器重启 Play Mode 后可能残留）
        Enemy.ActiveEnemies.RemoveAll(e => e == null);

        // 诊断日志：打印画布实际尺寸，确认坐标系正确
        Vector2 sz = _canvasRect.sizeDelta;
        Debug.Log($"[AttackWarningUI] 初始化完成 | Canvas sizeDelta=({sz.x},{sz.y}) "
                + $"| rect=({_canvasRect.rect.width},{_canvasRect.rect.height}) "
                + $"| 敌人数: {Enemy.ActiveEnemies.Count}");
    }

    void Update()
    {
        // 先全部隐藏
        for (int i = 0; i < _arrows.Count; i++)
            _arrows[i].gameObject.SetActive(false);

        if (_cam == null || Enemy.ActiveEnemies.Count == 0) return;

        // 用 sizeDelta 获取画布尺寸（World Space 模式下比 rect 更可靠）
        Vector2 canvasSize = _canvasRect.sizeDelta;
        float halfW = canvasSize.x * 0.5f - edgePadding;
        float halfH = canvasSize.y * 0.5f - edgePadding;

        int idx = 0;
        foreach (Enemy enemy in Enemy.ActiveEnemies)
        {
            if (enemy == null) continue;          // 防止 stale 引用异常
            if (idx >= maxArrows)    break;

            // ── VR 兼容方案：用方向向量投影到 Canvas 平面，不依赖视口坐标 ──
            // 将敌人方向转换到 Canvas 的本地空间（Canvas 是相机子物体，始终面向前方）
            Vector3 worldDir   = (enemy.transform.position - _cam.transform.position).normalized;
            Vector3 localDir   = transform.InverseTransformDirection(worldDir);

            // 用 X（左右）和 Z（前后）映射到画布，忽略世界 Y 轴高低差
            // 箭头朝上 = 敌人在前方，朝下 = 背后，朝左右 = 两侧
            Vector2 offset = new Vector2(localDir.x, localDir.z);
            if (offset.sqrMagnitude < 1e-6f) offset = Vector2.up;

            // ── 夹到画布边缘 ──
            Vector2 edgePos = ClampToEdge(offset.normalized, halfW, halfH);

            // ── 设置箭头位置 / 旋转 / 动画 ──
            RectTransform rt  = _arrows[idx];
            Image         img = _arrowImages[idx];
            rt.gameObject.SetActive(true);
            rt.anchoredPosition3D = new Vector3(edgePos.x, edgePos.y, 0f);  // Z 明确归零

            // 箭头尖端指向敌人方向
            float angle = Vector2.SignedAngle(Vector2.up, offset);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);

            if (enemy.IsCharging)
            {
                // 蓄力闪烁：只改变 Alpha，不改变尺寸
                float blinkAlpha = Mathf.Lerp(blinkMinAlpha, 1f,
                    (Mathf.Sin(Time.time * blinkSpeed * Mathf.PI) + 1f) * 0.5f);
                Color c = arrowChargingColor;
                c.a = blinkAlpha;
                if (img != null) img.color = c;
            }
            else
            {
                // 正常状态：固定颜色和透明度
                Color c = arrowColor;
                c.a = normalAlpha;
                if (img != null) img.color = c;
            }
            // 尺寸始终保持不变
            rt.localScale = Vector3.one;

            idx++;
        }
    }

    /// <summary>将归一化方向投影到矩形（halfW × halfH）的边缘点</summary>
    static Vector2 ClampToEdge(Vector2 dir, float halfW, float halfH)
    {
        // 分别计算到左右边和上下边所需的缩放系数，取最小值
        float sx = Mathf.Abs(dir.x) > 1e-6f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
        float sy = Mathf.Abs(dir.y) > 1e-6f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
        return dir * Mathf.Min(sx, sy);
    }
}
