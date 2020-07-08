using Godot;
using System;
using MD;

[MDAutoRegister]
public class ReplicatedListExample : Node2D
{
    protected MDGameSession GameSession;

    [MDBindNode("/root/PredictiveExample/CanvasLayer/BtnDisconnect/ButtonRoot")]
    protected Control ButtonRoot;
    
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
        CallDeferred(nameof(SpawnNode));
    }

    private void SpawnNode()
    {
        if (this.IsClient())
        {
            return;
        }

        this.SpawnNetworkedNode(GetActorScene(), "Actor");
    }

    protected virtual void OnSessionFailedOrEndedEvent()
    {
        foreach (Node n in GetTree().GetNodesInGroup(ListActor.GROUP_ACTORS))
        {
            n.QueueFree();
        }
    }

    private String GetActorScene()
    {
        // This is to avoid needing references
        return GetParent().Filename.GetBaseDir() + "/ListActor.tscn";
    }
}
