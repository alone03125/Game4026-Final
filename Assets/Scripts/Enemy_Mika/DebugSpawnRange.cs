using UnityEngine;

/// <summary>
/// 将此脚本挂载到与 EnemySpawner 同一个 GameObject 上，
/// 即可在 Scene 视图（以及可选的 Game 视图）中看到每个阶段的刷新圆柱范围。
/// 仅在编辑器及开发构建中有效，不影响正式发布包体积。
/// </summary>
[RequireComponent(typeof(EnemySpawner))]
public class DebugSpawnRange : MonoBehaviour
{
    [Header("可视化设置")]
    [Tooltip("是否在 Game 视图（运行时）也绘制辅助线")]
    public bool drawInGameView = true;

    [Tooltip("圆柱轮廓用多少段折线近似（越大越圆，建议 32-64）")]
    [Range(8, 64)]
    public int circleSegments = 32;

    [Tooltip("各阶段的绘制颜色，数量不足时循环使用")]
    public Color[] stageColors = new Color[]
    {
        new Color(0.2f, 0.8f, 0.2f, 0.8f),   // 阶段 0 — 绿色
        new Color(0.2f, 0.6f, 1.0f, 0.8f),   // 阶段 1 — 蓝色
        new Color(1.0f, 0.4f, 0.2f, 0.8f),   // 阶段 2 — 橙色
    };

    [Tooltip("Boss 阶段专属范围的绘制颜色")]
    public Color bossStageColor = new Color(1.0f, 0.1f, 0.8f, 0.9f);  // 洋红色

    [Header("当前阶段高亮")]
    [Tooltip("高亮当前激活阶段时是否加粗（用额外的偏移圆圈叠加表示）")]
    public bool highlightCurrentStage = true;

    // ── 运行时缓存 ──────────────────────────────────────────────
    private EnemySpawner _spawner;
    private Transform _player;

    void Awake()
    {
        _spawner = GetComponent<EnemySpawner>();
    }

    void Update()
    {
        // 每帧刷新 player 引用（支持场景中途加载玩家）
        if (_player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _player = playerObj.transform;
        }
    }

    // ── Scene 视图 Gizmo ────────────────────────────────────────
    void OnDrawGizmos()
    {
        if (_spawner == null) _spawner = GetComponent<EnemySpawner>();
        DrawAllStages();
    }

    // ── Game 视图运行时绘制（使用 Debug.DrawLine）──────────────
    void LateUpdate()
    {
        if (!drawInGameView) return;
        DrawAllStagesRuntime();
    }

    // ── 核心绘制逻辑 ────────────────────────────────────────────

    private void DrawAllStages()
    {
        if (_spawner == null || _spawner.stages == null) return;

        Transform player = _player != null ? _player : (_player = FindPlayer());

        for (int i = 0; i < _spawner.stages.Length; i++)
        {
            Color col = GetStageColor(i);
            bool isCurrent = highlightCurrentStage && !GetIsBossStage() && (i == GetCurrentStage());

            DrawCylinderGizmo(_spawner.stages[i], player, col, isCurrent, $"Stage {i}");
        }

        // Boss 阶段专属范围
        if (_spawner.useBossStageRange)
        {
            bool isBossCurrent = highlightCurrentStage && GetIsBossStage();
            DrawCylinderGizmo(_spawner.bossStageSpawnRange, player, bossStageColor, isBossCurrent, "Boss Stage");
        }
    }

    private void DrawAllStagesRuntime()
    {
        if (_spawner == null || _spawner.stages == null) return;

        for (int i = 0; i < _spawner.stages.Length; i++)
        {
            Color col = GetStageColor(i);
            DrawCylinderRuntime(_spawner.stages[i], _player, col);
        }

        // Boss 阶段专属范围
        if (_spawner.useBossStageRange)
            DrawCylinderRuntime(_spawner.bossStageSpawnRange, _player, bossStageColor);
    }

    // ── 使用 Gizmos API（Scene 视图）────────────────────────────

