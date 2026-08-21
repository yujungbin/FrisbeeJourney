using System.Collections;
using UnityEngine;

public class SettingsPanelController : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject resetMessage;

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    public void ShowResetMessage()
    {
        StartCoroutine(ShowResetMessageRoutine());
    }

    private IEnumerator ShowResetMessageRoutine()
    {
        resetMessage.SetActive(true);

        yield return new WaitForSecondsRealtime(1.5f);

        resetMessage.SetActive(false);
    }
}