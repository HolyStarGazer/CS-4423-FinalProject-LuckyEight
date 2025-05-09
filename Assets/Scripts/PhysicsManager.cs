using System;
using System.Collections.Generic;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEngine;
using UnityEngine.Rendering;

public class PhysicsManager : MonoBehaviour
{
    [Header("References")]
    public List<Ball> balls;
    public BilliardsManager billiardsManager;
    public GameManager gameManager;

    [Header("Physics Settings")]
    public float minVelocity = 0.001f;
    public const float k_FIXED_TIME_STEP = 0.0125f; // 1f / 80f Fixed time step for physics updates 
    public const float k_MAX_DELTA = k_FIXED_TIME_STEP * 6; // Maximum delta time for physics updates

    [Header("Friction Settings")]
    public float rollingSyncThreshold = 0.1f; // How close velocity and spin must be to count as rolling.

    public float sleepThreshold = 0.003f; // Minimum speed before stopping the ball completely
    public float slideFriction = 0.2f;
    public float rollFriction = 0.01f;
    public float rollingThreshold = 0.05f;

    public float slidingDeceleration = 0.25f; // 0.4 How strong friction is when sliding (more aggressive).
    public float rollingDeceleration = 0.008f; // 0.02 How strong friction is when rolling (gentle).
    
    private const float kGravity = 9.80665f;
    private const float kMuRoll = 0.01f;
    private const float kMuSlide = 0.2f;

    private float rollingFrictionAccel => -kMuRoll * kGravity * k_FIXED_TIME_STEP;
    private float slidingFrictionAccel => -kMuSlide * kGravity * k_FIXED_TIME_STEP;
    private float slidingAngularAccel => -(5f * kMuSlide * kGravity) / (2f * balls[0].radius) * k_FIXED_TIME_STEP; // 5/2 for a sphere

    private float accumulatedTime;
    private float lastTimestamp;

    [Header("Pockets")]
    public LayerMask pocketMask;

    [Header("Debug")]
    public bool trainingMode = false;

    void Awake()
    {
        //ballRadius = balls[0].radius; // Ensure we are using the same radius as the balls for calculations
    }

    private void FixedUpdate()
    {
        float now = Time.timeSinceLevelLoad;
        float delta = now - lastTimestamp;
        lastTimestamp = now;

        if (billiardsManager.IsSimulationPaused) return; 

        float newAccumulatedTime = Mathf.Clamp(accumulatedTime + Time.fixedDeltaTime, 0, k_MAX_DELTA);
        while (newAccumulatedTime >= k_FIXED_TIME_STEP)
        {
            Simulate();
            newAccumulatedTime -= k_FIXED_TIME_STEP;
        }
        
        accumulatedTime = newAccumulatedTime;
    }