    private void DrawCylinderGizmo(EnemySpawner.StageSpawnRange range, Transform player, Color color, bool highlight, string label)
    {
        Vector3 origin = player != null ? player.position : Vector3.zero;
        Vector3 forward = player != null
            ? new Vector3(player.forward.x, 0f, player.forward.z).normalized
            : Vector3.forward;

        float r = range.horizontalRange;
        float topY    = origin.y + range.upperOffset;
        float bottomY = origin.y - range.lowerOffset;
        float centerY = (topY + bottomY) * 0.5f;

        // forwardOffset 偏移（水平）
        Vector3 centerXZ = new Vector3(origin.x, centerY, origin.z) + forward * range.forwardOffset;

        Color prevColor = Gizmos.color;
        Gizmos.color = color;

        // 顶圆 & 底圆
        DrawGizmoCircle(new Vector3(centerXZ.x, topY,    centerXZ.z), r);
        DrawGizmoCircle(new Vector3(centerXZ.x, bottomY, centerXZ.z), r);

        // 四条垂直线
        for (int k = 0; k < 4; k++)
        {
            float angle = k * 90f * Mathf.Deg2Rad;
            float dx = Mathf.Cos(angle) * r;
            float dz = Mathf.Sin(angle) * r;
            Gizmos.DrawLine(
                new Vector3(centerXZ.x + dx, topY,    centerXZ.z + dz),
                new Vector3(centerXZ.x + dx, bottomY, centerXZ.z + dz)
            );
        }

        // 标记圆柱中心轴
        Gizmos.DrawLine(
            new Vector3(centerXZ.x, topY,    centerXZ.z),
            new Vector3(centerXZ.x, bottomY, centerXZ.z)
        );

        // forwardOffset 参考线（从玩家到圆柱中心 XZ）
        if (Mathf.Abs(range.forwardOffset) > 0.01f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(
                new Vector3(origin.x, centerY, origin.z),
                new Vector3(centerXZ.x, centerY, centerXZ.z)
            );
        }

        // 高亮当前阶段：额外膨胀一圈
        if (highlight)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
            DrawGizmoCircle(new Vector3(centerXZ.x, topY,    centerXZ.z), r + 0.5f);
            DrawGizmoCircle(new Vector3(centerXZ.x, bottomY, centerXZ.z), r + 0.5f);
        }

        Gizmos.color = prevColor;

        // 文字标签
#if UNITY_EDITOR
        string stageLabel = $"{label}  r={r:F1}  fwd={range.forwardOffset:F1}\n"
                          + $"上={range.upperOffset:F1}  下={range.lowerOffset:F1}";
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(new Vector3(centerXZ.x + r, topY + 0.3f, centerXZ.z), stageLabel);
#endif
    }

    private void DrawGizmoCircle(Vector3 center, float radius)
    {
        int seg = circleSegments;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i       * 2f * Mathf.PI / seg;
            float a1 = (i + 1) * 2f * Mathf.PI / seg;
            Gizmos.DrawLine(
                center + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius),
                center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius)
            );
        }
    }

    // ── 使用 Debug.DrawLine（Game 视图）────────────────────────

    private void DrawCylinderRuntime(EnemySpawner.StageSpawnRange range, Transform player, Color color)
    {
        Vector3 origin = player != null ? player.position : Vector3.zero;
        Vector3 forward = player != null
            ? new Vector3(player.forward.x, 0f, player.forward.z).normalized
            : Vector3.forward;

        float r = range.horizontalRange;
        float topY    = origin.y + range.upperOffset;
        float bottomY = origin.y - range.lowerOffset;
        float centerY = (topY + bottomY) * 0.5f;

        Vector3 centerXZ = new Vector3(origin.x, centerY, origin.z) + forward * range.forwardOffset;

        int seg = circleSegments;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i       * 2f * Mathf.PI / seg;
            float a1 = (i + 1) * 2f * Mathf.PI / seg;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);

            // 顶圆
            Debug.DrawLine(
                new Vector3(centerXZ.x, topY, centerXZ.z) + p0,
                new Vector3(centerXZ.x, topY, centerXZ.z) + p1,
                color);
            // 底圆
            Debug.DrawLine(
                new Vector3(centerXZ.x, bottomY, centerXZ.z) + p0,
                new Vector3(centerXZ.x, bottomY, centerXZ.z) + p1,
                color);
        }

        // 四条垂直边
        for (int k = 0; k < 4; k++)
        {
            float angle = k * 90f * Mathf.Deg2Rad;
            float dx = Mathf.Cos(angle) * r;
            float dz = Mathf.Sin(angle) * r;
            Debug.DrawLine(
                new Vector3(centerXZ.x + dx, topY,    centerXZ.z + dz),
                new Vector3(centerXZ.x + dx, bottomY, centerXZ.z + dz),
                color);
        }

        // forwardOffset 参考线
        if (Mathf.Abs(range.forwardOffset) > 0.01f)
        {
            Debug.DrawLine(
                new Vector3(origin.x, centerY, origin.z),
                new Vector3(centerXZ.x, centerY, centerXZ.z),
                Color.yellow);
        }
    }

    // ── 工具方法 ────────────────────────────────────────────────

    private Color GetStageColor(int index)
    {
        if (stageColors == null || stageColors.Length == 0)
            return Color.white;
        return stageColors[index % stageColors.Length];
    }

    private int GetCurrentStage()
    {
        // 通过反射读取私有字段，避免修改 EnemySpawner
        var field = typeof(EnemySpawner).GetField("currentStage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? (int)field.GetValue(_spawner) : 0;
    }

    private bool GetIsBossStage()
    {
        var field = typeof(EnemySpawner).GetField("isBossStage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null && (bool)field.GetValue(_spawner);
    }

    private Transform FindPlayer()
    {
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        return obj != null ? obj.transform : null;
    }
}
