using System.Collections;
using UnityEngine;

/// <summary>
/// 创建"YOU WIN" 3D 挤出文字效果：
///   - 通过多层 TextMesh 沿本地 Z 轴叠加，模拟立体厚度
///   - 从摄像机视线以下上升到视线平行位置
///   - 到达目标高度后绕 Y 轴持续旋转
/// 使用方法：将此脚本挂载到场景中任意一个空的 GameObject 上即可。
/// </summary>
public class YouWinText : MonoBehaviour
{
    [Header("文字外观")]
    public int fontSize = 60;
    public float characterSize = 0.4f;
    public Color frontColor  = Color.yellow;          // 正面颜色
    public Color sideColor   = new Color(0.6f, 0.45f, 0f); // 侧面（挤出层）颜色
    public FontStyle fontStyle = FontStyle.Bold;

    [Header("3D 挤出")]
    [Tooltip("挤出总深度（单位）")]
    public float extrudeDepth = 0.25f;
    [Tooltip("用多少层来模拟挤出（越多越平滑，性能越低）")]
    [Range(2, 20)]
    public int extrudeLayers = 8;

    [Header("动画参数")]
    [Tooltip("文字出现在摄像机正前方多少单位处")]
    public float offsetFromCamera = 6f;
    [Tooltip("文字起始位置比眼睛高度低多少单位（模拟从地下升起）")]
    public float startDepthBelow = 8f;
    [Tooltip("上升速度（单位/秒）")]
    public float riseSpeed = 4f;

    [Header("旋转控制")]
    [Tooltip("旋转速度：绕 X / Y / Z 轴的度/秒，旋转阶段生效")]
    public Vector3 rotationSpeed = new Vector3(0f, 80f, 0f);

    [Tooltip("Z 轴振荡的最小角度（度）")]
    public float zAngleMin = -180f;
    [Tooltip("Z 轴振荡的最大角度（度）")]
    public float zAngleMax =  180f;

    [Tooltip("文字固定偏移角度（欧拉角），叠加在朝向摄像机的基础旋转之上\n例：X=15 让文字向后仰，Z=10 让文字侧倾")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("延迟显示")]
    [Tooltip("Boss 被击败后延迟多少秒再显示文字")]
    public float delayAfterBoss = 5f;

    [Header("通关时间显示")]
    [Tooltip("时间文字 fontSize 相对主文字的缩放系数")]
    public float timeFontSizeScale = 0.55f;
    [Tooltip("时间文字中心点与 YOU WIN 中心点的纵向间距（世界单位）")]
    public float timeTextYSpacing = 1.2f;
    [Tooltip("时间文字的独立旋转速度（度/秒）")]
    public Vector3 timeRotationSpeed = new Vector3(0f, -65f, 0f);
    [Tooltip("时间文字 Z 轴振荡最小角度")]
    public float timeZAngleMin = -120f;
    [Tooltip("时间文字 Z 轴振荡最大角度")]
    public float timeZAngleMax =  120f;
    [Tooltip("时间文字正面颜色")]
    public Color timeFrontColor = Color.white;
    [Tooltip("时间文字侧面（挤出层）颜色")]
    public Color timeSideColor  = new Color(0.3f, 0.3f, 0.3f);

    // ── 内部状态 ──────────────────────────────────────────────
    private Camera mainCamera;
    private float  targetY;
    private bool   isRising = true;
    private bool   activated = false;

    // X/Y 轴累积角度，Z 轴振荡计时
    private float _xAngle;
    private float _yAngle;
    private float _zTime;

