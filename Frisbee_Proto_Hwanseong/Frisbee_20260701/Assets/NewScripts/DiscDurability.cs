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
    [SerializeField] private DurabilityChangedEvent onDurabilityChanged;
    [SerializeField] private UnityEvent onBroken;

    public float MaxDurability => maxDurability;
    public float CurrentDurability => currentDurability;
    public bool IsBroken => currentDurability <= 0f;

    public void Initialize(float newMaxDurability)
    {
        maxDurability = Mathf.Max(1f, newMaxDurability);
        currentDurability = maxDurability;

        onDurabilityChanged?.Invoke(currentDurability, maxDurability);
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

        onDurabilityChanged?.Invoke(currentDurability, maxDurability);

        if (IsBroken)
            onBroken?.Invoke();
    }

    public void RepairToFull()
    {
        currentDurability = maxDurability;
        onDurabilityChanged?.Invoke(currentDurability, maxDurability);
    }
}
