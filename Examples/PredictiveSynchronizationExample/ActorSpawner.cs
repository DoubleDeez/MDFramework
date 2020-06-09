using Godot;
using System;

[MDAutoRegister]
public class ActorSpawner : Node2D
{
	private const string LOG_CAT = "LogActorSpawner";

	[Export]
	public PackedScene ActorScene;

	[Export]
	public int TotalNodes = 10;

	[MDBindNode("/root/PredictiveExample/CanvasLayer/BtnDisconnect/ButtonRoot")]
	protected Control ButtonRoot;

	protected MDGameSession GameSession;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GameSession = this.GetGameSession();
		GameSession.OnSessionStartedEvent += OnSessionStartedEvent;
		GameSession.OnSessionFailedEvent += OnSessionFailedOrEndedEvent;
		GameSession.OnSessionEndedEvent += OnSessionFailedOrEndedEvent;
	}

	public override void _ExitTree()
	{
		GameSession.OnSessionStartedEvent -= OnSessionStartedEvent;
		GameSession.OnSessionFailedEvent -= OnSessionFailedOrEndedEvent;
		GameSession.OnSessionEndedEvent -= OnSessionFailedOrEndedEvent;
	}

	protected virtual void OnSessionStartedEvent()
	{
		ToggleButtonRoot(!this.IsClient());
		CallDeferred(nameof(SpawnNodes));
	}

	protected virtual void OnSessionFailedOrEndedEvent()
	{
		foreach (Node n in GetTree().GetNodesInGroup(PredictiveActor.GROUP_ACTORS))
		{
			n.QueueFree();
		}
	}

	private void OnIncreasePressed()
	{
		if (this.IsClient())
		{
			return;
		}
		TotalNodes += 10;
		SpawnNodes();
	}


	private void OnDecreasePressed()
	{
		if (this.IsClient())
		{
			return;
		}
		TotalNodes -= 10;
		if (TotalNodes <= 10)
		{
			TotalNodes = 10;
		}
		SpawnNodes();
	}

	private void SpawnNodes()
	{
		if (this.IsClient())
		{
			return;
		}

		int CurrentNodes = GetTree().GetNodesInGroup(PredictiveActor.GROUP_ACTORS).Count;
		if (CurrentNodes < TotalNodes)
		{
			// Add more if needed
			this.SpawnNetworkedNode(ActorScene, "Actor");
		}
		else if (CurrentNodes > TotalNodes)
		{
			// Remove
			((Node)GetTree().GetNodesInGroup(PredictiveActor.GROUP_ACTORS)[0]).QueueFree();
		}
		else
		{
			return;
		}

		CallDeferred(nameof(SpawnNodes));
	}

	private void ToggleButtonRoot(bool visible)
	{
		if (ButtonRoot == null)
		{
			MDLog.Warn(LOG_CAT, "ButtonRoot not found");
			return;
		}
		ButtonRoot.Visible = visible;
	}

}
