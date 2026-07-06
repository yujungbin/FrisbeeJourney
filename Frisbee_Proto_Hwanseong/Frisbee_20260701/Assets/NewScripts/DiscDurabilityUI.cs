using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DiscDurabilityUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DiscDurability durability;
    [SerializeField] private Slider durabilitySlider;
    [SerializeField] private TextMeshProUGUI durabilityTitleText;
    [SerializeField] private TextMeshProUGUI durabilityValueText;

    [Header("Text")]
    [SerializeField] private string titleText = "³»±¸µµ";
    [SerializeField] private string valueFormat = "{0:0} / {1:0}";

    private void OnEnable()
    {
        if (durabilityTitleText != null)
            durabilityTitleText.text = titleText;

        if (durability != null)
            Refresh(durability.CurrentDurability, durability.MaxDurability);
    }

    public void Refresh(float current, float max)
    {
        float safeMax = Mathf.Max(1f, max);
        float safeCurrent = Mathf.Clamp(current, 0f, safeMax);

        if (durabilitySlider != null)
        {
            durabilitySlider.minValue = 0f;
            durabilitySlider.maxValue = safeMax;
            durabilitySlider.value = safeCurrent;
        }

        if (durabilityValueText != null)
        {
            durabilityValueText.text = string.Format(
                valueFormat,
                safeCurrent,
                safeMax
            );
        }
    }
}