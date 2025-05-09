using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public BilliardsManager billiardsManager;
    public CameraController cameraController;

    // Update is called once per frame
    void Update()
    {
        // if (Input.GetKeyDown(KeyCode.Escape))
        // {
        //     // Show/Hide cursor
        //     Cursor.visible = !Cursor.visible;
        //     Cursor.lockState = Cursor.visible ? CursorLockMode.None : CursorLockMode.Locked;
        // }
        // if (Input.GetKeyDown(KeyCode.C))
        // {
        //     // Iterate camera perspectives
        //     //cameraController.CycleCamera();
        // }

        // Undo/Redo Key Bindings
        if (Input.GetKeyDown(KeyCode.I))
        {
            billiardsManager.UndoTurn();
        }
        if (Input.GetKeyDown(KeyCode.O))
        {
            billiardsManager.RedoTurn();
        }
    }
}
