using UnityEngine;

public class GuideCollectible : MonoBehaviour
{
    [Header("Collector")]
    [SerializeField] private string collectorTag = "Player";

    private bool isCollected;

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected)
            return;

        Transform rootObject =
            other.transform.root;

        if (
            rootObject.CompareTag(
                collectorTag
            ) == false
        )
        {
            return;
        }

        isCollected = true;

        Collect();
    }

    private void Collect()
    {
        Destroy(gameObject);
    }
}