using Godot;
using System;

/*
 * MDNetEntity
 *
 * Class that handles all the incoming and outgoing data.
 * Can start a server or join an existing one.
 */
public class MDNetEntity : MDNode
{
    public override void _Ready()
    {
        NetHost = new GDNetHost();
    }

    public bool StartServer(int Port)
    {
        GDNetAddress Address = new GDNetAddress();
        Address.SetPort(Port);

        return NetHost.Bind(Address) == Error.Ok;
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

        return ClientPeer != null;
    }

    private GDNetHost NetHost;
    private GDNetPeer ClientPeer;
}
