using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public Transform cueBall;
    [Header("Camera Properties")]
    public float rotationSpeed = 5f;
    public float zoomSpeed = 5;
    public float minDistance = 0.5f;
    public float maxDistance = 3f;

    // List of camera positions
    //private int currentCameraIndex = 0;
    [SerializeField] public CameraAngle[] cameraAngles;

    private float currentDistance;
    private Vector3 offset;
    private float yaw = 0f;
    private float pitch = 20f; // Slight downward angle

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentDistance = (transform.position - cueBall.position).magnitude;
        offset = transform.position - cueBall.position;
    }

    // Update is called once per frame
    void Update()
    {
        // if (cameraAngles[currentCameraIndex].followCueBall)
        // {
        //     LookAtCueBall();
        // }
        // transform.position = cameraAngles[currentCameraIndex].CameraPosition;
        // transform.eulerAngles = cameraAngles[currentCameraIndex].CameraRotation;

        if (gameManager.IsMenuOpen)
        {
            return; // Don't update camera if menu is open
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw += rotationSpeed * Input.GetAxis("Mouse X");
        pitch -= rotationSpeed * Input.GetAxis("Mouse Y");
        pitch = Mathf.Clamp(pitch, 0, 90);

        // Zoom with scroll wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        currentDistance -= scroll * zoomSpeed;
        currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

        // Update position
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 newPos = cueBall.position + rotation * offset.normalized * currentDistance;
        transform.position = newPos;
        transform.LookAt(cueBall.position);
    }

    // public void FollowCueBall()
    // {
    //     transform.position = cueBall.position + offset;
    // }

    // public void LookAtCueBall()
    // {
    //     transform.LookAt(cueBall.position);
    // }

    // public void CycleCamera()
    // {
    //     currentCameraIndex++;
    //     if (currentCameraIndex >= cameraAngles.Length)
    //     {
    //         currentCameraIndex = 0;
    //     }
    // }
}
