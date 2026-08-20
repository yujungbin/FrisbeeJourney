using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ResultScreenController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private ResultPanelView intermediatePanel;
    [SerializeField] private ResultPanelView finalPanel;

    [Header("Overlay")]
    [SerializeField] private CanvasGroup overlayCanvasGroup;
    [SerializeField] private Image dimmedBackground;

    [Header("Data Sources")]
    [SerializeField] private RunProgressTracker progressTracker;
    [SerializeField] private RunCoinBank coinBank;
    [SerializeField] private DiscDurability durability;

    [Header("Theme")]
    [SerializeField] private ResultScreenTheme theme;

    [Header("Options")]
    [SerializeField] private bool pauseTimeScaleWhileOpen = false;

    [Header("Flow Events")]
    [Tooltip("중간 결과에서 '응!'을 눌렀을 때 호출됩니다.")]
    [SerializeField]
    private UnityEvent onContinueRequested =
        new UnityEvent();

    [Tooltip("중간 결과에서 '그만할래'를 눌렀을 때 호출됩니다.")]
    [SerializeField]
    private UnityEvent onQuitRequested =
        new UnityEvent();

    [Tooltip("최종 결과에서 '받기!'를 눌렀을 때 호출됩니다.")]
    [SerializeField]
    private UnityEvent onCollectRequested =
        new UnityEvent();
    [Header("Final Result Text")]
    [SerializeField]
    private string completeTitle = "COMPLETE!";

    [SerializeField]
    private string brokenTitle = "DISC BROKEN";

    [SerializeField]
    private string noThrowsTitle = "FINAL RESULT";

    [SerializeField]
    private string collectButtonLabel = "COLLECT";

    private bool opened;

    private void Awake()
    {
        ApplyOverlayTheme();
        HideAllImmediate();
    }

    public void ShowIntermediateResult()
    {
        RunResultSnapshot snapshot = BuildSnapshot(false);

        SetOverlayVisible(true);

        if (finalPanel != null)
            finalPanel.Hide();

        if (intermediatePanel != null)
        {
            intermediatePanel.Show(
                snapshot,
                "또 던질까?",
                "응!",
                "그만할래",
                true,
                HandleContinue,
                HandleQuit,
                theme
            );
        }

        SetPauseState(true);
    }

    public void ShowFinalCompleteResult()
    {
        ShowFinalResult(
            completeTitle,
            true
        );
    }

    public void ShowFinalBrokenResult()
    {
        ShowFinalResult(
            brokenTitle,
            false
        );
    }

    public void ShowFinalNoThrowsResult()
    {
        ShowFinalResult(
            noThrowsTitle,
            false
        );
    }
    //private void ShowNoThrowsFinalResult()
    //{
    //    if (finalResultShown)
    //        return;

    //    finalResultShown = true;
    //    runActive = false;
    //    rethrowRoutine = null;

    //    Debug.Log(
    //        $"Showing no-throws final result. " +
    //        $"Throws used: {throwsUsed}/{MaxThrowsPerRun}, " +
    //        $"remaining: {ThrowsRemaining}",
    //        this
    //    );

    //    if (resultScreenController == null)
    //    {
    //        Debug.LogError(
    //            "DiscRunManager: ResultScreenController is not assigned. " +
    //            "Assign ResultOverlayRoot to the Result Screen Controller field.",
    //            this
    //        );

    //        return;
    //    }

    //    resultScreenController.ShowFinalNoThrowsResult();
    //}
    public void HideAll()
    {
        if (intermediatePanel != null)
            intermediatePanel.Hide();

        if (finalPanel != null)
            finalPanel.Hide();

        SetOverlayVisible(false);
        SetPauseState(false);
    }

    private void ShowFinalResult(
    string title,
    bool completed)
    {
        /*
         * ResultOverlayRoot를 Hierarchy에서 꺼둔 경우에도
         * 다시 활성화합니다.
         */
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        RunResultSnapshot snapshot =
            BuildSnapshot(completed);

        SetOverlayVisible(true);

        if (intermediatePanel != null)
            intermediatePanel.Hide();

        if (finalPanel == null)
        {
            Debug.LogError(
                "ResultScreenController: Final Panel is not assigned.",
                this
            );

            return;
        }

        finalPanel.Show(
            snapshot,
            title,
            collectButtonLabel,
            string.Empty,
            false,
            HandleCollect,
            null,
            theme
        );

        SetPauseState(true);

        Canvas.ForceUpdateCanvases();

        Debug.Log(
            $"Final result shown | " +
            $"title: {title}, " +
            $"distance: {snapshot.totalFlightDistance:F1}, " +
            $"coins: {snapshot.pendingCoins}",
            this
        );
    }

    private RunResultSnapshot BuildSnapshot(bool completed)
    {
        return new RunResultSnapshot
        {
            lastFlightDistance = progressTracker != null
                ? progressTracker.CurrentThrowDistance
                : 0f,

            totalFlightDistance = progressTracker != null
                ? progressTracker.TotalDistance
                : 0f,

            pendingCoins = coinBank != null
                ? coinBank.PendingCoins
                : 0,

            currentDurability = durability != null
                ? durability.CurrentDurability
                : 0f,

            maxDurability = durability != null
                ? durability.MaxDurability
                : 1f,

            levelProgress01 = progressTracker != null
                ? progressTracker.LevelProgress01
                : 0f,

            levelCompleted = completed
        };
    }

    private void HandleContinue()
    {
        HideAll();
        onContinueRequested.Invoke();
    }

    private void HandleQuit()
    {
        HideAll();
        onQuitRequested.Invoke();
    }

    private void HandleCollect()
    {
        HideAll();
        onCollectRequested.Invoke();
    }

    private void ApplyOverlayTheme()
    {
        if (theme == null || dimmedBackground == null)
            return;

        dimmedBackground.color = theme.overlayColor;
    }

    private void SetOverlayVisible(bool visible)
    {
        if (visible && !gameObject.activeSelf)
            gameObject.SetActive(true);

        if (overlayCanvasGroup == null)
        {
            Debug.LogError(
                "ResultScreenController: Overlay CanvasGroup is not assigned.",
                this
            );

            return;
        }

        overlayCanvasGroup.alpha =
            visible ? 1f : 0f;

        overlayCanvasGroup.interactable =
            visible;

        overlayCanvasGroup.blocksRaycasts =
            visible;
    }

    private void SetPauseState(bool resultOpen)
    {
        if (!pauseTimeScaleWhileOpen)
            return;

        Time.timeScale = resultOpen ? 0f : 1f;
    }

    private void HideAllImmediate()
    {
        opened = false;

        if (intermediatePanel != null)
            intermediatePanel.Hide();

        if (finalPanel != null)
            finalPanel.Hide();

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.interactable = false;
            overlayCanvasGroup.blocksRaycasts = false;
        }
    }
}