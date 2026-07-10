using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DiscDurabilityGaugeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DiscDurability durability;
    [SerializeField] private Slider durabilitySlider;
    [SerializeField] private TextMeshProUGUI durabilityTitleText;
    [SerializeField] private TextMeshProUGUI durabilityValueText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Text")]
    [SerializeField] private string titleText = "내구도";
    [SerializeField] private string valueFormat = "{0:0} / {1:0}";

    [Header("Gauge")]
    [SerializeField] private bool smoothGauge = true;
    [SerializeField] private float smoothSpeed = 12f;

    [Header("Input Blocking")]
    [Tooltip("true면 이 UI가 터치/클릭 입력을 막지 않습니다.")]
    [SerializeField] private bool ignorePointerInput = true;

    private float targetNormalized = 1f;
    private float displayedNormalized = 1f;

    private float currentValue;
    private float maxValue = 1f;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null && ignorePointerInput)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        Subscribe();

        if (durability != null)
            Refresh(durability.CurrentDurability, durability.MaxDurability);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (durabilitySlider == null)
            return;

        if (!smoothGauge)
        {
            displayedNormalized = targetNormalized;
        }
        else
        {
            float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);

            displayedNormalized = Mathf.Lerp(
                displayedNormalized,
                targetNormalized,
                t
            );
        }

        durabilitySlider.value = displayedNormalized;
    }

    private void Subscribe()
    {
        if (durability == null)
            return;

        durability.DurabilityChanged -= Refresh;
        durability.DurabilityChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (durability == null)
            return;

        durability.DurabilityChanged -= Refresh;
    }

    public void Refresh(float current, float max)
    {
        maxValue = Mathf.Max(1f, max);
        currentValue = Mathf.Clamp(current, 0f, maxValue);

        targetNormalized = Mathf.Clamp01(currentValue / maxValue);

        if (!smoothGauge)
            displayedNormalized = targetNormalized;

        if (durabilitySlider != null)
        {
            durabilitySlider.minValue = 0f;
            durabilitySlider.maxValue = 1f;

            if (!smoothGauge)
                durabilitySlider.value = targetNormalized;
        }

        if (durabilityTitleText != null)
            durabilityTitleText.text = titleText;

        if (durabilityValueText != null)
        {
            durabilityValueText.text = string.Format(
                valueFormat,
                currentValue,
                maxValue
            );
        }
    }

    public void SetDurability(DiscDurability newDurability)
    {
        Unsubscribe();

        durability = newDurability;

        Subscribe();

        if (durability != null)
            Refresh(durability.CurrentDurability, durability.MaxDurability);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}