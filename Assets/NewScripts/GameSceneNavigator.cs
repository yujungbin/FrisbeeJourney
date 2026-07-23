using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneNavigator : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField]
    private string startSceneName = "StartScene";

    private bool isLoading;

    public void ReturnToStartScene()
    {
        if (isLoading)
            return;

        if (string.IsNullOrWhiteSpace(startSceneName))
        {
            Debug.LogError(
                "GameSceneNavigator: Start Scene 이름이 비어 있습니다."
            );

            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(startSceneName))
        {
            Debug.LogError(
                $"GameSceneNavigator: '{startSceneName}' Scene을 로드할 수 없습니다. " +
                "Scene 이름과 Build Profiles의 Scene 목록을 확인하세요."
            );

            return;
        }

        isLoading = true;

        // 결과 화면에서 Time.timeScale을 0으로 사용한 경우를 대비합니다.
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            startSceneName,
            LoadSceneMode.Single
        );
    }
}