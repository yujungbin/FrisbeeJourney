using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ResultPanelView : MonoBehaviour
{
    [Header("Visibility")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Images")]
    [SerializeField] private Image panelBackground;
    [SerializeField] private Image primaryButtonImage;
    [SerializeField] private Image secondaryButtonImage;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI primaryButtonLabel;
    [SerializeField] private TextMeshProUGUI secondaryButtonLabel;

    [Header("Buttons")]
    [SerializeField] private Button primaryButton;
    [SerializeField] private Button secondaryButton;

    [Header("Formatting")]
    [TextArea]
    [SerializeField]
    private string statsFormat =
        "이번 비행 거리: {0:0}m\n" +
        "누적 비행 거리: {1:0}m\n" +
        "누적 코인 개수: {2}개";

    private UnityAction cachedPrimaryAction;
    private UnityAction cachedSecondaryAction;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        SetVisible(false);
    }

    public void Show(
        RunResultSnapshot snapshot,
        string title,
        string primaryLabel,
        string secondaryLabel,
        bool showSecondaryButton,
        UnityAction primaryAction,
        UnityAction secondaryAction,
        ResultScreenTheme theme)
    {
        ApplyTheme(theme);

        if (titleText != null)
            titleText.text = title;

        if (statsText != null)
        {
            statsText.text = string.Format(
                statsFormat,
                snapshot.lastFlightDistance,
                snapshot.totalFlightDistance,
                snapshot.pendingCoins
            );
        }

        if (primaryButtonLabel != null)
            primaryButtonLabel.text = primaryLabel;

        if (secondaryButtonLabel != null)
            secondaryButtonLabel.text = secondaryLabel;

        if (secondaryButton != null)
            secondaryButton.gameObject.SetActive(showSecondaryButton);

        BindPrimaryButton(primaryAction);
        BindSecondaryButton(
            showSecondaryButton ? secondaryAction : null
        );

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            gameObject.SetActive(visible);
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void BindPrimaryButton(UnityAction action)
    {
        if (primaryButton == null)
            return;

        if (cachedPrimaryAction != null)
            primaryButton.onClick.RemoveListener(cachedPrimaryAction);

        cachedPrimaryAction = action;

        if (cachedPrimaryAction != null)
            primaryButton.onClick.AddListener(cachedPrimaryAction);
    }

    private void BindSecondaryButton(UnityAction action)
    {
        if (secondaryButton == null)
            return;

        if (cachedSecondaryAction != null)
            secondaryButton.onClick.RemoveListener(cachedSecondaryAction);

        cachedSecondaryAction = action;

        if (cachedSecondaryAction != null)
            secondaryButton.onClick.AddListener(cachedSecondaryAction);
    }

    private void ApplyTheme(ResultScreenTheme theme)
    {
        if (theme == null)
            return;

        ApplyImageTheme(
            panelBackground,
            theme.panelSprite,
            theme.panelColor
        );

        ApplyImageTheme(
            primaryButtonImage,
            theme.primaryButtonSprite,
            theme.primaryButtonColor
        );

        ApplyImageTheme(
            secondaryButtonImage,
            theme.secondaryButtonSprite,
            theme.secondaryButtonColor
        );

        ApplyFont(titleText, theme.fontAsset, theme.titleColor);
        ApplyFont(statsText, theme.fontAsset, theme.bodyTextColor);

        ApplyFont(
            primaryButtonLabel,
            theme.fontAsset,
            theme.buttonTextColor
        );

        ApplyFont(
            secondaryButtonLabel,
            theme.fontAsset,
            theme.buttonTextColor
        );
    }

    private void ApplyFont(
        TextMeshProUGUI text,
        TMP_FontAsset font,
        Color color)
    {
        if (text == null)
            return;

        if (font != null)
            text.font = font;

        text.color = color;
    }
    private void ApplyImageTheme(
    Image image,
    Sprite sprite,
    Color color)
    {
        if (image == null)
            return;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }

        image.color = color;
    }
}