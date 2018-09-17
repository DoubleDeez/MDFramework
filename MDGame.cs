using Godot;
using System;

/*
 * MDGame
 *
 * Class that controls flow of the game and holds game data that is replicated to players.
 */
public class MDGame : Node
{
    private const string GAME_NODE_NAME = "GameNode";

    public override void _Ready()
    {
        SetName(GAME_NODE_NAME);
    }
}
