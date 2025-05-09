using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [Header("References")]
    public GameObject pauseMenuGroup; 
    public GameManager gameManager;
    public BilliardsManager billiardsManager; 

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Show/Hide the options menu
            if (pauseMenuGroup.activeSelf)
            {
                HidePauseMenu();
            }
            else
            {
                ShowPauseMenu();
            }
        }
    }

    public void ShowPauseMenu()
    {
        pauseMenuGroup.SetActive(true);

        gameManager.SetMenuOpen(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void HidePauseMenu()
    {
        pauseMenuGroup.SetActive(false);

        gameManager.SetMenuOpen(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ExitToMainMenu()
    {
        // Load the main menu scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    public void ResumeGame()
    {
        // Hide the options menu
        HidePauseMenu();
    }

    public void RestartGame()
    {
        // Restart the current game scene
        billiardsManager.RestartGame();
    }
}
