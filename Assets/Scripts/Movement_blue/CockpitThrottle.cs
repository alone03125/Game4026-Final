using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRSimpleInteractable))]
public class CockpitThrottle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] XRSimpleInteractable leverInteractable;
    [SerializeField] Transform xrOrigin;
    [SerializeField] Transform xrCamera;
    [SerializeField] Transform mechaRoot;
    [SerializeField] CharacterController characterController;

    [Header("Movement")]
    [SerializeField] float constantSpeed = 2.0f;
    [SerializeField] float handSpeedDeadzone = 0.05f;
    [SerializeField] bool useHeadForward = true;
    [SerializeField] bool flattenToHorizontal = true;

    [Header("Hold Still Stop")]
    [SerializeField] float stillDeltaMeters = 0.0015f;
    [SerializeField] float stillStopTime = 0.06f;

    [Header("Interaction")]
    [Tooltip("若為 true，只有 XRDirectInteractor（近距離碰觸）能啟動，會忽略射線選取")]
    [SerializeField] bool requireDirectInteractorOnly = true;

    [Header("Gravity Effect")]
    [SerializeField] bool useGameManagerGravity = true;
    [SerializeField] float gravityNeutral = 1f;
    [SerializeField] float minSpeedMultiplier = 0.4f;
    [SerializeField] float maxSpeedMultiplier = 1.8f;

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
        if (requireDirectInteractorOnly && args.interactorObject is not XRDirectInteractor)
            return;

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

        Vector3 forward = GetPlayerForwardAxis();
        if (forward.sqrMagnitude < 0.0001f) return;

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        else
            right.Normalize();

        Vector3 handNow = GetInteractorWorldPos(_activeInteractor);

        if (!_hasPrevHandPos)
        {
            _prevHandPos = handNow;
            _hasPrevHandPos = true;
            return;
        }

        Vector3 handDelta = handNow - _prevHandPos;
        _prevHandPos = handNow;

        if (flattenToHorizontal)
            handDelta.y = 0f;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        if (handDelta.sqrMagnitude <= stillDeltaMeters * stillDeltaMeters)
            _stillTimer += dt;
        else
            _stillTimer = 0f;

        Vector3 moveDir = Vector3.zero;

        if (_stillTimer < stillStopTime)
        {
            float alongF = Vector3.Dot(handDelta, forward) / dt;
            float alongR = Vector3.Dot(handDelta, right) / dt;

            float fx = 0f, rx = 0f;
            if (alongF > handSpeedDeadzone) fx = 1f;
            else if (alongF < -handSpeedDeadzone) fx = -1f;

            if (alongR > handSpeedDeadzone) rx = 1f;
            else if (alongR < -handSpeedDeadzone) rx = -1f;

            moveDir = forward * fx + right * rx;
            if (moveDir.sqrMagnitude > 0.0001f)
                moveDir.Normalize();
        }

        float gMul = GetGravitySpeedMultiplier();
        Vector3 velocity = moveDir * (constantSpeed * gMul);
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