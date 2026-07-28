using UnityEngine;

[CreateAssetMenu(
    fileName = "DiscCosmetic_",
    menuName = "Frisbee Journey/Disc Cosmetic Data"
)]
public sealed class DiscCosmeticData : ScriptableObject
{
    [Header("기본 정보")]
    [SerializeField] private string cosmeticId = "default";
    [SerializeField] private string displayName = "주황색 원반";

    [SerializeField, TextArea(2, 4)]
    private string description = "행운을 주는 주황색 기본 원반";

    [SerializeField] private Sprite thumbnail;

    [Header("3D 외형")]
    [SerializeField] private Mesh visualMesh;
    [SerializeField] private Material[] visualMaterials;

    [Header("외형 위치 보정")]
    [SerializeField] private Vector3 modelLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 modelLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 modelLocalScale = Vector3.one;

    [Header("보유 정보")]
    [SerializeField, Min(0)] private int price;
    [SerializeField] private bool ownedByDefault;

    public string CosmeticId => cosmeticId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Thumbnail => thumbnail;

    public Mesh VisualMesh => visualMesh;
    public Material[] VisualMaterials => visualMaterials;

    public Vector3 ModelLocalPosition => modelLocalPosition;
    public Vector3 ModelLocalEulerAngles => modelLocalEulerAngles;
    public Vector3 ModelLocalScale => modelLocalScale;

    public int Price => price;
    public bool OwnedByDefault => ownedByDefault;

    private void OnValidate()
    {
        price = Mathf.Max(0, price);

        if (modelLocalScale == Vector3.zero)
        {
            modelLocalScale = Vector3.one;
        }

        if (cosmeticId != null)
        {
            cosmeticId = cosmeticId.Trim();
        }
    }
}