using Godot;
using System;
using System.Reflection;

/*
 * MDGameSession
 *
 * Class that manages the current multiplayer state of the game.
 */
public class MDGameSession : Node
{
    private const string ARG_SERVER = "server";
    private const string ARG_CLIENT = "client";
    private const string LOG_CAT = "LogGameSession";

    public override void _Ready()
    {
        NetEntity = new MDNetEntity();
        this.AddNodeToRoot(NetEntity);
        CheckArgsForConnectionInfo();
        MDCommands.RegisterCommandAttributes(this);
    }

    [MDCommand()]
    public bool StartServer(int Port)
    {
        bool Success = NetEntity.StartServer(Port);
        MDLog.CLog(Success, LOG_CAT, MDLogLevel.Info, "Started server on port {0}", Port);
        MDLog.CLog(!Success, LOG_CAT, MDLogLevel.Error, "Failed to start server on port {0}", Port);
        return Success;
    }

    [MDCommand()]
    public bool StartClient(string Address, int Port)
    {
        bool Success = NetEntity.ConnectToServer(Address, Port);
        MDLog.CLog(Success, LOG_CAT, MDLogLevel.Info, "Connected to server at {0}:{1}", Address, Port);
        MDLog.CLog(!Success, LOG_CAT, MDLogLevel.Error, "Failed to connect to server at {0}:{1}", Address, Port);
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
            string ClientArg = MDArguments.GetArg(ARG_CLIENT);
            string[] HostPort = ClientArg.Split(":");
            if (HostPort.Length == 2)
            {
                StartClient(HostPort[0], HostPort[1].ToInt());
            }
            else
            {
                MDLog.Error(LOG_CAT, "Failed to parse client arg {0}, expecting -{1}=[IPAddres:Port]", ClientArg, ARG_CLIENT);
            }
        }
    }

    private MDNetEntity NetEntity;
}
