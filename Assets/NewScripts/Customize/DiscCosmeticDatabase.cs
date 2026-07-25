using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DiscCosmeticDatabase",
    menuName = "Frisbee Journey/Disc Cosmetic Database"
)]
public sealed class DiscCosmeticDatabase : ScriptableObject
{
    [SerializeField]
    private DiscCosmeticData defaultCosmetic;

    [SerializeField]
    private List<DiscCosmeticData> cosmetics =
        new List<DiscCosmeticData>();

    public DiscCosmeticData DefaultCosmetic => defaultCosmetic;

    public IReadOnlyList<DiscCosmeticData> Cosmetics => cosmetics;

    public DiscCosmeticData GetById(string cosmeticId)
    {
        if (string.IsNullOrWhiteSpace(cosmeticId))
        {
            return defaultCosmetic;
        }

        for (int i = 0; i < cosmetics.Count; i++)
        {
            DiscCosmeticData cosmetic = cosmetics[i];

            if (cosmetic == null)
            {
                continue;
            }

            if (cosmetic.CosmeticId == cosmeticId)
            {
                return cosmetic;
            }
        }

        return defaultCosmetic;
    }

    public DiscCosmeticData GetEquippedCosmetic()
    {
        string fallbackId = defaultCosmetic != null
            ? defaultCosmetic.CosmeticId
            : string.Empty;

        string equippedId =
            DiscCosmeticSave.GetEquippedCosmeticId(fallbackId);

        return GetById(equippedId);
    }
}