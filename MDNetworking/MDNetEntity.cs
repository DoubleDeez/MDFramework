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
public class MDNetEntity 
{
    public delegate void OnNetEventDelegate(GDNetEvent NetEvent);
    public OnNetEventDelegate OnNetEvent;

    private const string LOG_CAT = "LogNetEntity";

    public MDNetEntity()
    {
        NetMode = MDNetMode.Standalone;
    }

    public void ReadNetEvents()
    {
        if (NetMode > MDNetMode.Standalone)
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
    }

    public bool StartServer(int Port)
    {
        GDNetAddress Address = new GDNetAddress();
        Address.SetHost("");
        Address.SetPort(Port);

        if (NetHost.Bind(Address) == Error.Ok)
        {
            NetMode = MDNetMode.Server;
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
    }

    public void SendBytes(byte[] Data, GDNetMessage.Type MsgType = GDNetMessage.Type.Reliable)
    {
        if (NetMode == MDNetMode.Server)
        {
            NetHost.BroadcastPacket(Data, 0, (int)MsgType);
        }
        else if (NetMode == MDNetMode.Client)
        {
            ClientPeer.SendPacket(Data, 0, (int)MsgType);
        }
        else
        {
            MDLog.Error(LOG_CAT, "Can't send data, not connected");
        }
    }

    public void SendBytes(int Peer, byte[] Data, GDNetMessage.Type MsgType = GDNetMessage.Type.Reliable)
    {
        if (NetMode == MDNetMode.Server)
        {
            GDNetPeer NetPeer = NetHost.GetPeer(Peer);
            if (NetPeer != null)
            {
                NetPeer.SendPacket(Data, 0, (int)MsgType);
            }
            else
            {
                MDLog.Error(LOG_CAT, "Peer ID [{0}] doesn't exist", Peer);
            }
        }
        else
        {
            MDLog.Error(LOG_CAT, "Can't send data, not server");
        }
    }

    public GDNetHost NetHost {get; private set;} = new GDNetHost();
    public GDNetPeer ClientPeer {get; private set;}
    public MDNetMode NetMode {get; private set;} 
}
