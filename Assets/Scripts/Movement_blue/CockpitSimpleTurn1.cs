using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRSimpleInteractable))]
public class CockpitSimpleTurn : MonoBehaviour
{
    [Header("References")]
    [SerializeField] XRSimpleInteractable leverInteractable;
    [SerializeField] Transform xrOrigin;
    [SerializeField] Transform xrCamera;
    [SerializeField] Transform yawPivot;
    [SerializeField] Transform motionReference;
    [SerializeField] bool useHeadForward = true;
    [SerializeField] bool flattenToHorizontal = true;

    [Header("Turn")]
    [SerializeField] float yawDegreesPerSecond = 90f;
    [SerializeField] float positionDeadzoneMeters = 0.02f;
    [SerializeField] float maxOffsetForFullSpeed = 0.12f;
    // true: Keeps spinning as long as hand remains on the side (depending on position)
    [SerializeField] bool usePositionBasedTurn = true;

    [Header("Pitch (俯仰)")]
    [SerializeField] float pitchDegreesPerSecond = 60f;
    [Tooltip("低头最大角度（正值，实际为负方向）")]
    [SerializeField] float maxPitchDown = 15f;
    [Tooltip("抬头最大角度")]
    [SerializeField] float maxPitchUp = 35f;

    [Header("Velocity")]
    [SerializeField] float handSpeedDeadzoneLR = 0.05f;
    [SerializeField] float stillDeltaMeters = 0.0015f;
    [SerializeField] float stillStopTime = 0.06f;

    [Header("Interaction")]
    [SerializeField] bool requireDirectInteractorOnly = true;

    [Header("Gravity Effect")]
    [SerializeField] bool useGameManagerGravity = false;
    [SerializeField] float gravityNeutral = 1f;
    [SerializeField] float minSpeedMultiplier = 0.4f;
    [SerializeField] float maxSpeedMultiplier = 1.8f;

    public float LastLateralAnalog { get; private set; }
    public float LastForwardAnalog { get; private set; }

    IXRSelectInteractor _activeInteractor;
    bool _isControlling;
    bool _hasPrevHandLocal;
    Vector3 _prevHandLocal;
    Vector3 _grabNeutralLocal;
    float _stillTimer;
    float _currentPitch;

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
        _stillTimer = 0f;

        Transform pivot = yawPivot != null ? yawPivot : xrOrigin;
        Transform reference = motionReference != null ? motionReference : pivot;
        if (reference == null)
        {
            _hasPrevHandLocal = false;
            return;
        }

        Vector3 handNow = GetInteractorWorldPos(_activeInteractor);
        _prevHandLocal = reference.InverseTransformPoint(handNow);
        _grabNeutralLocal = _prevHandLocal;
        _hasPrevHandLocal = true;
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        _isControlling = false;
        _activeInteractor = null;
        _hasPrevHandLocal = false;
        _stillTimer = 0f;
        LastLateralAnalog = 0f;
        LastForwardAnalog = 0f;
    }

    void Update()
    {
        if (!_isControlling || _activeInteractor == null) return;

        Transform pivot = yawPivot != null ? yawPivot : xrOrigin;
        if (pivot == null) return;

        Transform reference = motionReference != null ? motionReference : pivot;

        Vector3 forward = GetPlayerForwardAxis();
        if (forward.sqrMagnitude < 0.0001f) return;

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        else
            right.Normalize();

        Vector3 handNow = GetInteractorWorldPos(_activeInteractor);

        if (!_hasPrevHandLocal)
        {
            _prevHandLocal = reference.InverseTransformPoint(handNow);
            _grabNeutralLocal = _prevHandLocal;
            _hasPrevHandLocal = true;
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float gMul = GetGravitySpeedMultiplier();

        float rx;
        float fx;

        if (usePositionBasedTurn)
        {
            Vector3 nowLocal = reference.InverseTransformPoint(handNow);
            Vector3 dispLocal = nowLocal - _grabNeutralLocal;
            Vector3 dispWorld = reference.TransformVector(dispLocal);

            // 水平偏移（左右 → yaw）
            Vector3 dispH = dispWorld;
            if (flattenToHorizontal)
                dispH.y = 0f;

            float alongR = Vector3.Dot(dispH, right);
            float absOffR = Mathf.Abs(alongR);

            if (absOffR <= positionDeadzoneMeters)
            {
                rx = 0f;
            }
            else
            {
                float sign = Mathf.Sign(alongR);
                float over = absOffR - positionDeadzoneMeters;
                float denom = Mathf.Max(0.0001f, maxOffsetForFullSpeed - positionDeadzoneMeters);
                float t = Mathf.Clamp01(over / denom);
                rx = sign * t;
            }

            // 前后偏移（前/后 → pitch）
            float alongF = -Vector3.Dot(dispH, forward);
            float absOffF = Mathf.Abs(alongF);

            if (absOffF <= positionDeadzoneMeters)
            {
                fx = 0f;
            }
            else
            {
                float sign = Mathf.Sign(alongF);
                float over = absOffF - positionDeadzoneMeters;
                float denom = Mathf.Max(0.0001f, maxOffsetForFullSpeed - positionDeadzoneMeters);
                float t = Mathf.Clamp01(over / denom);
                fx = sign * t;
            }
        }
        else
        {
            Vector3 nowLocal = reference.InverseTransformPoint(handNow);
            Vector3 deltaLocal = nowLocal - _prevHandLocal;
            Vector3 handDelta = reference.TransformVector(deltaLocal);
            if (flattenToHorizontal)
                handDelta.y = 0f;

            if (handDelta.sqrMagnitude <= stillDeltaMeters * stillDeltaMeters)
                _stillTimer += dt;
            else
                _stillTimer = 0f;

            rx = 0f;
            fx = 0f;
            if (_stillTimer < stillStopTime)
            {
                float velR = Vector3.Dot(handDelta, right) / dt;
                if (velR > handSpeedDeadzoneLR) rx = 1f;
                else if (velR < -handSpeedDeadzoneLR) rx = -1f;

                float velF = Vector3.Dot(handDelta, forward) / dt;
                if (velF > handSpeedDeadzoneLR) fx = 1f;
                else if (velF < -handSpeedDeadzoneLR) fx = -1f;
            }
        }

        LastLateralAnalog = rx;
        LastForwardAnalog = fx;

        // ── Yaw（水平转向）──
        float yawDelta = rx * yawDegreesPerSecond * gMul * dt;
        pivot.Rotate(0f, yawDelta, 0f, Space.World);

        // ── Pitch（俯仰）── 向后拉 = 抬头（pitch 减小），向前推 = 低头（pitch 增大）
        float pitchDelta = -fx * pitchDegreesPerSecond * gMul * dt;
        _currentPitch = Mathf.Clamp(_currentPitch + pitchDelta, -maxPitchUp, maxPitchDown);

        // 将 pitch 应用到 yawPivot/xrOrigin 的本地 X 旋转（旋转整个机甲框架）
        // 不能旋转 xrCamera，因为 VR 头显追踪会每帧覆盖其旋转
        {
            Vector3 pivotEuler = pivot.localEulerAngles;
            pivotEuler.x = _currentPitch;
            pivot.localEulerAngles = pivotEuler;
        }

        handNow = GetInteractorWorldPos(_activeInteractor);
        _prevHandLocal = reference.InverseTransformPoint(handNow);
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