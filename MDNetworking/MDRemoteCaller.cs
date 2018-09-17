using System;
using OwnerMap = System.Collections.Generic.Dictionary<string, int>;

public class MDRemoteCaller
{
    // Set a peer ID as network owner
    public void SetNetworkOwner(string NodeName, int PeerID)
    {
        // Only server can set network owner
        if (MDStatics.GetNetMode() == MDNetMode.Server)
        {
            // We only store the network owner when it's not the server/standalone client
            if (PeerID < 0)
            {
                NetworkOwners.Remove(NodeName);
            }
            else
            {
                NetworkOwners[NodeName] = PeerID;
            }
        }
    }

    // Returns true if the specified peer is the network owner of the specified node name
    public bool IsNetworkOwner(string NodeName, int PeerID)
    {
        if (PeerID < 0)
        {
            // Specified peer is server or standalone which always have owner rights
            return true;
        }

        if (NetworkOwners.ContainsKey(NodeName))
        {
            return PeerID == NetworkOwners[NodeName];
        }

        return false;
    }

    // Returns the network owner PeerID of the specified node
    public int GetNetworkOwner(string NodeName)
    {
        if (NetworkOwners.ContainsKey(NodeName))
        {
            return NetworkOwners[NodeName];
        }

        return MDStatics.GetNetMode() == MDNetMode.Server ? MDGameSession.SERVER_PEER_ID : MDGameSession.STANDALONE_PEER_ID;
    }

    // Returns true if the local peer is the network owner of the specified node name
    public bool IsNetworkOwner(string NodeName)
    {
        return IsNetworkOwner(NodeName, MDStatics.GetPeerID());
    }

    OwnerMap NetworkOwners = new OwnerMap();
}