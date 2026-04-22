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
    [SerializeField] Transform motionReference;

    [Header("Movement")]
    [SerializeField] float constantSpeed = 6.0f;
    [SerializeField] bool useHeadForward = true;
    [SerializeField] bool flattenToHorizontal = true;

    [Header("Thrust ramp (噴射漸推)")]
    [SerializeField] float timeToMaxSpeed = 1.0f;
    [SerializeField] float timeToStop = 0.5f;

    [Header("Input mode")]
    [SerializeField] bool usePositionBasedMovement = true;
    [SerializeField] float positionDeadzoneMeters = 0.02f;
    [SerializeField] float maxOffsetForFullSpeed = 0.12f;

    [Header("Velocity")]
    [SerializeField] float handSpeedDeadzoneFB = 0.05f;
    [SerializeField] float handSpeedDeadzoneLR = 0.05f;
    [SerializeField] float stillDeltaMeters = 0.0015f;
    [SerializeField] float stillStopTime = 0.06f;

    [Header("Interaction")]
    [SerializeField] bool requireDirectInteractorOnly = true;

    [Header("Gravity Effect")]
    [SerializeField] bool useGameManagerGravity = true;
    [SerializeField] float gravityNeutral = 1f;
    [SerializeField] float minSpeedMultiplier = 0.4f;
    [SerializeField] float maxSpeedMultiplier = 1.8f;

    [Header("Vertical")]
    [SerializeField] bool allowVerticalMovement = true;
    [SerializeField] float positionDeadzoneVerticalMeters = 0.02f;
    [SerializeField] float maxOffsetVerticalForFullSpeed = 0.12f;

    private bool _wasMovingLastFrame = false;

    public float LastLateralAnalog { get; private set; }
    public float LastForwardAnalog { get; private set; }

    IXRSelectInteractor _activeInteractor;
    bool _isControlling;
    bool _hasPrevHandLocal;
    Vector3 _prevHandLocal;
    Vector3 _grabNeutralLocal;
    float _stillTimer;
    float _thrustSpeed;

    float GetGravitySpeedMultiplier()
    {
        if (!useGameManagerGravity) return 1f;
        if (GameManager.Instance == null) return 1f;
        float g = Mathf.Max(0.01f, GameManager.Instance.GetCurrentGravity());
        float raw = gravityNeutral / g;
        return Mathf.Clamp(raw, minSpeedMultiplier, maxSpeedMultiplier);
    }

    static float AxisAnalogFromOffset(float along, float deadzone, float maxFull)
    {
        float absOff = Mathf.Abs(along);
        if (absOff <= deadzone)
            return 0f;
        float sign = Mathf.Sign(along);
        float over = absOff - deadzone;
        float denom = Mathf.Max(0.0001f, maxFull - deadzone);
        float t = Mathf.Clamp01(over / denom);
        return sign * t;
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

        //play SFX
        Transform audioTarget = mechaRoot != null ? mechaRoot : transform;
        AudioManager.Instance?.StopLoopOnTarget(audioTarget);
        _wasMovingLastFrame = false;
    
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (requireDirectInteractorOnly && args.interactorObject is not XRDirectInteractor)
            return;

        _activeInteractor = args.interactorObject;
        _isControlling = true;
        _stillTimer = 0f;

        Transform reference = motionReference != null ? motionReference : mechaRoot;
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
        _thrustSpeed = 0f;
        LastLateralAnalog = 0f;
        LastForwardAnalog = 0f;
     
        
        Transform audioTarget = mechaRoot != null ? mechaRoot : transform;
        AudioManager.Instance?.StopLoopOnTarget(audioTarget);
        _wasMovingLastFrame = false;
    }

    void Update()
    {
        if (!_isControlling || _activeInteractor == null) return;
        if (mechaRoot == null) return;

        Transform reference = motionReference != null ? motionReference : mechaRoot;

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

        Vector3 moveDir = Vector3.zero;
        float lateralRx = 0f;
        float forwardFx = 0f;

        if (usePositionBasedMovement)
        {
            Vector3 nowLocal = reference.InverseTransformPoint(handNow);
            Vector3 dispLocal = nowLocal - _grabNeutralLocal;

            Vector3 dispWorld = reference.TransformVector(dispLocal);
            Vector3 dispH = dispWorld;
            if (flattenToHorizontal)
                dispH.y = 0f;
            float alongF = Vector3.Dot(dispH, forward);
            float alongR = Vector3.Dot(dispH, right);
            float alongY = allowVerticalMovement ? dispWorld.y : 0f;

            float fx = AxisAnalogFromOffset(alongF, positionDeadzoneMeters, maxOffsetForFullSpeed);
            float rx = AxisAnalogFromOffset(alongR, positionDeadzoneMeters, maxOffsetForFullSpeed);
            float uy = allowVerticalMovement
                ? AxisAnalogFromOffset(alongY, positionDeadzoneVerticalMeters, maxOffsetVerticalForFullSpeed)
                : 0f;

            moveDir = forward * fx + right * rx + Vector3.up * uy;
            if (moveDir.sqrMagnitude > 0.0001f)
                moveDir.Normalize();

            lateralRx = rx;
            forwardFx = fx;
        }
        else
        {
            Vector3 nowLocal = reference.InverseTransformPoint(handNow);
            Vector3 deltaLocal = nowLocal - _prevHandLocal;
            Vector3 handDelta = reference.TransformVector(deltaLocal);

            Vector3 handH = handDelta;
            if (flattenToHorizontal)
                handH.y = 0f;

            float velF = Vector3.Dot(handH, forward) / dt;
            float velR = Vector3.Dot(handH, right) / dt;
            float velY = allowVerticalMovement ? handDelta.y / dt : 0f;

            if (handDelta.sqrMagnitude <= stillDeltaMeters * stillDeltaMeters)
                _stillTimer += dt;
            else
                _stillTimer = 0f;

            moveDir = Vector3.zero;

            if (_stillTimer < stillStopTime)
            {
                float af = Mathf.Abs(velF);
                float ar = Mathf.Abs(velR);
                float ay = Mathf.Abs(velY);

                float fx = 0f, rx = 0f, uy = 0f;

                if (allowVerticalMovement && ay >= af && ay >= ar && ay > 0.0001f)
                {
                    if (velY > handSpeedDeadzoneFB) uy = 1f;
                    else if (velY < -handSpeedDeadzoneFB) uy = -1f;
                }
                else if (af >= ar && af >= ay)
                {
                    if (velF > handSpeedDeadzoneFB) fx = 1f;
                    else if (velF < -handSpeedDeadzoneFB) fx = -1f;

                    if (fx == 0f)
                    {
                        if (velR > handSpeedDeadzoneLR) rx = 1f;
                        else if (velR < -handSpeedDeadzoneLR) rx = -1f;
                    }
                }
                else
                {
                    if (velR > handSpeedDeadzoneLR) rx = 1f;
                    else if (velR < -handSpeedDeadzoneLR) rx = -1f;

                    if (rx == 0f)
                    {
                        if (velF > handSpeedDeadzoneFB) fx = 1f;
                        else if (velF < -handSpeedDeadzoneFB) fx = -1f;
                    }
                }

                moveDir = forward * fx + right * rx + Vector3.up * uy;
                if (moveDir.sqrMagnitude > 0.0001f)
                    moveDir.Normalize();

                lateralRx = rx;
                forwardFx = fx;
            }
        }

        LastLateralAnalog = lateralRx;
        LastForwardAnalog = forwardFx;

        float maxSpeed = constantSpeed * gMul;
        float targetSpeed = moveDir.sqrMagnitude > 0.0001f ? maxSpeed : 0f;
        {
            float accelRate = maxSpeed / Mathf.Max(0.01f, timeToMaxSpeed);
            float decelRate = maxSpeed / Mathf.Max(0.01f, timeToStop);
            if (targetSpeed > _thrustSpeed)
                _thrustSpeed = Mathf.MoveTowards(_thrustSpeed, targetSpeed, accelRate * dt);
            else
                _thrustSpeed = Mathf.MoveTowards(_thrustSpeed, targetSpeed, decelRate * dt);
        }
        Vector3 velocity = moveDir * _thrustSpeed;
        Vector3 step = velocity * dt;

        if (characterController != null)
            characterController.Move(step);
        else
            mechaRoot.position += step;


        handNow = GetInteractorWorldPos(_activeInteractor);
        _prevHandLocal = reference.InverseTransformPoint(handNow);

      
        if (moveDir.sqrMagnitude > 0.0001f)
            CockpitShake.TriggerWalk();

        //Play SFX
        // AudioManager.Instance?.StartLoopOnTarget(SfxId.PlayerWalkLoop, mechaRoot != null ? mechaRoot : transform, 0.75f);

        bool isMovingNow = moveDir.sqrMagnitude > 0.0001f;
        Transform audioTarget = mechaRoot != null ? mechaRoot : transform;
        
        if (isMovingNow)
        {
            CockpitShake.TriggerWalk();
            //only (stop to move) start loop
            if (!_wasMovingLastFrame)
                AudioManager.Instance?.StartLoopOnTarget(SfxId.PlayerWalkLoop, audioTarget, 0.75f);
        }
        else
        {
            //Only (move to stop) close loop
            if (_wasMovingLastFrame)
                AudioManager.Instance?.StopLoopOnTarget(audioTarget);
        }
        _wasMovingLastFrame = isMovingNow;
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