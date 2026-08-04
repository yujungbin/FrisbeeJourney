using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class GameSceneNavigator : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("돌아갈 시작 Scene의 정확한 이름입니다. .unity는 붙이지 않습니다.")]
    [SerializeField]
    private string startSceneName = "StartScene";

    [Header("Run Reward")]
    [Tooltip("PlayScene에서 임시 코인을 관리하는 RunCoinBank입니다.")]
    [SerializeField]
    private RunCoinBank runCoinBank;

    [Tooltip(
        "StartScene으로 이동하기 전에 RunCoinBank의 임시 코인을 " +
        "DiscProgressionStore에 정산합니다."
    )]
    [SerializeField]
    private bool commitPendingCoinsBeforeReturn = true;

    [Header("Debug")]
    [SerializeField]
    private bool logNavigation = true;

    private bool isLoading;

    /// <summary>
    /// 임시 코인을 영구 코인으로 정산한 뒤 StartScene으로 돌아갑니다.
    /// 결과 화면의 QUIT / COLLECT 버튼에서 이 메서드를 호출합니다.
    /// </summary>
    public void ReturnToStartScene()
    {
        if (isLoading)
            return;

        if (!ValidateStartScene())
            return;

        isLoading = true;

        int pendingCoinsBeforeCommit =
            runCoinBank != null
                ? runCoinBank.PendingCoins
                : 0;

        if (commitPendingCoinsBeforeReturn)
        {
            if (runCoinBank == null)
            {
                Debug.LogError(
                    "GameSceneNavigator: RunCoinBank가 연결되지 않아 " +
                    "코인을 정산하지 않고 Scene을 이동할 수 없습니다.",
                    this
                );

                isLoading = false;
                return;
            }

            bool committed =
                runCoinBank.CommitPendingCoins();

            if (!committed)
            {
                Debug.LogError(
                    "GameSceneNavigator: 코인 정산에 실패하여 " +
                    "StartScene 이동을 취소했습니다.",
                    this
                );

                isLoading = false;
                return;
            }
        }

        // 결과 UI에서 Time.timeScale을 0으로 설정했을 경우 복구합니다.
        Time.timeScale = 1f;

        if (logNavigation)
        {
            Debug.Log(
                $"Returning to StartScene | " +
                $"scene: {startSceneName}, " +
                $"committed pending coins: {pendingCoinsBeforeCommit}",
                this
            );
        }

        SceneManager.LoadScene(
            startSceneName,
            LoadSceneMode.Single
        );
    }

    private bool ValidateStartScene()
    {
        if (string.IsNullOrWhiteSpace(startSceneName))
        {
            Debug.LogError(
                "GameSceneNavigator: Start Scene 이름이 비어 있습니다.",
                this
            );

            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(startSceneName))
        {
            Debug.LogError(
                $"GameSceneNavigator: '{startSceneName}' Scene을 " +
                "로드할 수 없습니다. Scene 이름과 Build Profiles의 " +
                "Scene 목록을 확인하세요.",
                this
            );

            return false;
        }

        return true;
    }
}