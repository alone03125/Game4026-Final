using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRSimpleInteractable))]
public class CockpitThrottle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] XRSimpleInteractable leverInteractable; // Cube 上的 Simple Interactable
    [SerializeField] Transform xrOrigin;                     // 玩家 XR Origin
    [SerializeField] Transform xrCamera;                     // HMD Camera
    [SerializeField] Transform mechaRoot;                    // 要移動的角色/機甲
    [SerializeField] CharacterController characterController; // 有就用，沒有可留空

    [Header("Movement")]
    [SerializeField] float constantSpeed = 2.0f;             // 固定速度 (m/s)
    [SerializeField] float handSpeedDeadzone = 0.05f;        // 手部速度死區 (m/s)
    [SerializeField] bool useHeadForward = true;             // true: 用頭朝向當前後軸
    [SerializeField] bool flattenToHorizontal = true;        // 只在水平面前後

    [Header("Hold Still Stop")]
    [SerializeField] float stillDeltaMeters = 0.0015f;       // 每幀位移低於此值視為幾乎不動（公尺）
    [SerializeField] float stillStopTime = 0.06f;            // 持續不動超過此時間就強制停止（秒）


    [Header("Gravity Effect")]
    [SerializeField] bool useGameManagerGravity = true;
    [SerializeField] float gravityNeutral = 1f;      // 1.0 視為正常重力
    [SerializeField] float minSpeedMultiplier = 0.4f; // 避免重力太大時幾乎不能動
    [SerializeField] float maxSpeedMultiplier = 1.8f; // 避免重力太小時過快


    IXRSelectInteractor _activeInteractor;
    bool _isControlling;
    Vector3 _prevHandPos;
    bool _hasPrevHandPos;
    float _stillTimer;


    float GetGravitySpeedMultiplier()
{
    if (!useGameManagerGravity) return 1f;
    if (GameManager.Instance == null) return 1f;

    float g = Mathf.Max(0.01f, GameManager.Instance.GetCurrentGravity());

    // 重力越大 -> 倍率越小；重力越小 -> 倍率越大
    float raw = gravityNeutral / g;

    return Mathf.Clamp(raw, minSpeedMultiplier, maxSpeedMultiplier);
}


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
        _stillTimer = 0f;
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        _isControlling = false;
        _activeInteractor = null;
        _hasPrevHandPos = false;
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

        Vector3 handDelta = handNow - _prevHandPos;
        _prevHandPos = handNow;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // 手部靜止判定：按著不動就停
        if (handDelta.sqrMagnitude <= stillDeltaMeters * stillDeltaMeters)
            _stillTimer += dt;
        else
            _stillTimer = 0f;

        int dir = 0;

        if (_stillTimer < stillStopTime)
        {
            // 看「手當前是在往前推還是往後拉」
            float handSpeedAlongPlayerZ = Vector3.Dot(handDelta, playerForward) / dt;

            if (handSpeedAlongPlayerZ > handSpeedDeadzone) dir = 1;        // 前推 -> 前進
            else if (handSpeedAlongPlayerZ < -handSpeedDeadzone) dir = -1; // 後拉 -> 後退
        }

        float gravityMul = GetGravitySpeedMultiplier();
        Vector3 velocity = playerForward * (dir * constantSpeed * gravityMul);
        Vector3 step = velocity * dt;

        if (characterController != null)
            characterController.Move(step);
        else
            mechaRoot.position += step;
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