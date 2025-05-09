using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverUIController : MonoBehaviour
{
    public GameObject gameOverGroup;
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI hintText;
    public BilliardsManager billiardsManager;

    public void ShowGameOver(bool win, string hint)
    {
        resultText.text = win ? "You Win!" : "You Lose!";
        hintText.text = hint;
        gameOverGroup.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void RestartGame()
    {
        // Hide the game over menu
        gameOverGroup.SetActive(false);

        // Restart the game logic
        billiardsManager.RestartGame();
    }

    public void ExitToMainMenu()
    {
        SceneManager.LoadScene("MainMenuScene");
    }
}
