using UnityEngine;

[DisallowMultipleComponent]
public sealed class DiscAppearanceApplier : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField]
    private DiscCosmeticDatabase cosmeticDatabase;

    [Header("외형 대상")]
    [SerializeField]
    private MeshFilter targetMeshFilter;

    [SerializeField]
    private MeshRenderer targetMeshRenderer;

    [Header("자동 적용")]
    [SerializeField]
    private bool applyEquippedCosmeticOnAwake = true;

    public DiscCosmeticData CurrentCosmetic { get; private set; }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();

        if (applyEquippedCosmeticOnAwake)
        {
            ApplyEquippedCosmetic();
        }
    }

    public void ApplyEquippedCosmetic()
    {
        if (cosmeticDatabase == null)
        {
            Debug.LogError(
                $"{name}: DiscCosmeticDatabase가 연결되지 않았습니다."
            );

            return;
        }

        DiscCosmeticData equippedCosmetic =
            cosmeticDatabase.GetEquippedCosmetic();

        ApplyCosmetic(equippedCosmetic);
    }

    public bool ApplyCosmetic(DiscCosmeticData cosmetic)
    {
        if (cosmetic == null)
        {
            Debug.LogError(
                $"{name}: 적용할 원반 데이터가 없습니다."
            );

            return false;
        }

        ResolveReferences();

        if (targetMeshFilter == null ||
            targetMeshRenderer == null)
        {
            Debug.LogError(
                $"{name}: MeshFilter 또는 MeshRenderer가 없습니다."
            );

            return false;
        }

        if (cosmetic.VisualMesh == null)
        {
            Debug.LogError(
                $"{cosmetic.DisplayName}에 Visual Mesh가 없습니다."
            );

            return false;
        }

        targetMeshFilter.sharedMesh =
            cosmetic.VisualMesh;

        Material[] materials =
            cosmetic.VisualMaterials;

        if (materials != null && materials.Length > 0)
        {
            targetMeshRenderer.sharedMaterials =
                materials;
        }
        else
        {
            Debug.LogWarning(
                $"{cosmetic.DisplayName}에 Material이 없습니다. " +
                "기존 Material을 유지합니다."
            );
        }

        transform.localPosition =
            cosmetic.ModelLocalPosition;

        transform.localRotation =
            Quaternion.Euler(
                cosmetic.ModelLocalEulerAngles
            );

        transform.localScale =
            cosmetic.ModelLocalScale;

        CurrentCosmetic = cosmetic;

        return true;
    }

    private void ResolveReferences()
    {
        if (targetMeshFilter == null)
        {
            targetMeshFilter =
                GetComponent<MeshFilter>();
        }

        if (targetMeshRenderer == null)
        {
            targetMeshRenderer =
                GetComponent<MeshRenderer>();
        }
    }
}