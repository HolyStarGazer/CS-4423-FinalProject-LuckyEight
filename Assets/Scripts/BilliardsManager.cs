using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using UnityEngine;
using System.Collections;
using Unity.VisualScripting;

public class BilliardsManager : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public PhysicsManager physicsManager; 
    public PocketedBallUIController pocketedUI;
    public GameOverUIController gameOverUI; 

    [Header("Table")]
    public Transform tableTransform;
    public float tableWidth = 2.24f; // meters //2.84f
    public float tableHeight = 1.12f; // 1.42f
    public float cushionOffset = 0.03f; // 0.02f

    [Header("Billiard Spawn Points")]
    public Transform cueBallSpawnPoint;
    public Transform rackApexSpawnPoint;
    public Transform unusedSpawnPoint; // for future use

    [Header("Audio Settings")]
    public AudioSource AudioSource;
    public AudioClip []ballHitClips;
    public AudioClip intro;
    public AudioClip pocketClip;
    public AudioClip turnClip;

    [Header("Ball Tracking")]
    public List<Ball> balls;
    private List<Ball> ballsPocketedThisTurn = new List<Ball>();
    private bool firstCollisionOccured = false;
    private int firstCollisionBallID = -1;

    [Header("Ball-in-Hand Settings")]
    public bool allowBallInHand = false;
    public bool ballInHand = false;

    public enum Team { Neutral, Solids, Stripes }
    public Team currentTeam = Team.Neutral;
    public bool isTurnOngoing = false;
    public bool foulOccurred = false;
    public bool eightBallPocketed = false;

    private bool simulationPaused = true;
    public bool IsSimulationPaused => simulationPaused;

    public void PauseSimulation() => simulationPaused = true;
    public void ResumeSimulation() => simulationPaused = false;

    [System.Serializable]
    public class TurnSnapshot
    {
        public List<BallState> ballStates;
        public BilliardsManager.Team currentTeam;
        public bool foulOccurred;
        public bool eightBallPocketed;
        public int firstCollisionBallID;
        public List<int> ballsPocketedThisTurn;

        public class BallState
        {
            public int id;
            public Vector3 position;
            public Vector3 velocity;
            public Vector3 angularVelocity;
            public bool inPlay;
            public bool isMoving;
        }
    }

    void Awake()
    {
        // Copy pointers to the balls from the PhysicsManager
        balls = physicsManager.balls;

        // Initialize the game state
        pocketedUI.ResetUI();
        InitializeGame();
    }

    void OnDrawGizmos()
    {
        if (tableTransform == null) return;

        Gizmos.color = Color.yellow;
        Vector2 b = TableBounds;
        //Vector3 center = tableTransform.position;
        Vector3 size = tableTransform.TransformVector(new Vector3(b.x, 0.01f, b.y));//new Vector3(b.x * 2f, 0.01f, b.y * 2f);
        Gizmos.matrix = tableTransform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.identity; // Reset matrix to default to avoid affecting other Gizmos
    }

    // public void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.R))
    //     {
    //         RestartGame();
    //     }
    // }

    public Vector3 GetCueBallSpawnPoint() => tableTransform.TransformPoint(tableTransform.InverseTransformPoint(cueBallSpawnPoint.position));//cueBallSpawnPoint.position;
    public Vector3 GetRackApexSpawnPoint() => tableTransform.TransformPoint(tableTransform.InverseTransformPoint(rackApexSpawnPoint.position));//rackApexSpawnPoint.position;

    public void RestartGame()
    {
        // Reset game state
        currentTeam = Team.Neutral;
        isTurnOngoing = false;
        foulOccurred = false;
        eightBallPocketed = false;
        
        currentTeam = Team.Neutral;

        firstCollisionOccured = false;
        firstCollisionBallID = -1;
        ballsPocketedThisTurn.Clear();

        // Empty the undo/redo stacks
        undoStack.Clear();
        redoStack.Clear();

        pocketedUI.ResetUI();
        SaveTurn();
        InitializeGame();
    }

    public void InitializeGame()
    {
        Debug.Log("Pausing simulation for initialization...");
        // Pausing the simulation for initialization
        PauseSimulation();

        Debug.Log("Initializing game...");
        float ballRadius = balls[0].radius;
        float ballSpacing = (ballRadius * 2f) + 0.003f; // 3 mm spacing
        float rowHeight = ballSpacing * Mathf.Sqrt(3) / 2f;
        Vector3 rackOrigin = rackApexSpawnPoint.position + tableTransform.up * ballRadius; // Adjusted for table width and cushion offset

        int[] rackOrder = new int[] { // Read from right to left
            1,
            11, 4,
            3, 8, 14,
            6, 10, 9, 2,
            13, 7, 12, 15, 5
        };

        Dictionary<int, Vector3> rackPositions = new Dictionary<int, Vector3>();
        int rackIndex = 0;

        Debug.Log("Calculating rack positions...");
        for (int row = 0; row < 5; row++)
        {
            for  (int col = 0; col <= row; col++)
            {
                float xOffset = -(row * rowHeight);
                float zOffset = (col - row * 0.5f) * ballSpacing;
                Vector3 offset = (-rackApexSpawnPoint.forward * xOffset) + (rackApexSpawnPoint.right * zOffset);
                //float randomYOffset = Random.Range(0.05f, 0.75f); // Random Y offset for visual effect
                rackPositions[rackOrder[rackIndex++]] = rackOrigin + offset;
            }
        }

        foreach (Ball b in balls) Debug.Log($"Ball: {b.name}, ID: {b.ID}");
        
        Debug.Log("Placing balls in scene...");
        foreach (Ball ball in balls.OrderBy(b => b.ID))
        {
            //Debug.Log($"Placing ball {ball.ID} at position {rackPositions[ball.ID]}");
            if (ball.ID == 0)
            {
                Vector3 cue = GetCueBallSpawnPoint();
                ball.position = cue + tableTransform.up * ballRadius; // Adjusted for table width and cushion offset
            }
            else if (rackPositions.TryGetValue(ball.ID, out Vector3 pos))
            {
                ball.position = pos;
            }

            ball.velocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            ball.inPlay = true;
            ball.isMoving = false;
            ball.SyncTransform();
        }

        // Empty the undo/redo stacks
        undoStack.Clear();
        redoStack.Clear();

        AudioSource.PlayOneShot(intro);
        Debug.Log("Game initialized. Resuming simulation...");
        
        SaveTurn();
        ResumeSimulation();
    }

    public Vector2 TableBounds
    {
        get
        {
            float halfLength = tableWidth * 0.5f - cushionOffset - 0.0285f;
            float halfWidth = tableHeight * 0.5f - cushionOffset - 0.0285f;
            return new Vector2(halfLength, halfWidth);
        }
    }

    public void OnBallPocketed(Ball ball)
    {
        if (!ball.inPlay) return; // Ignore if already pocketed
        
        pocketedUI.OnBallPocketed(ball.ID);

        Debug.Log($"Ball {ball.ID} pocketed!");
        ballsPocketedThisTurn.Add(ball);
        ball.inPlay = false;

        if (ball.ID == 0) // Cue ball
        {
            Debug.LogWarning("Foul: Cue ball pocketed!");
            foulOccurred = true;
            return;
        }

        if (ball.ID == 8)
        {
            eightBallPocketed = true;
            return; // We'll check win/loss at the end of the turn
        }
    }

    public void ReportBallCollision(int ballA, int ballB)
    {
        if (firstCollisionOccured) return;

        int cueID = 0;
        int otherBallID = ballA == cueID ? ballB : (ballB == cueID ? ballA : -1);

        if (otherBallID != -1)
        {
            firstCollisionOccured = true;
            firstCollisionBallID = otherBallID;
            Debug.Log($"First collision with ball {firstCollisionBallID}");
        }
    }

    public void EndTurn()
    {
        Debug.Log("Evaluating end of turn...");

        foreach (var b in ballsPocketedThisTurn)
        {
            Debug.Log($"[Debug] Pocketed this turn: Ball {b.ID}, inPlay: {b.inPlay}");
        }

        Ball cueBall = balls.FirstOrDefault(b => b.ID == 0);
        if (cueBall != null && !cueBall.inPlay)
        {
            cueBall.position = GetCueBallSpawnPoint();
            cueBall.velocity = Vector3.zero;
            cueBall.angularVelocity = Vector3.zero;
            cueBall.inPlay = true;
            cueBall.isMoving = false;
            cueBall.SyncTransform();
            Debug.Log("Cue ball repositioned to spawn point.");
        }

        bool objectBallPocketed = ballsPocketedThisTurn.Any(b => b.ID != 0 && b.ID != 8);
        if (!objectBallPocketed && !eightBallPocketed)
        {
            Debug.LogWarning("Foul: No object ball pocketed!");
            foulOccurred = true;
        }

        if (currentTeam == Team.Neutral && !foulOccurred)
        {
            var solids = ballsPocketedThisTurn.Where(b => b.ID >= 1 && b.ID <= 7).Any();
            var stripes = ballsPocketedThisTurn.Where(b => b.ID >= 9 && b.ID <= 15).Any();

            if (solids && !stripes) currentTeam = Team.Solids;
            else if (stripes && !solids) currentTeam = Team.Stripes;
            else if (solids && stripes) currentTeam = Team.Neutral; // Remains open table
        }

        if (!eightBallPocketed) 
        {
            // Check for wrong team ball (foul)
            foreach (Ball b in ballsPocketedThisTurn)
            {
                if (b.ID == 8 || b.ID == 0) continue; // Ignore 8-ball and cue ball

                Team ballTeam = (b.ID >= 1 && b.ID <= 7) ? Team.Solids : Team.Stripes;
                if (ballTeam != currentTeam && currentTeam != Team.Neutral)
                {
                    Debug.LogWarning("Foul: Wrong team ball pocketed!");
                    foulOccurred = true;
                }
            }

            if (currentTeam != Team.Neutral && (foulOccurred || !objectBallPocketed))
            {
                currentTeam = currentTeam == Team.Solids ? Team.Stripes : Team.Solids;
                Debug.Log($"Turn switches due to foul or no object ball pocketed.");
                AudioSource.PlayOneShot(turnClip);
            }
        }
        
        if (eightBallPocketed)
        {
            CheckWinCondition();
        }

        ballsPocketedThisTurn.Clear();
        foulOccurred = false;
        isTurnOngoing = false;

        Debug.Log("End of Turn. Team: " + currentTeam);
        pocketedUI.SetTeam((PocketedBallUIController.Team)currentTeam);
        SaveTurn();
    }

    private bool ScoredThisTurn()
    {
        foreach (Ball ball in balls)
        {
            if (!ball.inPlay) continue; // Ignore pocketed balls
            if (ball.ID == 8) continue; // Ignore 8-ball

            if (currentTeam == Team.Solids && ball.ID >= 1 && ball.ID <= 7) return true;
            if (currentTeam == Team.Stripes && ball.ID >= 9 && ball.ID <= 15) return true;
        }
        return false;
    }

    private void CheckWinCondition()
    {
        if (currentTeam == Team.Neutral)
        {
            Debug.Log("You lose! You pocketed the 8 ball.");
            gameOverUI.ShowGameOver(false, "You pocketed the 8 ball without claiming a team.");
            return;
        }

        bool hasRemaining = balls.Any(ball => ball.inPlay && 
        ((currentTeam == Team.Solids && ball.ID >= 1 && ball.ID <= 7) ||
        (currentTeam == Team.Stripes && ball.ID >= 9 && ball.ID <= 15)));

        if (hasRemaining)
        {
            Debug.Log("You lose! You pocketed the 8 ball too early.");
            gameOverUI.ShowGameOver(false, "You pocketed the 8 ball too early.");
        }
        else if (foulOccurred)
        {
            Debug.Log("You lose! You pocketed the cue ball on the 8 ball.");
            gameOverUI.ShowGameOver(false, "You pocketed the cue ball on the 8 ball.");
        }
        else
        {
            Debug.Log("You win! You pocketed the 8 ball.");
            gameOverUI.ShowGameOver(true, "You pocketed the 8 ball.");
        }
    }

    public void CheckWallCollision(Ball ball)
    {
        float radius = ball.radius;
        Vector3 pos = ball.position;
        Vector3 vel = ball.velocity;

        Collider[] hits = Physics.OverlapSphere(pos, radius, LayerMask.GetMask("cushionMask"));
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Wall")) continue;

            //Debug.Log($"Ball {ball.ID} hit wall: {hit.name}");

            Vector3 closest = hit.ClosestPoint(pos);
            Vector3 normal = (pos - closest).normalized;

            if (normal.sqrMagnitude < 0.001f) continue;

            vel = Vector3.Reflect(vel, normal) * 0.9f; // Apply bounce dampening
            ball.velocity = vel;

            // Push ball out to avoid tunneling
            ball.position += normal * (radius - Vector3.Distance(pos, closest));
            ball.positionDirty = true;

            ball.angularVelocity *= 0.95f; // Simulate spin loss from wall impact

            break; // Stop after first valid cushion hit
        }
    }

    public void TriggerCollisionEvent(int ballA, int ballB)
    {
        // Handle collision event between two balls
        Debug.Log($"Collision between Ball {ballA} and Ball {ballB}");
    }

    /* UNDO/REDO SYSTEM */

    private Stack<TurnSnapshot> undoStack = new();
    private Stack<TurnSnapshot> redoStack = new();
    private TurnSnapshot lastSnapshot = null;
    private bool isUndoRedoLocked = false;

    public TurnSnapshot CreateSnapshot()
    {
        var snapshot = new TurnSnapshot
        {
            ballStates = balls.Select(ball => new TurnSnapshot.BallState
            {
                id = ball.ID,
                position = ball.position,
                velocity = ball.velocity,
                angularVelocity = ball.angularVelocity,
                inPlay = ball.inPlay,
                isMoving = ball.isMoving,
            }).ToList(),
            currentTeam = currentTeam,
            foulOccurred = foulOccurred,
            eightBallPocketed = eightBallPocketed,
            firstCollisionBallID = firstCollisionBallID,
            ballsPocketedThisTurn = new List<int>(ballsPocketedThisTurn.Select(b => b.ID)),
        };

        return snapshot;
    }

    public void RestoreSnapshot(TurnSnapshot snapshot)
    {
        PauseSimulation();
        foreach (var state in snapshot.ballStates)
        {
            var ball = balls.FirstOrDefault(b => b.ID == state.id);
            if (ball == null) continue;

            ball.position = state.position;
            ball.velocity = state.velocity;
            ball.angularVelocity = state.angularVelocity;
            ball.inPlay = state.inPlay;
            ball.isMoving = state.isMoving;
            ball.SyncTransform();
        }

        currentTeam = snapshot.currentTeam;
        foulOccurred = snapshot.foulOccurred;
        eightBallPocketed = snapshot.eightBallPocketed;
        firstCollisionBallID = snapshot.firstCollisionBallID;
        ballsPocketedThisTurn = snapshot.ballsPocketedThisTurn.Select(id => balls.FirstOrDefault(b => b.ID == id)).ToList();
        ResumeSimulation();
        Debug.Log("Game state restored from snapshot.");
    }

    public void SaveTurn()
    {
        var snapshot = CreateSnapshot();
        if (lastSnapshot != null && SnapshotsEqual(lastSnapshot, snapshot))
        {
            Debug.Log("No changes detected. Not saving snapshot.");
            return;
        }

        undoStack.Push(CreateSnapshot());
        redoStack.Clear();
        lastSnapshot = snapshot;
    }

    private bool SnapshotsEqual(TurnSnapshot a, TurnSnapshot b)
    {
        if (a.ballStates.Count != b.ballStates.Count) return false;

        for (int i = 0; i < a.ballStates.Count; i++)
        {
            var ba = a.ballStates[i];
            var bb = b.ballStates[i];

            if (ba.id != bb.id || ba.position != bb.position || ba.velocity != bb.velocity || ba.angularVelocity != bb.angularVelocity || ba.inPlay != bb.inPlay || ba.isMoving != bb.isMoving)
            {
                return false;
            }
        }

        return a.currentTeam == b.currentTeam && a.foulOccurred == b.foulOccurred && a.eightBallPocketed == b.eightBallPocketed && a.firstCollisionBallID == b.firstCollisionBallID && a.ballsPocketedThisTurn.SequenceEqual(b.ballsPocketedThisTurn);
    }

    public void UndoTurn()
    {
        if (isUndoRedoLocked) return; 

        if (undoStack.Count <= 1) // Only the initial state remains
        {   
            Debug.LogWarning("No more turns to undo.");
            return;
        }

        isUndoRedoLocked = true;
        StartCoroutine(UndoTurnRoutine());
    }

    public void RedoTurn()
    {
        if (isUndoRedoLocked) return;

        if (redoStack.Count == 0)
        {
            Debug.LogWarning("No more turns to redo.");
            return;
        }

        isUndoRedoLocked = true;
        StartCoroutine(RedoTurnRoutine());
    }

    IEnumerator UndoTurnRoutine()
    {
        TurnSnapshot current = undoStack.Pop();
        redoStack.Push(current);

        RestoreSnapshot(undoStack.Peek());

        yield return new WaitForSecondsRealtime(0.2f);
        isUndoRedoLocked = false;
    }

    IEnumerator RedoTurnRoutine()
    {
        TurnSnapshot next = redoStack.Pop();
        undoStack.Push(CreateSnapshot());
        RestoreSnapshot(next);

        yield return new WaitForSecondsRealtime(0.2f);
        isUndoRedoLocked = false;
    }
}