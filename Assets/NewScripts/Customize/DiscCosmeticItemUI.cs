using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DiscCosmeticItemUI : MonoBehaviour
{
    [Header("Button")]
    [SerializeField] private Button selectButton;

    [Header("UI")]
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private TMP_Text discNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text stateText;

    [Header("State")]
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private GameObject equippedMark;

    private DiscCosmeticData cosmeticData;
    private Action<DiscCosmeticData> onSelected;

    public DiscCosmeticData CosmeticData => cosmeticData;

    private void Reset()
    {
        if (selectButton == null)
        {
            selectButton = GetComponent<Button>();
        }
    }

    public void Initialize(
        DiscCosmeticData data,
        Action<DiscCosmeticData> selectedCallback)
    {
        cosmeticData = data;
        onSelected = selectedCallback;

        if (selectButton == null)
        {
            selectButton = GetComponent<Button>();
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleSelected);
            selectButton.onClick.AddListener(HandleSelected);
        }

        RefreshInformation();
    }

    public void RefreshState(
        bool isSelected,
        bool isEquipped,
        bool isOwned)
    {
        if (selectedFrame != null)
        {
            selectedFrame.SetActive(isSelected);
        }

        if (equippedMark != null)
        {
            equippedMark.SetActive(isEquipped);
        }

        if (stateText == null)
        {
            return;
        }

        if (isEquipped)
        {
            stateText.text = "장착중";
        }
        else if (isOwned)
        {
            stateText.text = "보유";
        }
        else
        {
            stateText.text = "미보유";
        }
    }

    private void RefreshInformation()
    {
        if (cosmeticData == null)
        {
            if (discNameText != null)
            {
                discNameText.text = "이름 없음";
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.Empty;
            }

            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = null;
                thumbnailImage.enabled = false;
            }

            return;
        }

        if (discNameText != null)
        {
            discNameText.text = cosmeticData.DisplayName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = cosmeticData.Description;
        }

        if (thumbnailImage != null)
        {
            thumbnailImage.sprite = cosmeticData.Thumbnail;
            thumbnailImage.enabled = cosmeticData.Thumbnail != null;
            thumbnailImage.preserveAspect = true;
        }
    }

    private void HandleSelected()
    {
        if (cosmeticData == null)
        {
            return;
        }

        onSelected?.Invoke(cosmeticData);
    }

    private void OnDestroy()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleSelected);
        }
    }
}