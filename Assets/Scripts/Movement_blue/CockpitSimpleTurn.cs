using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRSimpleInteractable))]
public class CockpitSimpleTurn : MonoBehaviour
{
    [Header("References")]
    [SerializeField] XRSimpleInteractable leverInteractable;
    [SerializeField] Transform xrOrigin;
    [SerializeField] Transform xrCamera;
    [SerializeField] Transform mechaRoot;

    [Header("Turning")]
    [SerializeField] float turnSpeedDegPerSec = 90f;          // 固定轉向速度（度/秒）
    [SerializeField] bool useHeadForward = true;
    [SerializeField] bool flattenToHorizontal = true;
    [SerializeField] bool invertTurn = false;                 // 反轉左右

    [Header("Anti Jitter")]
    [SerializeField] float enterThreshold = 0.10f;            // 進入轉向門檻（m/s）
    // [SerializeField] float exitThreshold = 0.04f;             // 退出轉向門檻（m/s），要比 enter 小
    // [SerializeField] float speedSmoothTime = 0.08f;           // 手部速度平滑時間（秒）

    // [Header("Hold Still Stop")]
    // [SerializeField] float stillDeltaMeters = 0.0015f;        // 每幀位移低於此值視為幾乎不動（公尺）
    // [SerializeField] float stillStopTime = 0.06f;             // 持續不動超過此時間就強制停轉（秒）


    [Header("Gravity Effect")]
    [SerializeField] bool useGameManagerGravity = true;
    [SerializeField] float gravityNeutral = 1f;       // 1.0 視為正常重力
    [SerializeField] float minTurnMultiplier = 0.5f;  // 高重力時最慢轉向倍率
    [SerializeField] float maxTurnMultiplier = 1.5f;  // 低重力時最快轉向倍率

    IXRSelectInteractor _activeInteractor;
    bool _isControlling;
    // Vector3 _prevHandPos;
    // bool _hasPrevHandPos;
    private Vector3 _grabStartHandPos;

    // float _filteredHandSpeedZ; // 平滑後速度
    // int _turnState;            // -1 左轉, 0 停止, 1 右轉（有 hysteresis）
    // float _stillTimer;         // 手部靜止計時

    void Reset()
    {
        leverInteractable = GetComponent<XRSimpleInteractable>();
    }

    void OnEnable()
    {
        if (leverInteractable == null) return;
        leverInteractable.selectEntered.AddListener(OnSelectEntered);
        leverInteractable.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        if (leverInteractable == null) return;
        leverInteractable.selectEntered.RemoveListener(OnSelectEntered);
        leverInteractable.selectExited.RemoveListener(OnSelectExited);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        _activeInteractor = args.interactorObject;
        _isControlling = true;
        // _prevHandPos = GetInteractorWorldPos(_activeInteractor);
        // _hasPrevHandPos = true;

        // _filteredHandSpeedZ = 0f;
        // _turnState = 0;
        // _stillTimer = 0f;
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        _isControlling = false;
        _activeInteractor = null;
        // _hasPrevHandPos = false;

        // _filteredHandSpeedZ = 0f;
        // _turnState = 0;
        // _stillTimer = 0f;

        _grabStartHandPos = GetInteractorWorldPos(_activeInteractor);
        
    }

   void Update()
{
    if (!_isControlling || _activeInteractor == null) return;
    if (mechaRoot == null) return;

    float dt = Mathf.Max(Time.deltaTime, 0.0001f);
    Vector3 playerForward = GetPlayerForwardAxis();
    if (playerForward.sqrMagnitude < 0.0001f) return;

    Vector3 handNow = GetInteractorWorldPos(_activeInteractor);

    // 相對抓取起點偏移
    Vector3 handOffset = handNow - _grabStartHandPos;
    float offsetAlongPlayerZ = Vector3.Dot(handOffset, playerForward);

    int dir = 0;
    if (offsetAlongPlayerZ > enterThreshold) dir = 1;          // 持續右轉（或你的定義方向）
    else if (offsetAlongPlayerZ < -enterThreshold) dir = -1;   // 立刻反向
    // 中立區間不轉

    if (invertTurn) dir = -dir;

    float turnMul = GetGravityTurnMultiplier();
    float yaw = dir * turnSpeedDegPerSec * turnMul * dt;
    mechaRoot.Rotate(0f, yaw, 0f, Space.World);
}

    float GetGravityTurnMultiplier()
    {
        if (!useGameManagerGravity) return 1f;
        if (GameManager.Instance == null) return 1f;
        float g = Mathf.Max(0.01f, GameManager.Instance.GetCurrentGravity());
        float raw = gravityNeutral / g; // 重力大 -> 倍率小；重力小 -> 倍率大
        return Mathf.Clamp(raw, minTurnMultiplier, maxTurnMultiplier);
    }

    Vector3 GetPlayerForwardAxis()
    {
        Transform basis = null;

        if (useHeadForward && xrCamera != null)
            basis = xrCamera;
        else if (xrOrigin != null)
            basis = xrOrigin;
        else if (Camera.main != null)
            basis = Camera.main.transform;

        if (basis == null)
            return Vector3.forward;

        Vector3 fwd = basis.forward;

        if (flattenToHorizontal)
        {
            fwd.y = 0f;
            float mag = fwd.magnitude;
            if (mag > 0.0001f) fwd /= mag;
        }

        return fwd;
    }

    static Vector3 GetInteractorWorldPos(IXRSelectInteractor interactor)
    {
        if (interactor == null) return Vector3.zero;
        Transform t = interactor.GetAttachTransform(null);
        return t != null ? t.position : Vector3.zero;
    }
}