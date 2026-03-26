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
    [SerializeField] float exitThreshold = 0.04f;             // 退出轉向門檻（m/s），要比 enter 小
    [SerializeField] float speedSmoothTime = 0.08f;           // 手部速度平滑時間（秒）

    [Header("Hold Still Stop")]
    [SerializeField] float stillDeltaMeters = 0.0015f;        // 每幀位移低於此值視為幾乎不動（公尺）
    [SerializeField] float stillStopTime = 0.06f;             // 持續不動超過此時間就強制停轉（秒）

    IXRSelectInteractor _activeInteractor;
    bool _isControlling;
    Vector3 _prevHandPos;
    bool _hasPrevHandPos;

    float _filteredHandSpeedZ; // 平滑後速度
    int _turnState;            // -1 左轉, 0 停止, 1 右轉（有 hysteresis）
    float _stillTimer;         // 手部靜止計時

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
        _prevHandPos = GetInteractorWorldPos(_activeInteractor);
        _hasPrevHandPos = true;

        _filteredHandSpeedZ = 0f;
        _turnState = 0;
        _stillTimer = 0f;
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        _isControlling = false;
        _activeInteractor = null;
        _hasPrevHandPos = false;

        _filteredHandSpeedZ = 0f;
        _turnState = 0;
        _stillTimer = 0f;
    }

    void Update()
    {
        if (!_isControlling || _activeInteractor == null) return;
        if (mechaRoot == null) return;

        Vector3 playerForward = GetPlayerForwardAxis();
        if (playerForward.sqrMagnitude < 0.0001f) return;

        Vector3 handNow = GetInteractorWorldPos(_activeInteractor);
        if (!_hasPrevHandPos)
        {
            _prevHandPos = handNow;
            _hasPrevHandPos = true;
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 handDelta = handNow - _prevHandPos;
        _prevHandPos = handNow;

        // ===== 靜止判定：按著不動就停轉 =====
        if (handDelta.sqrMagnitude <= stillDeltaMeters * stillDeltaMeters)
            _stillTimer += dt;
        else
            _stillTimer = 0f;

        if (_stillTimer >= stillStopTime)
        {
            _turnState = 0;
            _filteredHandSpeedZ = 0f;
            return; // 本幀直接不轉
        }

        // 原始沿玩家Z軸的手部速度
        float rawSpeedZ = Vector3.Dot(handDelta, playerForward) / dt;

        // 一階低通平滑，降低手部追蹤噪聲
        float alpha = dt / (speedSmoothTime + dt);
        _filteredHandSpeedZ = Mathf.Lerp(_filteredHandSpeedZ, rawSpeedZ, alpha);

        // Hysteresis：避免在門檻附近抖動
        switch (_turnState)
        {
            case 0:
                if (_filteredHandSpeedZ > enterThreshold) _turnState = 1;
                else if (_filteredHandSpeedZ < -enterThreshold) _turnState = -1;
                break;

            case 1:
                if (_filteredHandSpeedZ < exitThreshold) _turnState = 0;
                break;

            case -1:
                if (_filteredHandSpeedZ > -exitThreshold) _turnState = 0;
                break;
        }

        int dir = _turnState;
        if (invertTurn) dir = -dir;

        float yaw = dir * turnSpeedDegPerSec * dt;
        mechaRoot.Rotate(0f, yaw, 0f, Space.World);
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
        Transform t = interactor.GetAttachTransform(null);
        return t != null ? t.position : Vector3.zero;
    }
}