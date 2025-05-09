using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using System.Collections.Generic;
using Unity.VisualScripting;

public class CueController : MonoBehaviour
{
    [Header("References")]
    public LineRenderer predictionLine;
    public BilliardsManager billiardsManager;
    public GameManager gameManager;
    public CueSpinUIController cueSpinUI;
    //public GameObject spinIndicator;
    public Transform pivotFront;
    public Transform pivotRear; 

    //public GameObject ghostBallPrefab;
    //private List<GameObject> activeGhosts = new List<GameObject>();

    public Ball cueBall;
    public Transform cameraPivot;
    public Transform mainCamera;
    public Transform cuePivot;

    [Header("Shot Settings")]
    public float maxForce = 10f;
    public float minForce = 0.01f;
    public float forceSensitivty = 0.05f;
    public Vector3 cueOffset = new Vector3(0f, 0f, 0f); // dead center offset. Used for spin shots.
    public float cueVerticalOffset = 0f; // Vertical offset for cue stick. Allows for jump shots. Combined with spin for masse shots.
    
    [Header("Spin Settings")]
    public Vector3 spin = Vector3.zero;
    public float jumpAngle = 0f; // Angle for jump shots. 0-90 degrees.
    public float spinSpeed = 1f;
    public float maxSpinMagnitude = 0.90f;

    [Header("Camera Orbit")]
    public float cueDistance = 0.05f;
    public float orbitSpeed = 5f;
    public float cameraDistance = 1.5f;
    public float cameraHeight = 0.4f;

    private float shotForce = 0f;
    private bool charging = false;
    private float orbitAngle = 0f;
    
    private Vector3 aimDirection;

    public struct CuePredictionHit
    {
    public Vector3 position;
    public Vector3 normal;
    public GameObject hitObject;
    }

    void Update()
    {
        if (gameManager.IsMenuOpen) return;

        if (gameManager.currentMode != GameManager.InteractionMode.Shooting)
        {
            cuePivot.gameObject.SetActive(false);
            //spinIndicator.SetActive(false);
            return;
        }
        
        if (gameManager.AnyBallMoving())
        {
            cuePivot.gameObject.SetActive(false);
            //spinIndicator.SetActive(false);
            return;
        }

        cuePivot.gameObject.SetActive(true);
        //spinIndicator.SetActive(true);

        Vector3 cueBallFlat = new Vector3(cueBall.position.x, cuePivot.position.y, cueBall.position.z);
        cuePivot.LookAt(cueBallFlat);
        float cueDistance = 0.5f;
        Vector3 direction = (cuePivot.position - cueBall.position).normalized;
        cuePivot.position = cueBall.position + direction * -cueDistance;
        
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X");
            orbitAngle += mouseX * orbitSpeed;
        }

        // Aim camera and cue stick behind cue ball
        UpdateCameraAndCue();

        //PredictCuePath(cueBall.position, aimDirection, 10f, 5);
        //DrawPredictionLine(cueBall.position, aimDirection, 2f);


        // Spin offset
        UpdateSpinIndicator();
        UpdateJumpInidicator();

        // Cue stick charging
        if (Input.GetMouseButtonDown(0))
        {
            charging = true;
            shotForce = 0f;
        }

        if (Input.GetMouseButton(0) && charging)
        {
            float mouseY = Input.GetAxis("Mouse Y");
            shotForce += mouseY * forceSensitivty;
            shotForce = Mathf.Clamp(shotForce, 0f, maxForce);

            UpdateCuePosition();
        }

