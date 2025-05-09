using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public PhysicsManager physicsManager;
    public BilliardsManager billiardsManager;

    public bool IsMenuOpen { get; private set; }

    public void SetMenuOpen(bool open)
    {
        IsMenuOpen = open;

        if (open)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            billiardsManager.PauseSimulation();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            billiardsManager.ResumeSimulation();
        }
    }
    

    public enum GameMode
    {
        EightBall,
        NineBall,
        TrickShot
    }

    [Header("Game Mode Settings")]
    public GameMode currentGameMode = GameMode.EightBall;
    public bool trainingMode = true;
    public bool ballInHand = false;
    public bool kitchenRuleEnabled = false;

    public enum InteractionMode { Shooting, Positioning }
    public InteractionMode currentMode = InteractionMode.Shooting;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void FixedUpdate()
    {
        if (IsMenuOpen) return;

        if (billiardsManager != null && billiardsManager.IsSimulationPaused) return;

        if (!AnyBallMoving()) return;

        physicsManager.Simulate();
        SyncVisuals();
    }

    void Update()
    {
        if (IsMenuOpen) return;

        if (Input.GetKeyDown(KeyCode.P))
        {
            if (billiardsManager.IsSimulationPaused)
            {
                Debug.Log("Resuming simulation...");
                billiardsManager.ResumeSimulation();
            }
            else
            {
                Debug.Log("Pausing simulation...");
                billiardsManager.PauseSimulation();
            }
        }

        // if (!AnyBallMoving())
        // {
        //     if (Input.GetKeyDown(KeyCode.Space))
        //     {
        //         ToggleMode();
        //     }
        // }
    }

    private void ToggleMode()
    {
        currentMode = currentMode == InteractionMode.Shooting ? InteractionMode.Positioning : InteractionMode.Shooting;

        // Lock and hide cursor of shooting mode
        if (currentMode == InteractionMode.Shooting)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            billiardsManager.ResumeSimulation();
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            billiardsManager.PauseSimulation();
        }

        Debug.Log("Switched to " + currentMode + " mode.");
    }

    public bool AnyBallMoving()
    {
        foreach (var ball in physicsManager.balls)
        {
            if (ball.isMoving) return true;
        }
        return false;
    }

    private void SyncVisuals()
    {
        foreach (var ball in physicsManager.balls)
        {
            ball.SyncTransform();
        }
    }
}
