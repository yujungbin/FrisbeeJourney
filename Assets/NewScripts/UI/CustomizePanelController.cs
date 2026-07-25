using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CustomizePanelController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private DiscCosmeticDatabase cosmeticDatabase;

    [Header("Panel")]
    [SerializeField] private GameObject customizePanelRoot;
    [SerializeField] private GameObject startUIRoot;

    [Tooltip("커스터마이징 패널이 열려 있는 동안 비활성화할 전체 화면 시작 버튼입니다.")]
    [SerializeField] private GameObject startTouchButtonObject;

    [Header("List")]
    [SerializeField] private Transform itemContent;
    [SerializeField] private DiscCosmeticItemUI itemPrefab;

    [Header("Selected Cosmetic UI")]
    [SerializeField] private TMP_Text selectedNameText;
    [SerializeField] private TMP_Text selectedDescriptionText;
    [SerializeField] private Image selectedThumbnailImage;

    [Header("Buttons")]
    [SerializeField] private Button equipButton;
    [SerializeField] private TMP_Text equipButtonText;
    [SerializeField] private Button closeButton;

    [Header("3D Preview")]
    [SerializeField] private DiscAppearanceApplier previewAppearance;

    private readonly List<DiscCosmeticItemUI> createdItems =
        new List<DiscCosmeticItemUI>();

    private DiscCosmeticData selectedCosmetic;
    private bool listCreated;

    private void Awake()
    {
        if (customizePanelRoot != null)
        {
            customizePanelRoot.SetActive(false);
        }

        if (equipButton != null)
        {
            equipButton.onClick.AddListener(EquipSelectedCosmetic);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
    }

    private void OnDestroy()
    {
        if (equipButton != null)
        {
            equipButton.onClick.RemoveListener(EquipSelectedCosmetic);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
        }
    }

    public void OpenPanel()
    {
        if (cosmeticDatabase == null)
        {
            Debug.LogError(
                "CustomizePanelController: " +
                "DiscCosmeticDatabase가 연결되지 않았습니다."
            );

            return;
        }

        if (customizePanelRoot == null)
        {
            Debug.LogError(
                "CustomizePanelController: " +
                "Customize Panel Root가 연결되지 않았습니다."
            );

            return;
        }

        // 시작 화면을 먼저 끄고 꾸미기 화면을 먼저 켭니다.
        if (startUIRoot != null)
        {
            startUIRoot.SetActive(false);
        }

        customizePanelRoot.SetActive(true);

        // 패널이 열린 뒤 목록을 한 번만 생성합니다.
        CreateItemListIfNeeded();

        DiscCosmeticData equippedCosmetic =
            cosmeticDatabase.GetEquippedCosmetic();

        SelectCosmetic(equippedCosmetic);
    }

    public void ClosePanel()
    {
        // 장착하지 않은 미리보기 선택을 실제 장착 외형으로 되돌립니다.
        if (previewAppearance != null)
        {
            previewAppearance.ApplyEquippedCosmetic();
        }

        if (customizePanelRoot != null)
        {
            customizePanelRoot.SetActive(false);
        }

        if (startUIRoot != null)
        {
            startUIRoot.SetActive(true);
        }
    }

    private void CreateItemListIfNeeded()
    {
        if (listCreated)
        {
            return;
        }

        if (itemContent == null)
        {
            Debug.LogError(
                "CustomizePanelController: Item Content가 연결되지 않았습니다."
            );

            return;
        }

        if (itemPrefab == null)
        {
            Debug.LogError(
                "CustomizePanelController: Item Prefab이 연결되지 않았습니다."
            );

            return;
        }

        IReadOnlyList<DiscCosmeticData> cosmetics =
            cosmeticDatabase.Cosmetics;

        for (int i = 0; i < cosmetics.Count; i++)
        {
            DiscCosmeticData cosmetic = cosmetics[i];

            if (cosmetic == null)
            {
                continue;
            }

            DiscCosmeticItemUI item =
                Instantiate(itemPrefab, itemContent);

            item.Initialize(
                cosmetic,
                SelectCosmetic
            );

            createdItems.Add(item);
        }

        listCreated = true;
    }

    private void SelectCosmetic(DiscCosmeticData cosmetic)
    {
        if (cosmetic == null)
        {
            Debug.LogWarning(
                "CustomizePanelController: 선택할 원반 데이터가 없습니다."
            );

            return;
        }

        selectedCosmetic = cosmetic;

        if (previewAppearance != null)
        {
            previewAppearance.ApplyCosmetic(selectedCosmetic);
        }

        RefreshSelectedInformation();
        RefreshItemStates();
    }

    private void EquipSelectedCosmetic()
    {
        if (selectedCosmetic == null)
        {
            return;
        }

        bool equipped =
            DiscCosmeticSave.TryEquip(selectedCosmetic);

        if (!equipped)
        {
            Debug.LogWarning(
                $"장착할 수 없는 원반입니다: " +
                $"{selectedCosmetic.DisplayName}"
            );

            RefreshSelectedInformation();
            RefreshItemStates();

            return;
        }

        if (previewAppearance != null)
        {
            previewAppearance.ApplyEquippedCosmetic();
        }

        RefreshSelectedInformation();
        RefreshItemStates();

        Debug.Log(
            $"원반 장착 완료: {selectedCosmetic.DisplayName}"
        );
    }

    private void RefreshSelectedInformation()
    {
        if (selectedCosmetic == null)
        {
            return;
        }

        if (selectedNameText != null)
        {
            selectedNameText.text =
                selectedCosmetic.DisplayName;
        }

        if (selectedDescriptionText != null)
        {
            selectedDescriptionText.text =
                selectedCosmetic.Description;
        }

        if (selectedThumbnailImage != null)
        {
            selectedThumbnailImage.sprite =
                selectedCosmetic.Thumbnail;

            selectedThumbnailImage.enabled =
                selectedCosmetic.Thumbnail != null;

            selectedThumbnailImage.preserveAspect = true;
        }

        bool isOwned =
            DiscCosmeticSave.IsOwned(selectedCosmetic);

        bool isEquipped =
            IsEquipped(selectedCosmetic);

        if (equipButton != null)
        {
            equipButton.interactable =
                isOwned && !isEquipped;
        }

        if (equipButtonText == null)
        {
            return;
        }

        if (!isOwned)
        {
            equipButtonText.text = "미보유";
        }
        else if (isEquipped)
        {
            equipButtonText.text = "장착중";
        }
        else
        {
            equipButtonText.text = "장착";
        }
    }

    private void RefreshItemStates()
    {
        for (int i = 0; i < createdItems.Count; i++)
        {
            DiscCosmeticItemUI item = createdItems[i];

            if (item == null)
            {
                continue;
            }

            DiscCosmeticData cosmetic =
                item.CosmeticData;

            if (cosmetic == null)
            {
                continue;
            }

            bool isSelected =
                selectedCosmetic != null &&
                cosmetic.CosmeticId ==
                selectedCosmetic.CosmeticId;

            bool isEquipped =
                IsEquipped(cosmetic);

            bool isOwned =
                DiscCosmeticSave.IsOwned(cosmetic);

            item.RefreshState(
                isSelected,
                isEquipped,
                isOwned
            );
        }
    }

    private bool IsEquipped(DiscCosmeticData cosmetic)
    {
        if (cosmetic == null ||
            cosmeticDatabase == null)
        {
            return false;
        }

        string fallbackId =
            cosmeticDatabase.DefaultCosmetic != null
                ? cosmeticDatabase.DefaultCosmetic.CosmeticId
                : string.Empty;

        string equippedId =
            DiscCosmeticSave.GetEquippedCosmeticId(
                fallbackId
            );

        return cosmetic.CosmeticId == equippedId;
    }
}