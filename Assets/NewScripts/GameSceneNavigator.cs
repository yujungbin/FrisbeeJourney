using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneNavigator : MonoBehaviour
{
    [SerializeField] private string startSceneName = "StartScene";

    public void ReturnToStartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(startSceneName);
    }
}