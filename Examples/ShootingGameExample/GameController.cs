using Godot;
using System;
using MD;

public class GameController : Node2D
{
    protected MDGameSession GameSession;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        GameSession = this.GetGameSession();
        GameSession.OnPlayerJoinedEvent += OnPlayerJoinedEvent;
        GameSession.OnPlayerLeftEvent += OnPlayerLeftEvent;
    }

    public override void _ExitTree()
    {
        GameSession.OnPlayerJoinedEvent -= OnPlayerJoinedEvent;
        GameSession.OnPlayerLeftEvent -= OnPlayerLeftEvent;
    }

    protected virtual void OnPlayerJoinedEvent(int PeerId)
    {
        if (this.IsClient())
        {
            return;
        }

        CallDeferred(nameof(SpawnPlayer), PeerId);
    }

    protected void SpawnPlayer(int PeerId)
    {
        this.SpawnNetworkedNode(GetPlayerScene(), "Player", PeerId);
    }

    private String GetPlayerScene()
    {
        // This is to avoid needing references
        return GetParent().Filename.GetBaseDir() + "/Player.tscn";
    }

    protected virtual void OnPlayerLeftEvent(int PeerId)
    {
        foreach (Node node in GetTree().GetNodesInGroup(Player.PLAYER_GROUP))
        {
            if (node.GetNetworkMaster() == PeerId)
            {
                node.RemoveAndFree();
            }
        }
    }
}