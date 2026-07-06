using TMPro;
using UnityEngine;

public class DiscThrowCountUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI throwCountText;

    [Header("Text")]
    [SerializeField] private string limitedFormat = "남은 투척: {0} / {1}";
    [SerializeField] private string unlimitedFormat = "던진 횟수: {0} / 제한 없음";

    public void Refresh(int usedThrows, int maxThrows)
    {
        if (throwCountText == null)
            return;

        if (maxThrows <= 0)
        {
            throwCountText.text = string.Format(
                unlimitedFormat,
                Mathf.Max(0, usedThrows)
            );

            return;
        }

        int remaining = Mathf.Clamp(
            maxThrows - usedThrows,
            0,
            maxThrows
        );

        throwCountText.text = string.Format(
            limitedFormat,
            remaining,
            maxThrows
        );
    }

    public void RefreshRemainingOnly(int remainingThrows)
    {
        if (throwCountText == null)
            return;

        if (remainingThrows < 0)
        {
            throwCountText.text = "투척 제한 없음";
            return;
        }

        throwCountText.text = $"남은 투척: {remainingThrows}";
    }

    public void Clear()
    {
        if (throwCountText != null)
            throwCountText.text = string.Empty;
    }
}