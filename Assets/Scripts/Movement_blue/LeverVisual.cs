using UnityEngine;

public class LeverVisual : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] CockpitThrottle throttle;
    [SerializeField] CockpitSimpleTurn simpleTurn;

    [SerializeField] Transform visual;

    [Header("Left Right")]
    [SerializeField] Vector3 lateralRotationAxis = new Vector3(0f, 0f, 1f);
    [SerializeField] float maxLateralTiltDegrees = 18f;
    [SerializeField] bool invertLateral = false;

    [Header("Front Back")]
    [SerializeField] Vector3 forwardRotationAxis = new Vector3(1f, 0f, 0f);
    [SerializeField] float maxForwardTiltDegrees = 18f;
    [SerializeField] bool invertForward = false;

    [Header("Smoothing")]
    [SerializeField] float smoothTime = 0.1f;
    [SerializeField] bool separateSmoothTimes = false;
    [SerializeField] float lateralSmoothTime = 0.1f;
    [SerializeField] float forwardSmoothTime = 0.1f;

    Quaternion _baseLocalRot;
    float _smoothLat;
    float _smoothFwd;
    float _latVel;
    float _fwdVel;

    void Awake()
    {
        if (visual == null)
            visual = transform;
        _baseLocalRot = visual.localRotation;

        if (throttle == null)
            throttle = GetComponentInParent<CockpitThrottle>();
        if (simpleTurn == null)
            simpleTurn = GetComponentInParent<CockpitSimpleTurn>();
    }

    void LateUpdate()
    {
        if (visual == null) return;

        float lat;
        float fwd;

        if (throttle != null)
        {
            lat = throttle.LastLateralAnalog;
            fwd = throttle.LastForwardAnalog;
        }
        else if (simpleTurn != null)
        {
            lat = simpleTurn.LastLateralAnalog;
            fwd = simpleTurn.LastForwardAnalog;
        }
        else
            return;

        if (invertLateral) lat = -lat;
        if (invertForward) fwd = -fwd;

        lat = Mathf.Clamp(lat, -1f, 1f);
        fwd = Mathf.Clamp(fwd, -1f, 1f);

        float tLat = separateSmoothTimes ? lateralSmoothTime : smoothTime;
        float tFwd = separateSmoothTimes ? forwardSmoothTime : smoothTime;

        _smoothLat = Mathf.SmoothDamp(_smoothLat, lat, ref _latVel, Mathf.Max(0.01f, tLat));
        _smoothFwd = Mathf.SmoothDamp(_smoothFwd, fwd, ref _fwdVel, Mathf.Max(0.01f, tFwd));

        float angleLat = _smoothLat * maxLateralTiltDegrees;
        float angleFwd = _smoothFwd * maxForwardTiltDegrees;

        Quaternion qLat = Quaternion.AngleAxis(angleLat, lateralRotationAxis.normalized);
        Quaternion qFwd = Quaternion.AngleAxis(angleFwd, forwardRotationAxis.normalized);

        visual.localRotation = _baseLocalRot * qFwd * qLat;
    }
}