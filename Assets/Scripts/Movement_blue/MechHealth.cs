using UnityEngine;
using System.Collections;

public class MechHealth : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int currentHp;

    [Header("Debug")]
    [SerializeField] private bool debugKillWithSpace = true;
    [SerializeField] private bool debugReviveWithF = true;

    [Header("Fall")]
    [SerializeField] private float fallAngle = 90f;
    [SerializeField] private float fallDuration = 0.5f;
    [SerializeField] private Vector3 fallAxis = Vector3.right; // 往前/後倒可改 axis
    [SerializeField] private MonoBehaviour[] disableOnDeath;   // 例如 CockpitThrottle, CockpitSimpleTurn, RTShoot

    private bool isDead = false;
    private Quaternion _aliveRotation;
    private Coroutine _poseRoutine;

    private void Start()
    {
        currentHp = maxHp;
        _aliveRotation = transform.rotation;
    }

    private void Update()
    {
        if (debugReviveWithF && Input.GetKeyDown(KeyCode.F))
        {
            Revive();
            return;
        }
        if (isDead) return;
        if (debugKillWithSpace && Input.GetKeyDown(KeyCode.Space))
        {
            currentHp = 0;
            Die();
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHp -= damage;
        if (currentHp <= 0)
        {
            currentHp = 0;
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        foreach (var comp in disableOnDeath)
        {
            if (comp != null) comp.enabled = false;
        }

      if (_poseRoutine != null) StopCoroutine(_poseRoutine);

        Quaternion target = _aliveRotation * Quaternion.Euler(0f, 0f, -90f);
        _poseRoutine = StartCoroutine(RotateToRoutine(target));
    }

    private void Revive()
    {
        currentHp = maxHp;
        isDead = false;

        foreach (var comp in disableOnDeath)
        {
            if (comp != null) comp.enabled = true;
        }

        if (_poseRoutine != null) StopCoroutine(_poseRoutine);
        _poseRoutine = StartCoroutine(RotateToRoutine(_aliveRotation));
    }

    private IEnumerator RotateToRoutine(Quaternion targetRot)
    {
        Quaternion startRot = transform.rotation;
        float t = 0f;
        while (t < fallDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fallDuration);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, p);
            yield return null;
        }
        transform.rotation = targetRot;
        _poseRoutine = null;
    }
}