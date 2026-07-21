using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelProgressUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RunProgressTracker progressTracker;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TextMeshProUGUI distanceText;

    [Header("Text")]
    [SerializeField] private string distanceFormat = "{0:0}m";

    private void Awake()
    {
        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.interactable = false;
        }
    }

    private void Update()
    {
        if (progressTracker == null)
            return;

        if (progressSlider != null)
        {
            progressSlider.value =
                progressTracker.LevelProgress01;
        }

        if (distanceText != null)
        {
            distanceText.text = string.Format(
                distanceFormat,
                progressTracker.TotalDistance
            );
        }
    }
}
