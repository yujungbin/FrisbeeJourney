using System;
using UnityEngine;
using UnityEngine.Events;

public class DiscDurability : MonoBehaviour
{
    [System.Serializable]
    public class DurabilityChangedEvent : UnityEvent<float, float> { }

    [Header("Runtime")]
    [SerializeField] private float maxDurability = 100f;
    [SerializeField] private float currentDurability = 100f;

    [Header("Events")]
    [SerializeField]
    private DurabilityChangedEvent onDurabilityChanged =
        new DurabilityChangedEvent();

    [SerializeField]
    private UnityEvent onBroken =
        new UnityEvent();

    public event Action<float, float> DurabilityChanged;

    public float MaxDurability => maxDurability;
    public float CurrentDurability => currentDurability;
    public bool IsBroken => currentDurability <= 0f;

    public float Normalized
    {
        get
        {
            if (maxDurability <= 0f)
                return 0f;

            return Mathf.Clamp01(currentDurability / maxDurability);
        }
    }

    private void Start()
    {
        NotifyChanged();
    }

    public void Initialize(float newMaxDurability)
    {
        maxDurability = Mathf.Max(1f, newMaxDurability);
        currentDurability = maxDurability;

        NotifyChanged();
    }

    public void ApplyDamage(float damage)
    {
        if (damage <= 0f)
            return;

        if (IsBroken)
            return;

        currentDurability = Mathf.Clamp(
            currentDurability - damage,
            0f,
            maxDurability
        );

        NotifyChanged();

        if (IsBroken)
            onBroken.Invoke();
    }

    public void RepairToFull()
    {
        currentDurability = maxDurability;
        NotifyChanged();
    }

    public void SetCurrentDurability(float value)
    {
        currentDurability = Mathf.Clamp(value, 0f, maxDurability);
        NotifyChanged();

        if (IsBroken)
            onBroken.Invoke();
    }

    private void NotifyChanged()
    {
        DurabilityChanged?.Invoke(currentDurability, maxDurability);
        onDurabilityChanged.Invoke(currentDurability, maxDurability);
    }
}
