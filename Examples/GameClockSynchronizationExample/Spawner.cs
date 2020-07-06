using Godot;
using System;
using MD;

[MDAutoRegister]
public class Spawner : Node2D
{
    public const String GROUP_ACTORS = "ACTORS";

    private const string LOG_CAT = "LogActorSpawner";

    [Export]
    public int TotalNodes = 10;

    [Export]
    public string ActorFileName = "Actor.tscn";

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
        CallDeferred(nameof(SpawnNodes));
    }

    protected virtual void OnSessionFailedOrEndedEvent()
    {
        foreach (Node n in GetTree().GetNodesInGroup(PredictiveActor.GROUP_ACTORS))
        {
            n.QueueFree();
        }
    }

    private void SpawnNodes()
    {
        if (this.IsClient())
        {
            return;
        }

        int CurrentNodes = GetTree().GetNodesInGroup(GROUP_ACTORS).Count;
        if (CurrentNodes < TotalNodes)
        {
            // Add more if needed
            this.SpawnNetworkedNode(GetActorScene(), "Actor");
        }
        else if (CurrentNodes > TotalNodes)
        {
            // Remove
            ((Node) GetTree().GetNodesInGroup(GROUP_ACTORS)[0]).QueueFree();
        }
        else
        {
            return;
        }

        CallDeferred(nameof(SpawnNodes));
    }

    private String GetActorScene()
    {
        // This is to avoid needing references
        return GetParent().Filename.GetBaseDir() + "/" + ActorFileName;
    }
}