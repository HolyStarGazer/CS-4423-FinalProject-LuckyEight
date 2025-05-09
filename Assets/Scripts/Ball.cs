using UnityEngine;

public class Ball : MonoBehaviour
{
    [Header("Ball Info")]
    public int ID;
    public bool inPlay = true;
    public bool isMoving = false;
    [HideInInspector] public bool positionDirty = false; // for syncing with the server

    [Header("Ball Physics")]
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 angularVelocity;

    public float radius = 0.0285f; // 2.85 cm radius for a standard billiard ball
    public float mass = 0.16f; // 160 grams for a standard billiard ball

    private float momentOfInertia; // Moment of inertia for a sphere: (2/5) * m * r^2

    public AudioSource audioSource;
    public AudioClip []hitSounds;

    public const float k_FIXED_TIME_STEP = 0.0125f;
    

    void Awake()
    {
        position = transform.position;
        momentOfInertia = (2f / 5f) * mass * radius * radius;
        audioSource = GetComponent<AudioSource>();
    }

    public void SyncTransform()
    {
        transform.position = position;
        
        if (angularVelocity.sqrMagnitude >= 1e-6f)
        {
            //Debug.Log($"Rotating ball {ID} with angular velocity: {angularVelocity}");

            Vector3 axis = angularVelocity.normalized;
            float angle = angularVelocity.magnitude * k_FIXED_TIME_STEP * Mathf.Rad2Deg;
            transform.Rotate(axis, -angle, Space.World);
            Debug.DrawRay(transform.position, angularVelocity * 0.2f, Color.green);
        }
    }

    public void ApplyCueStrike(Vector3 aimDir, float cueSpeed, Vector2 impactOffsetNormal, float jumpAngle, float cueMass = 0.5f)
    {
        float rSquared = radius * radius;

        float a = impactOffsetNormal.x * radius;
        float b = impactOffsetNormal.y * radius;
        float c = Mathf.Max(Mathf.Sqrt(Mathf.Clamp(rSquared - a * a - b * b, 0f, rSquared)), 0.001f);

        Quaternion elevation = Quaternion.AngleAxis(jumpAngle, Vector3.right);
        Vector3 elevatedDir = elevation * aimDir.normalized;

        float sinTheta = elevatedDir.y;
        float cosTheta = new Vector2(elevatedDir.x, elevatedDir.z).magnitude;

        float sinTheta2 = sinTheta * sinTheta;
        float cosTheta2 = cosTheta * cosTheta;

        float F = (2 * mass * cueSpeed) / (1 + (mass / cueMass) + (5f / (2f * radius)) *
            (a * a + b * b * cosTheta2 + c * c * sinTheta2 - 2 * b * c * cosTheta * sinTheta));

        Vector3 v_local = new Vector3(0f, F / mass * sinTheta, F / mass * cosTheta); // Shoots normally

        Vector3 w_local = new Vector3(
            (-c * F * sinTheta + b * F * cosTheta) / momentOfInertia,
            (a * F * sinTheta) / momentOfInertia,
            (-a * F * cosTheta) / momentOfInertia
        );

        Quaternion cueRotation = Quaternion.LookRotation(elevatedDir, Vector3.up);
        velocity = cueRotation * v_local;
        angularVelocity = cueRotation * new Vector3(-w_local.x, w_local.y, -w_local.z); // flip x/z

        float maxSpeed = 10f;
        if (velocity.magnitude > maxSpeed)
        {
            velocity = velocity.normalized * maxSpeed;
            Debug.Log($"Speed capped to {maxSpeed} m/s");
        }

        // Jump logic (BetaPhysicsManager style)
        if (velocity.y < 0f)
        {
            float minHorizontalVel = (radius - c) / k_FIXED_TIME_STEP;
            Vector3 v_horizontal = new Vector3(velocity.x, 0f, velocity.z);

            if (v_horizontal.magnitude < minHorizontalVel)
            {
                velocity.y = 0f;
                //Debug.Log($"⛔ Jump blocked — horizontal velocity too low (wanted {minHorizontalVel:F3}, got {v_horizontal.magnitude:F3})");
            }
            else
            {
                velocity.y = -velocity.y * 0.35f; // dampen jump
                //Debug.Log("✅ Jump shot velocity applied");

            }
        }

        isMoving = true;

        Debug.DrawRay(transform.position, velocity * 0.5f, Color.cyan, 1f);
        Debug.DrawRay(transform.position, angularVelocity.normalized * 0.3f, Color.magenta, 1f);
    }




    public void ResetBallVelocity()
    {
        position = transform.position;
        velocity = Vector3.zero;
        angularVelocity = Vector3.zero;
        isMoving = false;
        inPlay = true;
        positionDirty = false;
    } 

    public void PlayHitSound()
    {
        float volume = Mathf.Clamp01(velocity.magnitude / 2f);
        if (hitSounds.Length > 0 && audioSource != null)
        {
            int randomIndex = Random.Range(0, hitSounds.Length);
            audioSource.PlayOneShot(hitSounds[randomIndex], volume);
        }
    }
}