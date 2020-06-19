using Godot;
using System;

[MDAutoRegister]
public class HidingIcon : Area2D
{
    public static readonly String GROUP_ACTORS = "ACTORS";

	[MDBindNode("Sprite")]
	Sprite Icon;

	[MDBindNode("Networking")]
	MDClockedNetworkDataNode NetworkNode;

	protected RandomNumberGenerator Random = new RandomNumberGenerator();

	protected MDClockedNetworkValue<Boolean> NetworkedVisible;

	protected MDCNetworkInterpolatedVector2 NetworkedPosition;

	protected MDClockedNetworkValue<Vector2> NetworkedScale;

	protected float Radius = 100f;
	protected float RotationSpeed = 20f;
	protected float Angle = 0f;

	protected Vector2 CenterPosition = Vector2.Zero;


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		AddToGroup(GROUP_ACTORS);
		// Setup network variables
		NetworkedVisible = new MDClockedNetworkValue<Boolean>(true, !this.IsClient(), ClockedPropertyMode.ON_CHANGE, MDReliability.Reliable);
		NetworkedVisible.OnValueChangedEvent += OnVisibilityChanged;
		NetworkNode.AddValue(NetworkedVisible);

        NetworkedPosition = new MDCNetworkInterpolatedVector2(Position, !this.IsClient());
		NetworkedPosition.OnValueChangedEvent += OnPositionChanged;
		NetworkNode.AddValue(NetworkedPosition);

		NetworkedScale = new MDClockedNetworkValue<Vector2>(Vector2.One, !this.IsClient(), ClockedPropertyMode.ON_CHANGE, MDReliability.Reliable);
		NetworkedScale.OnValueChangedEvent += OnScaleChanged;
		NetworkNode.AddValue(NetworkedScale);
        

		NetworkNode.SetUpdateInterval(0.1f);

		if (!this.IsClient())
		{
			Random.Randomize();
			CenterPosition = Position + new Vector2(Random.RandfRange(-2000f, 2000f), Random.RandfRange(-1000f, 1000f));
			Radius = Random.RandfRange(100f, 500f);
			RotationSpeed = Random.RandfRange(0.1f, 1.5f);
			float scale = Random.RandfRange(2f, 4f);
			NetworkedScale.SetValue(new Vector2(scale, scale));
		}
	}

	public override void _PhysicsProcess(float delta)
	{
		if (this.IsClient())
		{
			SetProcess(false);
			return;
		}
		Angle += RotationSpeed * delta;

		Vector2 offset = new Vector2(Mathf.Sin(Angle), Mathf.Cos(Angle)) * Radius;
		NetworkedPosition.SetValue(CenterPosition + offset);
	}

	protected void OnPositionChanged(Vector2 newValue, Vector2 oldValue)
	{
		Position = newValue;
	}

	protected void OnScaleChanged(Vector2 newValue, Vector2 oldValue)
	{
		Scale = newValue;
	}

	protected void OnVisibilityChanged(bool newValue, bool oldValue)
	{
		Icon.Visible = newValue;
	}

	private void OnMouseEntered()
	{
		if (!MDStatics.IsClient())
		{
			NetworkedVisible.SetValue(false);
		}
	}


	private void OnMouseExited()
	{
		if (!MDStatics.IsClient())
		{
			NetworkedVisible.SetValue(true);
		}
	}

}