    // 时间显示文字的独立根节点及旋转状态
    private GameObject _timeRoot;
    private float      _tmXAngle;
    private float      _tmYAngle;
    private float      _tmZTime;

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        BuildExtrudedText();
        BuildTimeText();
        // 初始隐藏渲染器，等待 Boss 被击败后再显示
        SetRenderersEnabled(false);
    }

    void OnEnable()
    {
        BossController.OnBossDied += OnBossDied;
    }

    void OnDisable()
    {
        BossController.OnBossDied -= OnBossDied;
    }

    private void OnBossDied()
    {
        StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(delayAfterBoss);

        // 将计时结果写入所有挤出层
        if (_timeRoot != null)
        {
            string timeStr = GameTimer.FormatTime(GameTimer.ElapsedSeconds);
            foreach (var tm in _timeRoot.GetComponentsInChildren<TextMesh>(true))
                tm.text = timeStr;
        }

        SetRenderersEnabled(true);
        Activate();
    }

    private void SetRenderersEnabled(bool on)
    {
        foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            r.enabled = on;

        if (_timeRoot != null)
            foreach (var r in _timeRoot.GetComponentsInChildren<MeshRenderer>(true))
                r.enabled = on;
    }

    private void Activate()
    {
        if (activated) return;
        activated = true;

        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning("[YouWinText] 场景中未找到 Main Camera，请确保摄像机 Tag 设为 MainCamera。");
            enabled = false;
            return;
        }

        // 目标高度 = 摄像机眼睛高度
        targetY = mainCamera.transform.position.y;

        // 计算正前方水平方向
        Vector3 forward = mainCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        // 起始位置：摄像机正前方，低于地面
        transform.position = mainCamera.transform.position
                             + forward * offsetFromCamera
                             + Vector3.down * startDepthBelow;

        // 时间文字初始位置：YOU WIN 正下方
        if (_timeRoot != null)
            _timeRoot.transform.position = transform.position + Vector3.down * timeTextYSpacing;

        ApplyFaceCamera();
    }

    void Update()
    {
        if (!activated || mainCamera == null) return;

        if (isRising)
            RiseToTarget();
        else
            SpinInPlace();
    }

    // ── 构建多层 TextMesh（挤出效果）─────────────────────────
    private void BuildExtrudedText()
    {
        float stepZ = extrudeDepth / Mathf.Max(extrudeLayers - 1, 1);

        for (int i = 0; i < extrudeLayers; i++)
        {
            bool isFront = (i == 0);

            GameObject layer;
            if (isFront)
            {
                // 最前层直接用自身 GameObject
                layer = gameObject;
            }
            else
            {
                layer = new GameObject("ExtrudeLayer_" + i);
                layer.transform.SetParent(transform, false);
            }

            TextMesh tm = layer.AddComponent<TextMesh>();
            tm.text          = "YOU WIN";
            tm.fontSize      = fontSize;
            tm.characterSize = characterSize;
            tm.anchor        = TextAnchor.MiddleCenter;
            tm.alignment     = TextAlignment.Center;
            tm.fontStyle     = fontStyle;

            // 正面最亮，越往后越暗
            if (isFront)
            {
                tm.color = frontColor;
            }
            else
            {
                float t = (float)i / (extrudeLayers - 1);
                tm.color = Color.Lerp(frontColor, sideColor, t);

                // 沿本地 Z 轴向后偏移（正 Z = 远离观察者）
                layer.transform.localPosition = new Vector3(0f, 0f, stepZ * i);
            }

            // Z-sorting：让正面渲染在最上层
            MeshRenderer mr = layer.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sortingOrder = extrudeLayers - i;
        }
    }

    // ── 上升阶段 ─────────────────────────────────────────────
    private void RiseToTarget()
    {
        float newY = Mathf.MoveTowards(transform.position.y, targetY, riseSpeed * Time.deltaTime);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // 时间文字同步跟随上升，保持纵向间距
        if (_timeRoot != null)
            _timeRoot.transform.position = transform.position + Vector3.down * timeTextYSpacing;

        ApplyFaceCamera();

        if (Mathf.Abs(transform.position.y - targetY) < 0.01f)
        {
            transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
            if (_timeRoot != null)
                _timeRoot.transform.position = transform.position + Vector3.down * timeTextYSpacing;
            isRising = false;
        }
    }

    // ── 旋转阶段 ─────────────────────────────────────────────
    private void SpinInPlace()
    {
        // YOU WIN：X / Y 轴持续累积，Z 轴来回振荡
        _xAngle += rotationSpeed.x * Time.deltaTime;
        _yAngle += rotationSpeed.y * Time.deltaTime;

        _zTime += Time.deltaTime;
        float zRange = Mathf.Abs(zAngleMax - zAngleMin);
        float zAngle = zRange > 0f
            ? Mathf.PingPong(_zTime * Mathf.Abs(rotationSpeed.z), zRange) + Mathf.Min(zAngleMin, zAngleMax)
            : zAngleMin;

        transform.rotation = Quaternion.Euler(_xAngle, _yAngle, zAngle);

        // 时间文字：使用独立角度，轨迹与 YOU WIN 完全无关
        if (_timeRoot != null)
        {
            _tmXAngle += timeRotationSpeed.x * Time.deltaTime;
            _tmYAngle += timeRotationSpeed.y * Time.deltaTime;

            _tmZTime += Time.deltaTime;
            float tmZRange = Mathf.Abs(timeZAngleMax - timeZAngleMin);
            float tmZAngle = tmZRange > 0f
                ? Mathf.PingPong(_tmZTime * Mathf.Abs(timeRotationSpeed.z), tmZRange) + Mathf.Min(timeZAngleMin, timeZAngleMax)
                : timeZAngleMin;

            _timeRoot.transform.rotation = Quaternion.Euler(_tmXAngle, _tmYAngle, tmZAngle);
        }
    }

    // ── 朝向摄像机（水平 Billboard）+ 固定偏移角度 ───────────
    private void ApplyFaceCamera()
    {
        Vector3 dir = transform.position - mainCamera.transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            // 基础朝向摄像机的旋转
            Quaternion faceCam = Quaternion.LookRotation(dir);
            // 叠加用户设定的偏移角度（本地空间）
            Quaternion offset  = Quaternion.Euler(rotationOffset);
            transform.rotation = faceCam * offset;

            // 上升阶段时间文字同步朝向摄像机
            if (_timeRoot != null)
                _timeRoot.transform.rotation = faceCam * offset;
        }
    }

    // ── 构建时间显示文字（独立挤出组，根节点为场景级对象）────
    private void BuildTimeText()
    {
        _timeRoot = new GameObject("YouWinTimeRoot");

        int tFontSize = Mathf.Max(1, Mathf.RoundToInt(fontSize * timeFontSizeScale));
        float stepZ   = extrudeDepth / Mathf.Max(extrudeLayers - 1, 1);

        for (int i = 0; i < extrudeLayers; i++)
        {
            bool isFront = (i == 0);

            GameObject layer;
            if (isFront)
            {
                layer = _timeRoot;
            }
            else
            {
                layer = new GameObject("TimeExtrudeLayer_" + i);
                layer.transform.SetParent(_timeRoot.transform, false);
            }

            TextMesh tm   = layer.AddComponent<TextMesh>();
            tm.text          = "00:00.000";  // 占位，显示时替换为实际用时
            tm.fontSize      = tFontSize;
            tm.characterSize = characterSize;
            tm.anchor        = TextAnchor.MiddleCenter;
            tm.alignment     = TextAlignment.Center;
            tm.fontStyle     = fontStyle;

            if (isFront)
            {
                tm.color = timeFrontColor;
            }
            else
            {
                float t  = (float)i / (extrudeLayers - 1);
                tm.color = Color.Lerp(timeFrontColor, timeSideColor, t);
                layer.transform.localPosition = new Vector3(0f, 0f, stepZ * i);
            }

            MeshRenderer mr = layer.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sortingOrder = extrudeLayers - i;
        }
    }
}
