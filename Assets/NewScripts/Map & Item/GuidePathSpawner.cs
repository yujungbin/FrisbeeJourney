using System.Collections.Generic;
using UnityEngine;

public class GuidePathSpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("길잡이 아이템 프리팹")]
    [SerializeField] private GameObject itemPrefab;

    [Tooltip("Point_0, Point_1... 이 들어있는 부모")]
    [SerializeField] private Transform pathRoot;

    [Header("Item Settings")]
    [Tooltip("아이템 사이의 간격")]
    [SerializeField] private float itemSpacing = 5f;

    [Tooltip("도로 표면에서 기본적으로 띄울 높이")]
    [SerializeField] private float baseHeight = 1.5f;

    [Header("Arc Settings")]
    [Tooltip("포물선 최고 높이")]
    [SerializeField] private float arcHeight = 5f;

    [Tooltip("경로 전체에서 포물선이 몇 번 반복될지")]
    [SerializeField] private int arcCount = 2;

    [Header("Curve Settings")]
    [Tooltip("곡선을 얼마나 부드럽게 계산할지. 높을수록 부드럽지만 계산량 증가")]
    [Range(5, 50)]
    [SerializeField] private int curveResolution = 20;

    [Header("Debug")]
    [SerializeField] private bool drawPath = true;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();

    private Transform runtimeContainer;

    private void Start()
    {
        SpawnGuideItems();
    }

    public void ResetGuideItems()
    {
        ClearGuideItems();
        SpawnGuideItems();
    }

    private void SpawnGuideItems()
    {
        if (itemPrefab == null)
        {
            Debug.LogWarning(gameObject.name + ": Guide Item Prefab이 비어있습니다.");
            return;
        }

        if (pathRoot == null)
        {
            Debug.LogWarning(gameObject.name + ": Path Root가 비어있습니다.");
            return;
        }

        if (pathRoot.childCount < 2)
        {
            Debug.LogWarning(gameObject.name + ": Path Point가 최소 2개 필요합니다.");
            return;
        }

        if (itemSpacing <= 0f)
        {
            Debug.LogWarning(gameObject.name + ": Item Spacing은 0보다 커야 합니다.");
            return;
        }

        CreateRuntimeContainer();

        List<Vector3> curvePoints = BuildCurvePoints();

        if (curvePoints.Count < 2)
            return;

        float totalLength = CalculateTotalLength(curvePoints);

        if (totalLength <= 0f)
            return;

        for (float distance = 0f;
             distance <= totalLength;
             distance += itemSpacing)
        {
            float normalizedDistance = distance / totalLength;

            Vector3 pathPosition =
                GetPositionAtDistance(
                    curvePoints,
                    distance
                );

            float arcOffset =
                CalculateArcHeight(normalizedDistance);

            pathPosition.y += baseHeight + arcOffset;

            GameObject newItem = Instantiate(
                itemPrefab,
                runtimeContainer
            );

            newItem.transform.localPosition = pathPosition;
            newItem.transform.localRotation = Quaternion.identity;

            spawnedItems.Add(newItem);
        }
    }

    private void CreateRuntimeContainer()
    {
        GameObject containerObject =
            new GameObject("GuideItems_Runtime");

        runtimeContainer = containerObject.transform;

        runtimeContainer.SetParent(transform);

        runtimeContainer.localPosition = Vector3.zero;
        runtimeContainer.localRotation = Quaternion.identity;
        runtimeContainer.localScale = Vector3.one;
    }

    private List<Vector3> BuildCurvePoints()
    {
        List<Vector3> controlPoints =
            new List<Vector3>();

        for (int i = 0; i < pathRoot.childCount; i++)
        {
            Transform point = pathRoot.GetChild(i);

            Vector3 localPoint =
                transform.InverseTransformPoint(
                    point.position
                );

            controlPoints.Add(localPoint);
        }

        List<Vector3> curvePoints =
            new List<Vector3>();

        if (controlPoints.Count == 2)
        {
            BuildStraightPath(
                controlPoints,
                curvePoints
            );

            return curvePoints;
        }

        for (int segment = 0;
             segment < controlPoints.Count - 1;
             segment++)
        {
            Vector3 p0 =
                controlPoints[
                    Mathf.Max(segment - 1, 0)
                ];

            Vector3 p1 =
                controlPoints[segment];

            Vector3 p2 =
                controlPoints[segment + 1];

            Vector3 p3 =
                controlPoints[
                    Mathf.Min(
                        segment + 2,
                        controlPoints.Count - 1
                    )
                ];

            for (int i = 0;
                 i < curveResolution;
                 i++)
            {
                float t =
                    i / (float)curveResolution;

                Vector3 point =
                    CatmullRom(
                        p0,
                        p1,
                        p2,
                        p3,
                        t
                    );

                curvePoints.Add(point);
            }
        }

        curvePoints.Add(
            controlPoints[
                controlPoints.Count - 1
            ]
        );

        return curvePoints;
    }

    private void BuildStraightPath(
        List<Vector3> controlPoints,
        List<Vector3> curvePoints
    )
    {
        Vector3 start = controlPoints[0];
        Vector3 end = controlPoints[1];

        for (int i = 0;
             i <= curveResolution;
             i++)
        {
            float t =
                i / (float)curveResolution;

            curvePoints.Add(
                Vector3.Lerp(
                    start,
                    end,
                    t
                )
            );
        }
    }

    private Vector3 CatmullRom(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        float t
    )
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f *
        (
            2f * p1
            +
            (-p0 + p2) * t
            +
            (
                2f * p0
                - 5f * p1
                + 4f * p2
                - p3
            ) * t2
            +
            (
                -p0
                + 3f * p1
                - 3f * p2
                + p3
            ) * t3
        );
    }

    private float CalculateTotalLength(
        List<Vector3> points
    )
    {
        float length = 0f;

        for (int i = 1;
             i < points.Count;
             i++)
        {
            length += Vector3.Distance(
                points[i - 1],
                points[i]
            );
        }

        return length;
    }

    private Vector3 GetPositionAtDistance(
        List<Vector3> points,
        float targetDistance
    )
    {
        float travelledDistance = 0f;

        for (int i = 1;
             i < points.Count;
             i++)
        {
            Vector3 previous = points[i - 1];
            Vector3 current = points[i];

            float segmentLength =
                Vector3.Distance(
                    previous,
                    current
                );

            if (
                travelledDistance
                + segmentLength
                >= targetDistance
            )
            {
                float remainingDistance =
                    targetDistance
                    - travelledDistance;

                float t =
                    segmentLength > 0f
                        ? remainingDistance
                            / segmentLength
                        : 0f;

                return Vector3.Lerp(
                    previous,
                    current,
                    t
                );
            }

            travelledDistance +=
                segmentLength;
        }

        return points[
            points.Count - 1
        ];
    }

    private float CalculateArcHeight(
        float normalizedDistance
    )
    {
        if (arcHeight <= 0f)
            return 0f;

        if (arcCount <= 0)
            return 0f;

        float arc =
            Mathf.Abs(
                Mathf.Sin(
                    normalizedDistance
                    * Mathf.PI
                    * arcCount
                )
            );

        return arc * arcHeight;
    }

    private void ClearGuideItems()
    {
        for (int i = 0;
             i < spawnedItems.Count;
             i++)
        {
            if (spawnedItems[i] != null)
            {
                Destroy(
                    spawnedItems[i]
                );
            }
        }

        spawnedItems.Clear();

        if (runtimeContainer != null)
        {
            Destroy(
                runtimeContainer.gameObject
            );

            runtimeContainer = null;
        }
    }

    private void OnDrawGizmos()
    {
        if (drawPath == false)
            return;

        if (pathRoot == null)
            return;

        if (pathRoot.childCount < 2)
            return;

        List<Vector3> points =
            new List<Vector3>();

        for (int i = 0;
             i < pathRoot.childCount;
             i++)
        {
            points.Add(
                pathRoot.GetChild(i).position
            );
        }

        Gizmos.color = Color.yellow;

        for (int i = 0;
             i < points.Count;
             i++)
        {
            Gizmos.DrawSphere(
                points[i],
                0.5f
            );
        }

        Gizmos.color = Color.cyan;

        for (int segment = 0;
             segment < points.Count - 1;
             segment++)
        {
            Vector3 p0 =
                points[
                    Mathf.Max(
                        segment - 1,
                        0
                    )
                ];

            Vector3 p1 =
                points[segment];

            Vector3 p2 =
                points[segment + 1];

            Vector3 p3 =
                points[
                    Mathf.Min(
                        segment + 2,
                        points.Count - 1
                    )
                ];

            Vector3 previous = p1;

            for (int i = 1;
                 i <= curveResolution;
                 i++)
            {
                float t =
                    i / (float)curveResolution;

                Vector3 current =
                    CatmullRom(
                        p0,
                        p1,
                        p2,
                        p3,
                        t
                    );

                Gizmos.DrawLine(
                    previous,
                    current
                );

                previous = current;
            }
        }
    }
}