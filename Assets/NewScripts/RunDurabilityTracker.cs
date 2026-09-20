using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunDurabilityTracker : MonoBehaviour
{
    [Serializable]
    public struct RunDurabilityRecord
    {
        public double consumedDurability;
        public bool completed;
    }

    [Serializable]
    private sealed class SavedRecords
    {
        public bool hasLastRun;
        public RunDurabilityRecord lastRun;

        public bool hasBestRun;
        public RunDurabilityRecord bestRun;
    }

    [Tooltip("맵과 기록 규칙을 구분하는 저장 ID입니다.")]
    [SerializeField]
    private string recordId = "map_01_durability_v1";

    private const string SaveKeyPrefix = "DiscGame.RunDurability.";

    private string saveKey;
    private SavedRecords saved;
    private RunDurabilityRecord currentRun;

    private DiscDurability source;
    private float previousDurability;

    public bool IsRecording { get; private set; }
    public bool IsNewBest { get; private set; }

    public RunDurabilityRecord CurrentRun => currentRun;

    public bool HasLastRun =>
        saved != null && saved.hasLastRun;

    public bool HasBestRun =>
        saved != null && saved.hasBestRun;

    public RunDurabilityRecord LastRun =>
        saved != null ? saved.lastRun : default;

    public RunDurabilityRecord BestRun =>
        saved != null ? saved.bestRun : default;

    private void Awake()
    {
        LoadIfNeeded();
    }

    private void OnDisable()
    {
        FinishRun(false);
    }

    private void OnApplicationQuit()
    {
        FinishRun(false);
    }

    // 한 판 시작 시, durability 초기화가 끝난 뒤 한 번만 호출합니다.
    // 같은 판에서 재투척할 때는 호출하지 않습니다.
    public void BeginRun(DiscDurability durability)
    {
        // 이전 판이 기록 중이면 중도 종료로 마무리합니다.
        FinishRun(false);

        if (!isActiveAndEnabled || durability == null)
        {
            Debug.LogError(
                "RunDurabilityTracker: 활성화된 기록기와 " +
                "DiscDurability가 필요합니다.",
                this
            );
            return;
        }

        LoadIfNeeded();

        currentRun = default;
        IsNewBest = false;

        source = durability;
        previousDurability = source.CurrentDurability;

        source.DurabilityChanged += HandleDurabilityChanged;
        IsRecording = true;
    }

    private void HandleDurabilityChanged(
        float currentDurability,
        float maxDurability)
    {
        AccumulateConsumption(currentDurability);
    }

    private void AccumulateConsumption(float currentDurability)
    {
        if (!IsRecording)
            return;

        // 요청된 데미지가 아니라 실제로 줄어든 내구도를 계산합니다.
        // 예: 남은 내구도 5에 데미지 20을 받아도 소모량은 5입니다.
        double decrease =
            (double)previousDurability - currentDurability;

        if (decrease > 0.0)
        {
            currentRun.consumedDurability += decrease;
        }

        // 회복 시에도 비교 기준은 갱신합니다.
        // 이미 누적한 소모량에서는 회복량을 빼지 않습니다.
        previousDurability = currentDurability;
    }

    // completed는 실제 맵 완주일 때만 true로 전달합니다.
    public void FinishRun(bool completed)
    {
        // 중복 호출로 완료 기록이 실패 기록으로 덮이는 것을 방지합니다.
        if (!IsRecording)
            return;

        if (source != null)
        {
            // 다른 이벤트 구독자가 먼저 종료를 요청하더라도
            // 마지막 내구도 감소를 빠뜨리지 않도록 확인합니다.
            AccumulateConsumption(source.CurrentDurability);

            source.DurabilityChanged -= HandleDurabilityChanged;
        }

        source = null;
        IsRecording = false;

        currentRun.completed = completed;

        // 실패하거나 중도 종료한 판도 최근 기록에는 남깁니다.
        saved.hasLastRun = true;
        saved.lastRun = currentRun;

        // 완주한 판끼리 비교합니다.
        // 무손상 완주인 0도 유효하며, 동률은 갱신하지 않습니다.
        IsNewBest =
            completed &&
            (
                !saved.hasBestRun ||
                currentRun.consumedDurability <
                saved.bestRun.consumedDurability
            );

        if (IsNewBest)
        {
            saved.hasBestRun = true;
            saved.bestRun = currentRun;
        }

        // 매번 내구도가 바뀔 때가 아니라 판 종료 시 저장합니다.
        PlayerPrefs.SetString(
            saveKey,
            JsonUtility.ToJson(saved)
        );

        PlayerPrefs.Save();
    }

    private void LoadIfNeeded()
    {
        if (saved != null)
            return;

        saveKey = SaveKeyPrefix + recordId;
        saved = new SavedRecords();

        string json = PlayerPrefs.GetString(saveKey, "");

        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            SavedRecords loaded =
                JsonUtility.FromJson<SavedRecords>(json);

            if (loaded != null)
                saved = loaded;
        }
        catch (ArgumentException)
        {
            Debug.LogWarning(
                "RunDurabilityTracker: 저장된 내구도 기록을 " +
                "읽을 수 없어 빈 기록으로 시작합니다.",
                this
            );
        }
    }
}