        if (Input.GetMouseButtonUp(0) && charging)
        {
            charging = false;

            if (shotForce >= minForce)
            {
                Quaternion elevation = Quaternion.AngleAxis(jumpAngle * Mathf.Rad2Deg, Vector3.right);
                Vector3 elevatedDir = elevation * aimDirection;

                cueBall.ApplyCueStrike(elevatedDir.normalized, shotForce, spin, jumpAngle);

                billiardsManager.isTurnOngoing = true; // Start turn after shot

                float pullBack = Mathf.Sin(Time.time) * 0.1f;
                cuePivot.position = new Vector3(0f, 0f, -pullBack);
            }

            shotForce = 0f;
            spin = Vector3.zero;
            jumpAngle = 0f; 
        }
    }

    private void UpdateJumpInidicator()
    {
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.W))
        {
            jumpAngle += Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.S))
        {
            jumpAngle -= Time.deltaTime;
        }

        jumpAngle = Mathf.Clamp(jumpAngle, 0f, Mathf.PI / 2f); // Limit jump angle to 0-90 degrees
    }

    private void UpdateSpinIndicator()
    {
        if (!Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.W))
        {
            spin += Vector3.up * Time.deltaTime;
        }
        if (!Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.S))
        {
            spin += Vector3.down * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.A))
        {
            spin += Vector3.left * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.D))
        {
            spin += Vector3.right * Time.deltaTime;
        }

        if(spin.magnitude > maxSpinMagnitude)
        {
            spin = spin.normalized * maxSpinMagnitude;
        }

        if (cueSpinUI != null)
        {
            cueSpinUI.SetSpin(new Vector2(spin.x, spin.y) / maxSpinMagnitude);
        }
    }

    void LateUpdate()
    {
        if (gameManager.IsMenuOpen) return;
        
        if (!cueBall.inPlay /*|| spinIndicator == null*/) return;

        float pullBack = Mathf.Lerp(0, cueDistance, shotForce / maxForce);
        Vector3 localRearOffset = new Vector3(0f, 0f, -(cueDistance + pullBack));

        Quaternion orbit = Quaternion.Euler(0f, orbitAngle, 0f);
        Quaternion elevation = Quaternion.AngleAxis(jumpAngle * Mathf.Rad2Deg, Vector3.right);
        Vector3 finalRearOffset = orbit * elevation * localRearOffset;
        pivotRear.position = cueBall.position + finalRearOffset;

        Vector3 cueDir = (cueBall.position - pivotRear.position).normalized;
        Quaternion cueRotation = Quaternion.LookRotation(cueDir, Vector3.up);

        // Local axes from cue rotation
        Vector3 forward = cueDir;
        Vector3 right = cueRotation * Vector3.right;
        Vector3 up = cueRotation * Vector3.up;

        Vector2 clampedSpin = Vector2.ClampMagnitude(new Vector2(spin.x, spin.y), maxSpinMagnitude);
        float a = clampedSpin.x * cueBall.radius;
        float b = clampedSpin.y * cueBall.radius;
        float c = Mathf.Sqrt(Mathf.Max(0f, cueBall.radius * cueBall.radius - a * a - b * b));

        Vector3 spinOffset = (right * a) + (up * b) - forward * c;

        pivotFront.position = cueBall.position + spinOffset;
        cuePivot.position = pivotRear.position;
        cuePivot.LookAt(pivotFront.position);

        //spinIndicator.transform.position = pivotFront.position;
        //spinIndicator.transform.rotation = Quaternion.LookRotation(spinOffset.normalized, up);
    }

    private Vector3 GetAimDirection()
    {
        Vector3 camToBall = cueBall.transform.position - cameraPivot.position;
        camToBall.y = 0f; // Ignore vertical component
        return camToBall.normalized;
    }

    private void UpdateCameraAndCue()
    {
        Vector3 offset = Quaternion.Euler(0f, orbitAngle, 0f) * Vector3.back * cameraDistance;
        Vector3 cameraPos = cueBall.transform.position + offset + Vector3.up * cameraHeight;

        cameraPivot.position = cameraPos;
        cameraPivot.LookAt(cueBall.transform.position, Vector3.up * 0.1f);

        aimDirection = GetAimDirection();
        Debug.DrawRay(cueBall.transform.position, aimDirection * 2f, Color.red);

        cuePivot.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);

        UpdateCuePosition();
    }

    private void UpdateCuePosition()
    {
        float pullBack = Mathf.Lerp(0, cueDistance, shotForce / maxForce);

        Vector3 localRearOffset = new Vector3(0f, 0f, -(cueDistance + pullBack));
        
        Quaternion elevation = Quaternion.AngleAxis(jumpAngle * Mathf.Rad2Deg, Vector3.right);
        Quaternion orbit = Quaternion.Euler(0f, orbitAngle, 0f);
        
        Vector3 finalRearOffset = orbit * elevation * localRearOffset;

        pivotRear.position = cueBall.position + finalRearOffset;

        cuePivot.position = pivotRear.position;
        cuePivot.LookAt(pivotFront.position, Vector3.up);
    }

    private void DrawPredictionLine(Vector3 start, Vector3 direction, float distance)
    {
        Vector3 end = start + direction * distance;
        predictionLine.SetPosition(0, start);
        predictionLine.SetPosition(1, end);
    }

    // public void PredictCuePath(Vector3 start, Vector3 direction, float maxDistance, int maxBounces)
    // {
    //     Vector3 currentOrigin = start;
    //     Vector3 currentDirection = direction.normalized;
    //     float remainingDistance = maxDistance;
    //     float radius = cueBall.radius * 0.95f;
    //     int linePointIndex = 0;

    //     List<Vector3> pathPoints = new List<Vector3> { currentOrigin };

    //     // Clean up old ghost balls
    //     foreach (var g in activeGhosts)
    //         Destroy(g);
    //     activeGhosts.Clear();

    //     for (int i = 0; i < maxBounces && remainingDistance > 0f; i++)
    //     {
    //         RaycastHit hit;
    //         if (Physics.SphereCast(currentOrigin, radius, currentDirection, out hit, remainingDistance))
    //         {
    //             pathPoints.Add(hit.point);

    //             if (hit.collider.CompareTag("Ball"))
    //             {
    //                 // Show ghost ball at predicted hit point
    //                 GameObject ghost = Instantiate(ghostBallPrefab, hit.collider.transform.position, Quaternion.identity);
    //                 activeGhosts.Add(ghost);
    //                 break;
    //             }
    //             else
    //             {
    //                 // Reflect off wall
    //                 currentOrigin = hit.point + hit.normal * 0.01f;
    //                 currentDirection = Vector3.Reflect(currentDirection, hit.normal);
    //                 remainingDistance -= hit.distance;
    //             }
    //         }
    //         else
    //         {
    //             // No more hits; extend final segment
    //             pathPoints.Add(currentOrigin + currentDirection * remainingDistance);
    //             break;
    //         }
    //     }

    //     predictionLine.positionCount = pathPoints.Count;
    //     predictionLine.SetPositions(pathPoints.ToArray());
    // }

}
