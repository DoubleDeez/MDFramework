using Godot;
using System;

/*
	A simple predictive actor that always behaves the same if it is synched
*/
[MDAutoRegister]
public class PredictiveActor : KinematicBody2D, IMDSynchronizedNode
{
	public static readonly string GROUP_ACTORS = "PerdictivActors";

	[MDReplicated]
	protected float Speed = 0f;

	[MDReplicated]
	protected Vector2 Direction = Vector2.Zero;

	protected Vector2 DirectionInt = Vector2.Zero;

	protected uint StartAt = 1;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// TODO: Implement request for position
		// TODO: Implement synchronizer interface ISynchronizedNode
		AddToGroup(GROUP_ACTORS);
		if (MDStatics.IsClient())
		{
			return;
		}
		RandomNumberGenerator Random = new RandomNumberGenerator();
		Random.Randomize();
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

		foreach (MDPlayerInfo info in this.GetGameSession().GetAllPlayerInfos())
		{
			if (info.PeerId == MDStatics.GetPeerId())
			{
				// Set our start time
				StartAt = OS.GetTicksMsec() + delay;
			}
			else
			{
				// Set start time for other clients
				RpcId(info.PeerId, nameof(RpcSetStartTime), this.GetPlayerTicksMsec(info.PeerId) + delay);
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
			return false;
		}

		return true;
	}

}
