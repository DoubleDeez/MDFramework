using Godot;
using MD;


public class Bullet : KinematicBody2D
{
    [Export]
    public float Speed = 300f;

    protected int OwnerPeerId = -1;

    protected Vector2 MovementDirection = Vector2.Right;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
    }

    public override void _PhysicsProcess(float delta)
    {
        MoveAndSlide(MovementDirection * Speed);
    }

    private void OnTimerTimeout()
    {
        this.RemoveAndFree();
    }

    public void SetOwner(int Owner)
    {
        OwnerPeerId = Owner;
    }

    public void SetTarget(Vector2 Target)
    {
        LookAt(Target);
        MovementDirection = (Target - GlobalPosition).Normalized();
    }

    private void OnBodyEntered(object body)
    {
        if (body is Player)
        {
            Player player = ((Player) body);
            if (player.GetNetworkMaster() != OwnerPeerId)
            {
                player.Hit();
                CallDeferred(nameof(Destroy));
            }
        }
    }

    private void Destroy()
    {
        this.RemoveAndFree();
    }
}