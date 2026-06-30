using TMPro;
using UnityEngine;

public class DiscThrowCountUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI throwCountText;

    [Header("Format")]
    [SerializeField] private string format = "≥≤¿∫ ≈ı√¥: {0} / {1}";

    [Header("Options")]
    [SerializeField] private bool hideWhenNoText = false;

    public void Refresh(int usedThrows, int maxThrows)
    {
        int safeMaxThrows = Mathf.Max(1, maxThrows);
        int remainingThrows = Mathf.Clamp(
            safeMaxThrows - usedThrows,
            0,
            safeMaxThrows
        );

        SetText(remainingThrows, safeMaxThrows);
    }

    public void RefreshRemainingOnly(int remainingThrows)
    {
        int safeRemaining = Mathf.Max(0, remainingThrows);

        if (throwCountText != null)
        {
            throwCountText.text = $"≥≤¿∫ ≈ı√¥: {safeRemaining}";
            throwCountText.gameObject.SetActive(
                !hideWhenNoText || !string.IsNullOrEmpty(throwCountText.text)
            );
        }
    }

    public void Clear()
    {
        if (throwCountText == null)
            return;

        throwCountText.text = string.Empty;

        if (hideWhenNoText)
            throwCountText.gameObject.SetActive(false);
    }

    private void SetText(int remainingThrows, int maxThrows)
    {
        if (throwCountText == null)
            return;

        throwCountText.text = string.Format(
            format,
            remainingThrows,
            maxThrows
        );

        throwCountText.gameObject.SetActive(
            !hideWhenNoText || !string.IsNullOrEmpty(throwCountText.text)
        );
    }
}
