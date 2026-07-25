using UnityEngine;

public static class DiscCosmeticSave
{
    private const string EquippedCosmeticKey =
        "DiscCosmetic.EquippedId";

    private const string OwnedCosmeticKeyPrefix =
        "DiscCosmetic.Owned.";

    public static string GetEquippedCosmeticId(string fallbackId)
    {
        return PlayerPrefs.GetString(
            EquippedCosmeticKey,
            fallbackId
        );
    }

    public static bool IsOwned(DiscCosmeticData cosmetic)
    {
        if (cosmetic == null)
        {
            return false;
        }

        if (cosmetic.OwnedByDefault)
        {
            return true;
        }

        string key =
            OwnedCosmeticKeyPrefix + cosmetic.CosmeticId;

        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    public static void Unlock(DiscCosmeticData cosmetic)
    {
        if (cosmetic == null)
        {
            return;
        }

        string key =
            OwnedCosmeticKeyPrefix + cosmetic.CosmeticId;

        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }

    public static bool TryEquip(DiscCosmeticData cosmetic)
    {
        if (cosmetic == null)
        {
            return false;
        }

        if (!IsOwned(cosmetic))
        {
            Debug.LogWarning(
                $"보유하지 않은 원반은 장착할 수 없습니다: " +
                $"{cosmetic.DisplayName}"
            );

            return false;
        }

        PlayerPrefs.SetString(
            EquippedCosmeticKey,
            cosmetic.CosmeticId
        );

        PlayerPrefs.Save();

        return true;
    }
}