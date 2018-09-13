using Godot;
using System;

// Description of the network state of this application.
public enum MDNetMode
{
    Standalone,
    Server,
    Client
}

/*
 * MDNetEntity
 *
 * Class that handles all the incoming and outgoing data with GDNet.
 * Can start a server or join an existing one.
 */
public class MDNetEntity : Node
{
    public delegate void OnNetEventDelegate(GDNetEvent NetEvent);
    public OnNetEventDelegate OnNetEvent;

    private const string LOG_CAT = "LogNetEntity";

    public override void _Ready()
    {
        NetHost = new GDNetHost();
        NetMode = MDNetMode.Standalone;

        SetProcess(false);
    }

    public override void _Process(float Delta)
    {
        while (NetHost.IsEventAvailable())
        {
            GDNetEvent NetEvent = NetHost.GetEvent();
            if (OnNetEvent != null)
            {
                OnNetEvent(NetEvent);
            }
        }
    }

    public bool StartServer(int Port)
    {
        GDNetAddress Address = new GDNetAddress();
        Address.SetHost("");
        Address.SetPort(Port);

        if (NetHost.Bind(Address) == Error.Ok)
        {
            NetMode = MDNetMode.Server;
            SetProcess(true);
            return true;
        }

        return false;
    }

    public bool ConnectToServer(string ServerAddress, int Port)
    {
        GDNetAddress Address = new GDNetAddress();
        Address.SetHost(ServerAddress);
        Address.SetPort(Port);

        if (NetHost.Bind(null) != Error.Ok)
        {
            return false;
        }

        ClientPeer = NetHost.HostConnect(Address);
        if (ClientPeer != null)
        {
            NetMode = MDNetMode.Client;
            SetProcess(true);
            return true;
        }

        return false;
    }

    public void Disconnect()
    {
        if (ClientPeer != null)
        {
            ClientPeer.DisconnectNow();
        }

        NetHost.Unbind();
        NetMode = MDNetMode.Standalone;
        SetProcess(false);
    }

    public void SendBytes(byte[] Data)
    {
        if (NetMode == MDNetMode.Server)
        {
            NetHost.BroadcastPacket(Data, 0, (int)GDNetMessage.Type.Reliable);
        }
        else if (NetMode == MDNetMode.Client)
        {
            ClientPeer.SendPacket(Data, 0, (int)GDNetMessage.Type.Reliable);
        }
        else
        {
            MDLog.Error(LOG_CAT, "Can't send data, not connected");
        }
    }

    public GDNetHost NetHost {get; private set;}
    public GDNetPeer ClientPeer {get; private set;}
    public MDNetMode NetMode {get; private set;} 
}
