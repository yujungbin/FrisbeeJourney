using UnityEngine;
using UnityEngine.SceneManagement;

public class StartSceneController : MonoBehaviour
{
    [SerializeField]
    private string gameSceneName = "SampleScene";

    private bool isLoading = false;

    public void MoveToGameScene()
    {
        if (isLoading)
        {
            return;
        }

        isLoading = true;
        SceneManager.LoadScene(gameSceneName);
    }
}