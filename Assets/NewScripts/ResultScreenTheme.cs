using TMPro;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ResultScreenTheme",
    menuName = "Disc Game/UI/Result Screen Theme"
)]
public class ResultScreenTheme : ScriptableObject
{
    [Header("Font")]
    public TMP_FontAsset fontAsset;

    [Header("Sprites")]
    public Sprite panelSprite;
    public Sprite primaryButtonSprite;
    public Sprite secondaryButtonSprite;

    [Header("Colors")]
    public Color overlayColor = new Color(0f, 0f, 0f, 0.2f);
    public Color panelColor = new Color(1f, 1f, 1f, 0.9f);

    public Color titleColor = Color.white;
    public Color bodyTextColor = Color.black;

    public Color primaryButtonColor =
        new Color(0.05f, 0.35f, 0.55f, 1f);

    public Color secondaryButtonColor =
        new Color(0.8f, 0.1f, 0.08f, 1f);

    public Color buttonTextColor = Color.white;
}
