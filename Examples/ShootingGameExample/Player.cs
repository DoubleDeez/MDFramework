using Godot;
using System;

[MDAutoRegister]
public class Player : KinematicBody2D
{
    [Export]
    public float MaxSpeed = 150f;
    [Export]
    public float Acceleration = 2000f;
    [Export]
    public float WeaponCooldown = 1f;

    [MDBindNode("Camera2D")]
    protected Camera2D Camera;

    [MDBindNode("HitCounter")]
    protected Label HitCounter;

    protected bool IsLocalPlayer = false;

    protected Vector2 MovementAxis = Vector2.Zero;
    protected Vector2 Motion = Vector2.Zero;

    protected float WeaponActiveCooldown = 0f;

    protected PackedScene BulletScene = null;

    protected int HitCounterValue = 0;

    // Networking code
    [MDBindNode("Networking")]
    MDClockedNetworkDataNode NetworkNode;
    protected MDCNetworkInterpolatedVector2 NetworkedPosition;

    protected MDClockedNetworkValue<Vector2> NetworkedShoot;

    protected Color _color;

    [MDReplicated(MDReliability.Reliable, MDReplicatedType.OnChange)]
    protected Color NetworkedColor {
        get
        {
            return _color;
        }
        set
        {
            _color = value;
            UpdateColor();
        }
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        SetupPlayer(GetNetworkMaster());
        NetworkedPosition = new MDCNetworkInterpolatedVector2(Position, IsLocalPlayer);
        NetworkedPosition.OnValueChangedEvent += OnPositionChanged;
        NetworkNode.AddValue(NetworkedPosition);

        NetworkedShoot = new MDClockedNetworkValue<Vector2>(Vector2.Zero, IsLocalPlayer, ClockedPropertyMode.ONE_SHOT, MDReliability.Reliable);
        NetworkedShoot.OnValueChangedEvent += OnShoot;
        NetworkNode.AddValue(NetworkedShoot);

        if (IsLocalPlayer)
        {
            RandomNumberGenerator rnd = new RandomNumberGenerator();
            rnd.Randomize();
            // Let's set our color
            NetworkedColor = new Color(rnd.Randf(), rnd.Randf(), rnd.Randf());
        }
    }

    protected void UpdateColor()
    {
        Modulate = NetworkedColor;
    }

    public void Hit()
    {
        HitCounterValue++;
        HitCounter.Text = HitCounterValue.ToString();
    }

    protected void OnPositionChanged(Vector2 newValue, Vector2 oldValue)
    {
        if (!IsLocalPlayer)
        {
            Position = newValue;
        }
    }

    protected void OnShoot(Vector2 newValue, Vector2 oldValue)
    {
        if (newValue != Vector2.Zero)
        {
            Bullet bullet = (Bullet)GetBulletScene().Instance();
            bullet.GlobalPosition = GlobalPosition;
            bullet.SetOwner(GetNetworkMaster());
            bullet.SetTarget(newValue);
            GetParent().AddChild(bullet);
        }
    }

    private PackedScene GetBulletScene()
    {
        if (BulletScene == null)
        {
            BulletScene = (PackedScene)ResourceLoader.Load(Filename.GetBaseDir() + "/Bullet.tscn");
        }
        return BulletScene;
    }

    public override void _PhysicsProcess(float delta)
    {
        if (IsLocalPlayer)
        {
            WeaponActiveCooldown -= delta;
            // Get input
            if (Input.IsMouseButtonPressed(1) && WeaponActiveCooldown <= 0f)
            {
                // Shoot towards mouse position
                NetworkedShoot.SetValue(GetGlobalMousePosition());
                WeaponActiveCooldown = WeaponCooldown;
            }
            MovementAxis = GetInputAxis();

            // Move
            if (MovementAxis == Vector2.Zero)
            {
                ApplyFriction(Acceleration * delta);
            }
            else
            {
                ApplyMovement(MovementAxis * Acceleration * delta, MaxSpeed);
            }
            Motion = MoveAndSlide(Motion);
            NetworkedPosition.SetValue(Position);
        }
    }

    protected virtual void ApplyMovement(Vector2 MovementSpeed, float Max)
    {
        this.Motion += MovementSpeed;
        this.Motion = Motion.Clamped(Max);
    }

    protected void ApplyFriction(float Amount)
    {
        if (Motion.Length() > Amount)
        {
            Motion -= Motion.Normalized() * Amount;
        }
        else
        {
            Motion = Vector2.Zero;
        }
    }
    
    protected Vector2 GetInputAxis()
    {
        Vector2 axis = Vector2.Zero;
        axis.x = IsActionPressed("ui_right") - IsActionPressed("ui_left");
        axis.y = IsActionPressed("ui_down") - IsActionPressed("ui_up");
        return axis.Normalized();
    }

    protected int IsActionPressed(String Action) 
    {
        if (Input.IsActionPressed(Action))
        {
            return 1;
        }
        return 0;
    }

    public void SetupPlayer(int PeerId)
    {
        if (PeerId == MDStatics.GetPeerId())
        {
            Camera.Current = true;
            IsLocalPlayer = true;
        }
    }
}
