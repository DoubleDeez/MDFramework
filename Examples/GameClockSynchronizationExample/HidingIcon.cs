using Godot;
using System;

[MDAutoRegister]
public class HidingIcon : Area2D
{
	public static readonly String GROUP_ACTORS = "ACTORS";

	[MDBindNode("Sprite")]
	Sprite Icon;

	protected RandomNumberGenerator Random = new RandomNumberGenerator();

	[MDReplicated]
	[MDReplicatedSetting(MDClockedReplicatedMember.Settings.OnValueChangedEvent, nameof(OnVisibilityChanged))]
	protected Boolean NetworkedVisible = true;

	[MDReplicated]
	[MDReplicatedSetting(MDClockedReplicatedMember.Settings.OnValueChangedEvent, nameof(OnPositionChanged))]
	protected Vector2 NetworkedPosition;

	[MDReplicated(MDReliability.Reliable, MDReplicatedType.OnChange)]
	[MDReplicatedSetting(MDClockedReplicatedMember.Settings.OnValueChangedEvent, nameof(OnScaleChanged))]
	[MDReplicatedSetting(MDReplicator.Settings.ReplicatedMemberType, typeof(MDClockedReplicatedMember))]
	protected Vector2 NetworkedScale;

	protected float Radius = 100f;
	protected float RotationSpeed = 20f;
	protected float Angle = 0f;

	protected Vector2 CenterPosition = Vector2.Zero;


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		AddToGroup(GROUP_ACTORS);

		if (!this.IsClient())
		{
			Random.Randomize();
			CenterPosition = Position + new Vector2(Random.RandfRange(-2000f, 2000f), Random.RandfRange(-1000f, 1000f));
			Radius = Random.RandfRange(100f, 500f);
			RotationSpeed = Random.RandfRange(0.1f, 1.5f);
			float scale = Random.RandfRange(2f, 4f);
			NetworkedScale = new Vector2(scale, scale);
			NetworkedVisible = true;
			OnScaleChanged();
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
		NetworkedPosition = CenterPosition + offset;
		OnPositionChanged();
	}

	protected void OnPositionChanged()
	{
		Position = NetworkedPosition;
	}

	protected void OnScaleChanged()
	{
		Scale = NetworkedScale;
	}

	protected void OnVisibilityChanged()
	{
		Icon.Visible = NetworkedVisible;
	}

	private void OnMouseEntered()
	{
		if (!MDStatics.IsClient())
		{
			NetworkedVisible = false;
			OnVisibilityChanged();
		}
	}


	private void OnMouseExited()
	{
		if (!MDStatics.IsClient())
		{
			NetworkedVisible = true;
			OnVisibilityChanged();
		}
	}

}