    /*
    Simulation divided into discrete steps for better control and accuracy.
    1. Update ball positions and handle collisions.
    2. Update ball velocities
    */
    public void Simulate()
    {
        // Update ball positions
        foreach (var ball in balls)
        {
            if (!ball.inPlay) continue;
            ball.position += ball.velocity * k_FIXED_TIME_STEP;
            ball.positionDirty = true;
            billiardsManager.CheckWallCollision(ball);
        }

        // Handle collisions
        HandleBallCollisions();     // Correct positions and velocites based on collisions
        ResolveResidualOverlaps();  // Fix any leftover overlaps after collisions

        // Update ball velocities
        foreach (var ball in balls)
        {
            if (!ball.inPlay) continue;
            CheckPocket(ball); 
            UpdateBallVelocity(ball);

            // Off-table check
            Vector3 localPos = billiardsManager.tableTransform.InverseTransformPoint(ball.position); //billiardsManager.transform.InverseTransformPoint(ball.position);
            Vector2 bounds = billiardsManager.TableBounds;
            float margin = ball.radius;
            if (Mathf.Abs(localPos.x) > bounds.x + margin || Mathf.Abs(localPos.z) > bounds.y + margin)
            {
                Debug.LogWarning($"Ball {ball.ID} went off the table!");

                ball.position = billiardsManager.GetCueBallSpawnPoint();
                ResetBallVelocity(ball);
                ball.SyncTransform();

                billiardsManager.OnBallPocketed(ball);

                if (ball.ID == 0)
                {
                    ball.position = billiardsManager.GetCueBallSpawnPoint();
                    ball.inPlay = true;
                    ball.SyncTransform();
                }
                else
                {
                    ball.position = billiardsManager.unusedSpawnPoint.position;
                    ball.inPlay = false;
                    ball.SyncTransform();
                }

                continue;
            }

            if (ball.positionDirty || ball.isMoving)
            {
                ball.SyncTransform();
                ball.positionDirty = false;
            }
        }

        if (!gameManager.AnyBallMoving() && billiardsManager.isTurnOngoing)
        {
            billiardsManager.EndTurn();
        }
    }
    private void UpdateBallVelocity(Ball ball)
    {
        if (!ball.inPlay) return;

        Vector3 V = ball.velocity;
        Vector3 W = ball.angularVelocity;
        Vector3 Vxz = new Vector3(V.x, 0f, V.z);

        float tableY = billiardsManager.tableTransform.position.y;
        float ballGroundY = tableY + ball.radius;
        bool grounded = ball.position.y <= ballGroundY + 0.0001f && V.y <= 0f;;

        if (grounded)
        {
            ball.position.y = ballGroundY;
            V.y = 0f;

            Vector3 contactPoint = Vector3.down * ball.radius;
            Vector3 cv = Vxz + Vector3.Cross(contactPoint, W);
            float cvMag = cv.magnitude;

            if (cvMag <= 0.1f) // Rolling
            {
                if (Vxz.sqrMagnitude > 1e-6f)
                    Vxz += rollingFrictionAccel * Vxz.normalized;

                // Sync spin with velocity (v ≈ ω × r)
                W.x = -Vxz.z / ball.radius;
                W.z = Vxz.x / ball.radius;

                // Bleed off Y spin slowly
                if (Mathf.Abs(W.y) > 0.3f)
                    W.y -= Mathf.Sign(W.y) * 0.3f;
                else
                    W.y = 0f;

                // Check if fully stopped
                if (Vxz.sqrMagnitude < 0.0001f && W.sqrMagnitude < 0.04f)
                {
                    ResetBallVelocity(ball);
                    return;
                }
            }
            else // Sliding
            {
                Vector3 nv = cv / cvMag;
                W += slidingAngularAccel * Vector3.Cross(Vector3.up, nv);
                Vxz += slidingFrictionAccel * nv;
            }

            V.x = Vxz.x;
            V.z = Vxz.z;
        }
        else // Airborne
        {
            V.y -= kGravity * k_FIXED_TIME_STEP;

            float floorY = billiardsManager.tableTransform.position.y + ball.radius;
            float verticalThreshold = 0.002f;

            if (ball.position.y <= floorY + verticalThreshold)
            {
                if (V.y < -0.3f) // Enough energy to bounce
                {
                    V.y = -V.y * 0.2f;
                    ball.position.y = floorY;
                    //Debug.Log("Ball bounced on table");
                }
                else if (Mathf.Abs(V.y) < 0.01f) // Not enough energy to bounce
                {
                    V.y = 0f;
                    ball.position.y = floorY;
                    //Debug.Log("🛬 Ball landed");
                }
            }
        }

        ball.velocity = V;
        ball.angularVelocity = W;
        ball.isMoving = true;

        if (ball.velocity.magnitude <sleepThreshold && ball.angularVelocity.magnitude < sleepThreshold)
        {
            ResetBallVelocity(ball);
            return;
        }
    }

    private void HandleBallCollisions()
    {
        int count = balls.Count;
        for (int i = 0; i < count; i++)
        {
            Ball a = balls[i];
            if (!a.inPlay) continue;

            for (int j = i + 1; j < balls.Count; j++)
            {
                Ball b = balls[j];
                if (!b.inPlay) continue;

                if (WillBallsCollide(a, b, k_FIXED_TIME_STEP, out float tCollision))
                {
                    Vector3 futurePosA = a.position + a.velocity * tCollision;
                    Vector3 futurePosB = b.position + b.velocity * tCollision;
                    Vector3 delta = futurePosB - futurePosA;
                    Vector3 normal = delta.normalized;

                    float penetration = (a.radius + b.radius - delta.magnitude) * 0.5f;
                    a.position -= normal * penetration;
                    b.position += normal * penetration;

                    a.positionDirty = true;
                    b.positionDirty = true;

                    ResolveBallCollision(a, b, normal);
                    Debug.DrawLine(a.position, b.position, Color.red, 0.1f);
                }

                // Vector3 delta = b.position - a.position;
                // float dist = delta.magnitude;
                // float minDist = a.radius + b.radius;

                // if (dist < minDist && dist > 0f)
                // {
                //     Vector3 normal = delta.normalized;
                //     float penetration = (minDist - dist) * 0.5f;
                //     a.position -= normal * penetration * (1f / a.mass);
                //     b.position += normal * penetration * (1f / b.mass);

                //     a.positionDirty = true;
                //     b.positionDirty = true;

                //     ResolveBallCollision(a, b, normal);
                //     Debug.DrawLine(a.position, b.position, Color.green, 0.1f);
                // }
            }
        }
    }

    private bool WillBallsCollide(Ball a, Ball b, float dt, out float tCollision)
    {
        Vector3 relPos = b.position - a.position;
        Vector3 relVel = b.velocity - a.velocity;
        float radiusSum = a.radius + b.radius;

        float aCoeff = Vector3.Dot(relVel, relVel);
        float bCoeff = 2f * Vector3.Dot(relPos, relVel);
        float cCoeff = Vector3.Dot(relPos, relPos) - radiusSum * radiusSum;

        float discriminant = bCoeff * bCoeff - 4f * aCoeff * cCoeff;
        if (discriminant < 0f) // No collision
        {
            tCollision = float.MaxValue;
            return false;
        }

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float t1 = (-bCoeff - sqrtDiscriminant) / (2f * aCoeff);
        float t2 = (-bCoeff + sqrtDiscriminant) / (2f * aCoeff);

        tCollision = (t1 >= 0f && t1 <= dt) ? t1 : ((t2 >= 0f && t2 <= dt) ? t2 : -1f);
        return tCollision >= 0f && tCollision <= dt;
    }

