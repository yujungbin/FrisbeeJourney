using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DiscDurabilityUI : MonoBehaviour
{
    [SerializeField] private DiscDurability durability;
    [SerializeField] private Slider durabilitySlider;
    [SerializeField] private TextMeshProUGUI durabilityText;

    private void Start()
    {
        Refresh(
            durability.CurrentDurability,
            durability.MaxDurability
        );
    }

    public void Refresh(float current, float max)
    {
        if (durabilitySlider != null)
        {
            durabilitySlider.maxValue = max;
            durabilitySlider.value = current;
        }

        if (durabilityText != null)
            durabilityText.text = $"{current:F0} / {max:F0}";
    }
}