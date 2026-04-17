using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRSimpleInteractable))]
public class CockpitSimpleTurn : MonoBehaviour{
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
    // [SerializeField] float positionDeadzoneMeters = 0.02f; //for hand position based turn
    // [SerializeField] float maxOffsetForFullSpeed = 0.12f; //for hand position based turn
    // // true: Keeps spinning as long as hand remains on the side (depending on position)
    // [SerializeField] bool usePositionBasedTurn = true;

    [Header("Pitch (俯仰)")]
    [SerializeField] float pitchDegreesPerSecond = 60f;
    [Tooltip("低头最大角度（正值，实际为负方向）")]
    [SerializeField] float maxPitchDown = 15f;
    [Tooltip("抬头最大角度")]
    [SerializeField] float maxPitchUp = 35f;

    // [Header("Velocity")]
    // [SerializeField] float handSpeedDeadzoneLR = 0.05f;
    // [SerializeField] float stillDeltaMeters = 0.0015f;
    // [SerializeField] float stillStopTime = 0.06f;

    [Header("Rotation")]
    [SerializeField] float rotationDeadzoneDeg = 4f;
    [SerializeField] float maxRotationForFullSpeedDeg = 25f;

    [Header("Interaction")]
    [SerializeField] bool requireDirectInteractorOnly = true;

    [Header("Gravity Effect")]
    [SerializeField] bool useGameManagerGravity = false;
    [SerializeField] float gravityNeutral = 1f;
    [SerializeField] float minSpeedMultiplier = 0.4f;
    [SerializeField] float maxSpeedMultiplier = 1.8f;

    [Header("Turn Rate")]
    [SerializeField] float timeToMaxTurnRate = 0.30f;   // 到最大轉速時間
    [SerializeField] float timeToStopTurn = 0.20f;      // 放手停下時間
    [SerializeField] float inputSmooth = 8f;            // 類比輸入平滑強度

    public float LastLateralAnalog { get; private set; }
    public float LastForwardAnalog { get; private set; }

    IXRSelectInteractor _activeInteractor;
    bool _isControlling;
    bool _hasPrevHandLocal;
    // Vector3 _prevHandLocal;
    // Vector3 _grabNeutralLocal;
    // float _stillTimer;
    float _currentPitch;
    // Rotation variables
    Quaternion _prevHandLocalRot;
    Quaternion _grabNeutralLocalRot;

    float _yawRate;        // deg/s
    float _pitchRate;      // deg/s
    float _rxSmoothed;
    float _fxSmoothed;

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
        // _stillTimer = 0f;

        Transform pivot = yawPivot != null ? yawPivot : xrOrigin;
        Transform reference = motionReference != null ? motionReference : pivot;
        if (reference == null)
        {
            _hasPrevHandLocal = false;
            return;
        }

        // Vector3 handNow = GetInteractorWorldPos(_activeInteractor);
        // _prevHandLocal = reference.InverseTransformPoint(handNow);
        // _grabNeutralLocal = _prevHandLocal;
        _hasPrevHandLocal = true;

        Quaternion handRotNow = GetInteractorWorldRot(_activeInteractor);
        _prevHandLocalRot = Quaternion.Inverse(reference.rotation) * handRotNow;
        _grabNeutralLocalRot = _prevHandLocalRot;
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        _isControlling = false;
        _activeInteractor = null;
        _hasPrevHandLocal = false;
        // _stillTimer = 0f;
        LastLateralAnalog = 0f;
        LastForwardAnalog = 0f;

