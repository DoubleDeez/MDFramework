using Godot;
using MD;

/*
    A simple predictive actor that always behaves the same if it is synched
*/
[MDAutoRegister]
public class PredictiveActor : KinematicBody2D, IMDSynchronizedNode
{
    public static readonly string GROUP_ACTORS = "PredictiveActors";

    [MDReplicated]
    protected float Speed = 0f;

    [MDReplicated]
    protected Vector2 Direction = Vector2.Zero;

    protected Vector2 DirectionInt = Vector2.Zero;

    protected uint StartAt = 1;

    protected uint FinishSynchAt = 0;

    protected RandomNumberGenerator Random = new RandomNumberGenerator();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Random.Randomize();

        AddToGroup(GROUP_ACTORS);
        if (MDStatics.IsClient())
        {
            return;
        }

        Direction = new Vector2(Random.RandfRange(-1f, 1f), Random.RandfRange(-1f, 1f));
        Speed = Random.RandfRange(1f, 4f);
        Direction = Direction.Normalized() * Speed;
        SetStartTime(2000);
        this.GetGameSession().OnPlayerJoinedEvent += OnPlayerJoinedEvent;
    }

    public override void _ExitTree()
    {
        this.GetGameSession().OnPlayerJoinedEvent -= OnPlayerJoinedEvent;
    }

    private void OnPlayerJoinedEvent(int PeerId)
    {
        if (!this.IsClient())
        {
            // Update direction so new client gets the correct direction
            Direction = DirectionInt;
        }
    }

    public void SetStartTime(uint delay)
    {
        if (MDStatics.IsClient())
        {
            return;
        }

        foreach (int peerid in this.GetGameSession().GetAllPeerIds())
        {
            if (peerid == MDStatics.GetPeerId())
            {
                // Set our start time
                StartAt = OS.GetTicksMsec() + delay;
            }
            else
            {
                // Set start time for other clients
                RpcId(peerid, nameof(RpcSetStartTime), this.GetPlayerTicksMsec(peerid) + delay);
            }
        }
    }

    [Puppet]
    public void RpcSetStartTime(uint StartTime)
    {
        StartAt = StartTime;
    }

    public override void _PhysicsProcess(float delta)
    {
        if (StartAt > OS.GetTicksMsec() || StartAt == 0)
        {
            return;
        }

        if (DirectionInt == Vector2.Zero)
        {
            DirectionInt = Direction;
        }

        KinematicCollision2D col = MoveAndCollide(DirectionInt * Speed);
        if (col != null)
        {
            DirectionInt = DirectionInt.Bounce(col.Normal);
        }
    }

    public bool IsSynchronizationComplete()
    {
        if (Speed == 0f || Direction == Vector2.Zero)
        {
            // Fake that we are taking variable time to synch
            FinishSynchAt = OS.GetTicksMsec() + (uint) Random.RandiRange(500, 8000);
            return false;
        }

        if (OS.GetTicksMsec() < FinishSynchAt)
        {
            return false;
        }

        return true;
    }
}