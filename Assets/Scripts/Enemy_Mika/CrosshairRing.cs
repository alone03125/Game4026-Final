using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 程序化绘制的准星圆环，挂载在 AttackWarningUI 的准星子物体上。
/// 不依赖任何 Sprite，完全由代码生成网格。
///
/// 接口：
///   ring.Radius    — 圆环半径（画布单位，与 Canvas sizeDelta 同一单位，默认 0.04）
///   ring.Thickness — 线条粗细（画布单位，默认 0.005）
///   ring.Segments  — 圆环分段数，越大越圆（默认 48）
/// </summary>
[AddComponentMenu("UI/Crosshair Ring")]
public class CrosshairRing : Graphic
{
    [SerializeField] private float _radius    = 0.04f;
    [SerializeField] private float _thickness = 0.005f;
    [SerializeField] [Range(8, 128)] private int _segments = 48;

    // ── 公开属性（修改后自动刷新网格）──────────────────────────

    public float Radius
    {
        get => _radius;
        set
        {
            _radius = Mathf.Max(0f, value);
            // 同步 RectTransform 大小，防止被 UI 裁剪系统剔除
            SyncRectSize();
            SetVerticesDirty();
        }
    }

    public float Thickness
    {
        get => _thickness;
        set
        {
            _thickness = Mathf.Max(0.001f, value);
            SetVerticesDirty();
        }
    }

    public int Segments
    {
        get => _segments;
        set
        {
            _segments = Mathf.Max(8, value);
            SetVerticesDirty();
        }
    }

    // ── Unity 生命周期 ─────────────────────────────────────────

    protected override void OnEnable()
    {
        base.OnEnable();
        SyncRectSize();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SyncRectSize();
    }
#endif

    // ── 核心网格生成 ───────────────────────────────────────────

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        int   seg   = Mathf.Max(8, _segments);
        float inner = Mathf.Max(0f, _radius - _thickness * 0.5f);
        float outer = _radius + _thickness * 0.5f;
        float step  = 2f * Mathf.PI / seg;

        for (int i = 0; i < seg; i++)
        {
            float a0 = i       * step;
            float a1 = (i + 1) * step;

            float cos0 = Mathf.Cos(a0), sin0 = Mathf.Sin(a0);
            float cos1 = Mathf.Cos(a1), sin1 = Mathf.Sin(a1);

            int vBase = i * 4;

            UIVertex v = UIVertex.simpleVert;
            v.color = color;

            // 内圆起点 → 外圆起点 → 外圆终点 → 内圆终点
            v.position = new Vector3(cos0 * inner, sin0 * inner, 0f); vh.AddVert(v);
            v.position = new Vector3(cos0 * outer, sin0 * outer, 0f); vh.AddVert(v);
            v.position = new Vector3(cos1 * outer, sin1 * outer, 0f); vh.AddVert(v);
            v.position = new Vector3(cos1 * inner, sin1 * inner, 0f); vh.AddVert(v);

            vh.AddTriangle(vBase,     vBase + 1, vBase + 2);
            vh.AddTriangle(vBase,     vBase + 2, vBase + 3);
        }
    }

    // ── 辅助：让 RectTransform 刚好包住整个圆环，避免被 Mask/Culling 裁掉 ──
    private void SyncRectSize()
    {
        float side = (_radius + _thickness * 0.5f) * 2f;
        rectTransform.sizeDelta = new Vector2(side, side);
    }
}