        _yawRate = 0f;
        _pitchRate = 0f;
        _rxSmoothed = 0f;
        _fxSmoothed = 0f;
    }

   void Update()
    {
        if (!_isControlling || _activeInteractor == null) return;

        Transform pivot = yawPivot != null ? yawPivot : xrOrigin;
        if (pivot == null) return;

        Transform reference = motionReference != null ? motionReference : pivot;

        if (!_hasPrevHandLocal)
        {
            Quaternion handRotInit = GetInteractorWorldRot(_activeInteractor);
            _prevHandLocalRot = Quaternion.Inverse(reference.rotation) * handRotInit;
            _grabNeutralLocalRot = _prevHandLocalRot;
            _hasPrevHandLocal = true;
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float gMul = GetGravitySpeedMultiplier();

        Quaternion handRotNow = GetInteractorWorldRot(_activeInteractor);
        Quaternion nowLocalRot = Quaternion.Inverse(reference.rotation) * handRotNow;

        Quaternion deltaRot = Quaternion.Inverse(_grabNeutralLocalRot) * nowLocalRot;
        Vector3 e = deltaRot.eulerAngles;

        float yawAngle = -Mathf.DeltaAngle(0f, e.z);
        float pitchAngle = Mathf.DeltaAngle(0f, e.x);

        float rx = AngleToAnalog(yawAngle, rotationDeadzoneDeg, maxRotationForFullSpeedDeg);
        float fx = AngleToAnalog(-pitchAngle, rotationDeadzoneDeg, maxRotationForFullSpeedDeg);

        LastLateralAnalog = rx;
        LastForwardAnalog = fx;

        // float yawDelta = rx * yawDegreesPerSecond * gMul * dt;
        // pivot.Rotate(0f, yawDelta, 0f, Space.World);
        
        // smooth input
        float a = 1f - Mathf.Exp(-inputSmooth * dt);
        _rxSmoothed = Mathf.Lerp(_rxSmoothed, rx, a);
        _fxSmoothed = Mathf.Lerp(_fxSmoothed, fx, a);

        // target angle rate
        float targetYawRate = _rxSmoothed * yawDegreesPerSecond * gMul;
        float targetPitchRate = -_fxSmoothed * pitchDegreesPerSecond * gMul;

        //speed ramp
        float yawAccel = (yawDegreesPerSecond * gMul) / Mathf.Max(0.01f, timeToMaxTurnRate);
        float yawDecel = (yawDegreesPerSecond * gMul) / Mathf.Max(0.01f, timeToStopTurn);
        float pitchAccel = (pitchDegreesPerSecond * gMul) / Mathf.Max(0.01f, timeToMaxTurnRate);
        float pitchDecel = (pitchDegreesPerSecond * gMul) / Mathf.Max(0.01f, timeToStopTurn);

        _yawRate = Mathf.MoveTowards(_yawRate, targetYawRate, (_yawRate * targetYawRate >= 0f && Mathf.Abs(targetYawRate) > Mathf.Abs(_yawRate) ? yawAccel : yawDecel) * dt);
        _pitchRate = Mathf.MoveTowards(_pitchRate, targetPitchRate, (_pitchRate * targetPitchRate >= 0f && Mathf.Abs(targetPitchRate) > Mathf.Abs(_pitchRate) ? pitchAccel : pitchDecel) * dt);

        // apply rotation
        pivot.Rotate(0f, _yawRate * dt, 0f, Space.World);
        _currentPitch = Mathf.Clamp(_currentPitch + _pitchRate * dt, -maxPitchUp, maxPitchDown);   


        float pitchDelta = -fx * pitchDegreesPerSecond * gMul * dt;
        _currentPitch = Mathf.Clamp(_currentPitch + pitchDelta, -maxPitchUp, maxPitchDown);

        Vector3 pivotEuler = pivot.localEulerAngles;
        pivotEuler.x = _currentPitch;
        pivot.localEulerAngles = pivotEuler;

        _prevHandLocalRot = nowLocalRot;
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

    // static Vector3 GetInteractorWorldPos(IXRSelectInteractor interactor)
    // {
    //     Transform t = interactor.GetAttachTransform(null);
    //     return t != null ? t.position : Vector3.zero;
    // }

    static Quaternion GetInteractorWorldRot(IXRSelectInteractor interactor)
    {
        Transform t = interactor.GetAttachTransform(null);
        return t != null ? t.rotation : Quaternion.identity;
    }

    static float AngleToAnalog(float angleDeg, float deadzoneDeg, float fullSpeedDeg)
    {
        float abs = Mathf.Abs(angleDeg);
        if (abs <= deadzoneDeg) return 0f;

        float over = abs - deadzoneDeg;
        float denom = Mathf.Max(0.0001f, fullSpeedDeg - deadzoneDeg);
        float t = Mathf.Clamp01(over / denom);
        return Mathf.Sign(angleDeg) * t;
    }
}
