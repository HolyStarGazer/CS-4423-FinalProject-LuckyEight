using UnityEngine;

public class CameraAngle : MonoBehaviour
{
    [Header("Camera Properties")]
    [SerializeField] public bool followCueBall;
    [SerializeField] public Vector3 CameraPosition;
    [SerializeField] public Vector3 CameraRotation; 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CameraPosition = transform.position;
        CameraRotation = transform.eulerAngles;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
