using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunActiveTimeTracker : MonoBehaviour
{
    [Serializable]
    public struct RunTimeRecord
    {
        public double flyingSeconds;
        public double settlingSeconds;
        public bool completed;

        public double TotalSeconds =>
            flyingSeconds + settlingSeconds;
    }

    [Serializable]
    private sealed class SavedRecords
    {
        public bool hasLastRun;
        public RunTimeRecord lastRun;

        public bool hasBestRun;
        public RunTimeRecord bestRun;
    }

    [Tooltip("맵과 기록 규칙을 구분하는 저장 ID입니다.")]
    [SerializeField]
    private string recordId = "map_01_active_v1";

    private const string SaveKeyPrefix = "DiscGame.RunTime.";

    private string saveKey;
    private SavedRecords saved;
    private RunTimeRecord currentRun;
    private DiscSlingshotController source;

    public bool IsRecording { get; private set; }
    public bool IsNewBest { get; private set; }

    public RunTimeRecord CurrentRun => currentRun;

    public bool HasLastRun =>
        saved != null && saved.hasLastRun;

    public bool HasBestRun =>
        saved != null && saved.hasBestRun;

    public RunTimeRecord LastRun =>
        saved != null ? saved.lastRun : default;

    public RunTimeRecord BestRun =>
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

    // 한 판 시작 시 한 번만 호출합니다.
    // 같은 판에서 재투척할 때는 호출하지 않습니다.
    public void BeginRun(DiscSlingshotController controller)
    {
        // 기록 중인 이전 판이 있으면 중도 종료로 마무리합니다.
        FinishRun(false);

        if (!isActiveAndEnabled || controller == null)
        {
            Debug.LogError(
                "RunActiveTimeTracker: 활성화된 기록기와 " +
                "DiscController가 필요합니다.",
                this
            );
            return;
        }

        LoadIfNeeded();

        currentRun = default;
        IsNewBest = false;

        source = controller;
        source.ActiveTimeAdvanced += HandleActiveTimeAdvanced;

        IsRecording = true;
    }

    private void HandleActiveTimeAdvanced(
        bool isFlying,
        float deltaSeconds)
    {
        if (!IsRecording || deltaSeconds <= 0f)
            return;

        if (isFlying)
            currentRun.flyingSeconds += deltaSeconds;
        else
            currentRun.settlingSeconds += deltaSeconds;
    }

    // completed는 실제 맵 완주일 때만 true로 전달합니다.
    public void FinishRun(bool completed)
    {
        // 중복 호출로 완료 기록이 실패 기록으로 바뀌는 것을 방지합니다.
        if (!IsRecording)
            return;

        IsRecording = false;

        if (source != null)
        {
            source.ActiveTimeAdvanced -= HandleActiveTimeAdvanced;
        }

        source = null;

        currentRun.completed = completed;

        // 실패하거나 중도 종료한 판도 최근 기록에는 남깁니다.
        saved.hasLastRun = true;
        saved.lastRun = currentRun;

        IsNewBest =
            completed &&
            currentRun.TotalSeconds > 0.0 &&
            (
                !saved.hasBestRun ||
                currentRun.TotalSeconds < saved.bestRun.TotalSeconds
            );

        if (IsNewBest)
        {
            saved.hasBestRun = true;
            saved.bestRun = currentRun;
        }

        // 매 프레임 저장하지 않고 판 종료 시에만 저장합니다.
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
                "RunActiveTimeTracker: 저장된 시간 기록을 " +
                "읽을 수 없어 빈 기록으로 시작합니다.",
                this
            );
        }
    }
}