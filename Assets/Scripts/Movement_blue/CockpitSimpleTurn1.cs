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

        if (usePositionBasedTurn)
        {
            Vector3 nowLocal = reference.InverseTransformPoint(handNow);
            Vector3 dispLocal = nowLocal - _grabNeutralLocal;
            Vector3 dispWorld = reference.TransformVector(dispLocal);
            if (flattenToHorizontal)
                dispWorld.y = 0f;

            float alongR = Vector3.Dot(dispWorld, right);
            float absOff = Mathf.Abs(alongR);

            if (absOff <= positionDeadzoneMeters)
            {
                rx = 0f;
            }
            else
            {
                float sign = Mathf.Sign(alongR);
                float over = absOff - positionDeadzoneMeters;
                float denom = Mathf.Max(0.0001f, maxOffsetForFullSpeed - positionDeadzoneMeters);
                float t = Mathf.Clamp01(over / denom);
                rx = sign * t;
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
            if (_stillTimer < stillStopTime)
            {
                float velR = Vector3.Dot(handDelta, right) / dt;
                if (velR > handSpeedDeadzoneLR) rx = 1f;
                else if (velR < -handSpeedDeadzoneLR) rx = -1f;
            }
        }

        LastLateralAnalog = rx;
        LastForwardAnalog = 0f;

        float yawDelta = rx * yawDegreesPerSecond * gMul * dt;
        pivot.Rotate(0f, yawDelta, 0f, Space.World);

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