using Godot;
using System;
using MD;

[MDAutoRegister]
public class Player : KinematicBody2D
{
    public const string PLAYER_GROUP = "PLAYERS";

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

    protected float RsetActiveCooldown = 0f;

    protected PackedScene BulletScene = null;

    protected int HitCounterValue = 0;

    [MDReplicated(MDReliability.Unreliable, MDReplicatedType.Interval)]
    [MDReplicatedSetting(MDReplicator.Settings.GroupName, "PlayerPositions")]
    [MDReplicatedSetting(MDReplicator.Settings.ProcessWhilePaused, false)]
    [MDReplicatedSetting(MDClockedReplicatedMember.Settings.OnValueChangedEvent, nameof(OnPositionChanged))]
    protected Vector2 NetworkedPosition;

    [MDReplicated(MDReliability.Reliable, MDReplicatedType.OnChange)]
    [MDReplicatedSetting(MDClockedReplicatedMember.Settings.OnValueChangedEvent, nameof(UpdateColor))]
    protected Color NetworkedColor { get; set; }

    [Puppet]
    protected String RsetTest = "";

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        AddToGroup(PLAYER_GROUP);
        SetupPlayer(GetNetworkMaster());

        if (IsLocalPlayer)
        {
            RandomNumberGenerator rnd = new RandomNumberGenerator();
            rnd.Randomize();

            // Let's set our color
            NetworkedColor = new Color(rnd.Randf(), rnd.Randf(), rnd.Randf());
            Modulate = NetworkedColor;
        }
        else
        {
            MDOnScreenDebug.AddOnScreenDebugInfo("RsetTest " + GetNetworkMaster().ToString(),
                () => { return RsetTest; });
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

    protected void OnPositionChanged()
    {
        if (!IsLocalPlayer)
        {
            Position = NetworkedPosition;
        }
    }

    [Remote]
    protected void OnShoot(Vector2 Target)
    {
        if (Target != Vector2.Zero)
        {
            Bullet bullet = (Bullet) GetBulletScene().Instance();
            bullet.GlobalPosition = GlobalPosition;
            bullet.SetOwner(GetNetworkMaster());
            GetParent().AddChild(bullet);
            bullet.SetTarget(Target);
        }
    }

    private PackedScene GetBulletScene()
    {
        if (BulletScene == null)
        {
            BulletScene = (PackedScene) ResourceLoader.Load(Filename.GetBaseDir() + "/Bullet.tscn");
        }

        return BulletScene;
    }

    public override void _PhysicsProcess(float delta)
    {
        if (IsLocalPlayer)
        {
            WeaponActiveCooldown -= delta;
            RsetActiveCooldown -= delta;
            // Get input
            if (Input.IsMouseButtonPressed(1) && WeaponActiveCooldown <= 0f)
            {
                // Shoot towards mouse position
                this.MDRpc(nameof(OnShoot), GetGlobalMousePosition());

                // Call it on local client, could do with RemoteSynch as well but then it won't work in standalone
                OnShoot(GetGlobalMousePosition());
                WeaponActiveCooldown = WeaponCooldown;
            }
            else if (Input.IsMouseButtonPressed(2) && RsetActiveCooldown <= 0f)
            {
                RandomNumberGenerator rnd = new RandomNumberGenerator();
                rnd.Randomize();
                this.MDRset(nameof(RsetTest), rnd.RandiRange(0, 100000).ToString());
                RsetActiveCooldown = 0.1f;
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
            NetworkedPosition = Position;
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