    private void ResolveResidualOverlaps()
{
    for (int i = 0; i < balls.Count; i++)
    {
        Ball a = balls[i];
        if (!a.inPlay) continue;

        for (int j = i + 1; j < balls.Count; j++)
        {
            Ball b = balls[j];
            if (!b.inPlay) continue;

            Vector3 delta = b.position - a.position;
            float dist = delta.magnitude;
            float minDist = a.radius + b.radius;

            if (dist < minDist && dist > 0.0001f)
            {
                Vector3 normal = delta.normalized;
                float penetration = (minDist - dist) * 0.5f;

                a.position -= normal * penetration;
                b.position += normal * penetration;

                a.positionDirty = true;
                b.positionDirty = true;
            }
        }
    }
}

    private void ResolveBallCollision(Ball a, Ball b, Vector3 normal)
    {
        Vector3 relativeVelocity = b.velocity - a.velocity;
        float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

        if (velocityAlongNormal > 0f) return; // No collision

        // Cue ball involvement check
        if (a.ID == 0 || b.ID == 0)
        {
            billiardsManager.ReportBallCollision(a.ID, b.ID);
        }

        float speed = relativeVelocity.magnitude;
        float dynamicRestitution = Mathf.Lerp(0.85f, 0.98f, Mathf.InverseLerp(0f, 2.5f, speed));        float invMassA = 1f / a.mass;
        float invMassB = 1f / b.mass;

        if (Mathf.Abs(velocityAlongNormal) < 0.02f) return; // Avoid jitter from micro-impacts

        // Impulse scalar
        float impulseMag = -(1f + dynamicRestitution) * velocityAlongNormal / (invMassA + invMassB);
        Vector3 impulse = impulseMag * normal;

        if (a.velocity.magnitude > b.velocity.magnitude)
            a.PlayHitSound();
        else
            b.PlayHitSound();

        // Apply linear velocity
        a.velocity -= impulse * invMassA;
        b.velocity += impulse * invMassB;

        // Apply tangential friction and spin transfer
        Vector3 tangent = (relativeVelocity - velocityAlongNormal * normal).normalized;
        float frictionCoefficient = 0.02f; // Adjust as needed

        float frictionImpulseMag = frictionCoefficient * impulseMag;
        Vector3 frictionImpulse = frictionImpulseMag * tangent;

        a.angularVelocity -= Vector3.Cross(normal, frictionImpulse) / a.radius;
        b.angularVelocity += Vector3.Cross(normal, frictionImpulse) / b.radius;

        //table.TriggerCollisionEvent(a.ID, b.ID); // Notify table of collision
    }

    private void CheckPocket(Ball ball)
    {
        float checkHeight = ball.radius;
        Vector3 top = ball.position + Vector3.up * checkHeight;
        Vector3 botttom = ball.position - Vector3.up * checkHeight;
        float radius = ball.radius * 0.85f;

        Collider[] hits = Physics.OverlapCapsule(top, botttom, radius, pocketMask);

        if (hits.Length == 0)
        {
            // Check for a sphere overlap if no capsule hits were found
            hits = Physics.OverlapSphere(ball.position, radius, pocketMask);
        }

        foreach (var hit in hits)
        {
            //Debug.Log($"[Pocket] Hit: {hit.name}");

            if (hit.CompareTag("Pocket"))
            {
                Debug.Log($"Ball {ball.ID} pocketed into {hit.name}");
                ResetBallVelocity(ball);
                ball.position = billiardsManager.unusedSpawnPoint.position;
                //ball.inPlay = false;
                ball.SyncTransform();

                billiardsManager.OnBallPocketed(ball);
                if (billiardsManager.pocketClip != null)
                    billiardsManager.AudioSource.PlayOneShot(billiardsManager.pocketClip, 0.5f);
                return; // Exit after pocketing the ball
            }
        }
    }

    private bool IsRolling(Ball ball)
    {
        if (ball.velocity.magnitude < sleepThreshold) return false;

        // For a rolling ball, v ≈ ω × r (approximate). So compare expected vs actual.
        Vector3 idealAngular = new Vector3(ball.velocity.z, 0f, -ball.velocity.x) / ball.radius;
        return (ball.angularVelocity - idealAngular).magnitude < rollingSyncThreshold;
    }

    private void ResetBallVelocity(Ball ball)
    {
        ball.velocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        ball.isMoving = false;
    }

    private Vector3 GetPocketDropPosition(string pocketName)
    {
        return new Vector3(0, -1f, 0); // Placeholder for actual pocket drop position (below table)
    }
}
