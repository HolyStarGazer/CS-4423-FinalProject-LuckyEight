using UnityEngine;

public class HelpUIController : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup helpPanel;
    //public GameObject helpButtonIcon;

    private bool isVisible = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            ToggleHelp();
        }
    }

    public void ToggleHelp()
    {
        isVisible = !isVisible;

        helpPanel.alpha = isVisible ? 1f : 0f;
        helpPanel.interactable = isVisible;
        helpPanel.blocksRaycasts = isVisible;
    }
}
