using Godot;
using System;

/*
 * MDGameSession
 *
 * Class that manages the current multiplayer state of the game.
 */
public class MDGameSession : MDNode
{
    private const string ARG_SERVER = "server";
    private const string ARG_CLIENT = "client";

    public override void _Ready()
    {
        NetEntity = new MDNetEntity();
        CheckArgsForConnectionInfo();
    }

    public bool StartServer(int Port)
    {
        bool Success = NetEntity.StartServer(Port);
        // TODO - log
        return Success;
    }

    public bool StartClient(string Address, int Port)
    {
        bool Success = NetEntity.ConnectToServer(Address, Port);
        // TODO - log
        return Success;
    }

    private void CheckArgsForConnectionInfo()
    {
        // Expects -server=[port]
        if (MDArguments.HasArg(ARG_SERVER))
        {
            int Port = MDArguments.GetArgInt(ARG_SERVER);
            StartServer(Port);
        }
        // Expects -client=[IPAddres:Port]
        else if (MDArguments.HasArg(ARG_CLIENT))
        {
            string[] HostPort = MDArguments.GetArg(ARG_CLIENT).Split(":");
            if (HostPort.Length > 1)
            {
                StartClient(HostPort[0], HostPort[1].ToInt());
            }
            else
            {
                // TODO - Log error
            }
        }
    }

    private MDNetEntity NetEntity;
}
