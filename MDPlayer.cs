using Godot;
using System;

/*
 * MDPlayer
 *
 * Class that tracks the players game data, to be replicated to relevant players.
 */
public class MDPlayer : Node
{
    public override void _Ready()
    {
    }

    public int PeerID {get; set;}
}
