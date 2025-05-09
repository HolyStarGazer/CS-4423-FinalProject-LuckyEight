using UnityEngine;

public class BallDragHandler : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public GameManager gameManager;
    public BilliardsManager billiardsManager;

    [Header("Layer Masks")]
    public LayerMask tableMask;
    public LayerMask ballMask;

    private Ball selectedBall = null;
    private bool isDragging = false;
    private Vector3 grabOffset = Vector3.zero;

    [Header("Kitchen Rule Settings")]
    public bool showKitchenBounds = true;
    private float kitchenX;

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        kitchenX = gameManager.billiardsManager.TableBounds.x * -1f;
    }

    void Update()
    {
        if (gameManager.IsMenuOpen) return;
        if (!gameManager.billiardsManager.IsSimulationPaused || gameManager.currentMode != GameManager.InteractionMode.Positioning) return;

        HandleMouseInput();
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Attempting to drag ball");
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, ballMask))
            {
                Debug.Log("Hit ball: " + hit.collider.name);
                Ball ball = hit.collider.GetComponent<Ball>();
                if (ball == null || !ball.inPlay || !CanDragBall(ball)) return;

                selectedBall = ball;
                grabOffset = hit.point - ball.position;
                isDragging = true;
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            Debug.Log("Releasing ball drag");
            SnapToSurface();
            selectedBall = null;
            isDragging = false;
        }

        if (isDragging && selectedBall != null)
        {
            Debug.Log("Dragging ball: " + selectedBall.name);
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, tableMask))
            {
                Vector3 newPosition = hit.point + grabOffset;
                newPosition.y = selectedBall.radius;

                foreach (var other in billiardsManager.balls)
                {
                    if (other == selectedBall || !other.inPlay) continue;
                    float minDistance = selectedBall.radius + other.radius;
                    if (Vector3.Distance(newPosition, other.position) < minDistance) return;
                }

                if (gameManager.trainingMode)
                {
                    MoveBall(selectedBall, newPosition);
                }
                else if (gameManager.ballInHand && selectedBall.ID == 0)
                {
                    if (gameManager.kitchenRuleEnabled && newPosition.x < kitchenX) return;

                    MoveBall(selectedBall, newPosition);
                }
            }
        }
    }

    private void SnapToSurface()
    {
        if (selectedBall != null)
        {
            Vector3 pos = selectedBall.position;
            pos.y = selectedBall.radius;
            selectedBall.position = pos;
        }
    }

    private void OnDrawGizmos()
    {
        if (gameManager == null || !showKitchenBounds) return;
        if (!gameManager.ballInHand || !gameManager.kitchenRuleEnabled) return;

        float width = gameManager.billiardsManager.TableBounds.x * 2f;
        float height = gameManager.billiardsManager.TableBounds.y * 2f;

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.25f);
        Vector3 center = new Vector3(kitchenX / 2f, 0.02f, 0f);
        Vector3 size = new Vector3(width / 2f, 0.01f, height);
        Gizmos.DrawCube(center, size);
    }

    private bool CanDragBall(Ball ball)
    {
        if (gameManager.trainingMode) return true;
        if (gameManager.ballInHand && ball.ID == 0) return true;
        return false;
    }

    private void MoveBall(Ball ball, Vector3 position)
    {
        ball.position = position;
        ball.velocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.isMoving = false;
        ball.positionDirty = true;
        ball.SyncTransform();
    }
}
