using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public static class MDStatics
{

    public static BindingFlags BindFlagsAllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // MDStatics needs a reference to a Godot object to really be useful, so the GameInstance sets a reference to itself here
    public static MDGameInstance GI {get; set;}

    // Useful to return instead of null
    public static byte[] EmptyByteArray {get;} = new byte[0];

    ///<summary>Get the global game session from the game instance</summary>
    public static MDGameSession GetGameSession()
    {
        return GI.GameSession;
    }

    ///<summary>Get the global game synchronizer from the game instance</summary>
    public static MDGameSynchronizer GetGameSynchronizer()
    {
        return GI.GameSynchronizer;
    }

    public static MDReplicator GetReplicator()
    {
        return GI.Replicator;
    }

    // Helper to construct a subarray
    public static T[] SubArray<T>(this T[] data, int StartIndex, int EndIndex)
    {
        int NewLength = 1 + (EndIndex - StartIndex);
        if (NewLength == data.Length)
        {
            return data;
        }

        T[] result = new T[NewLength];
        System.Array.Copy(data, StartIndex, result, 0, NewLength);
        return result;
    }

    // Helper to trim the beginning of an array
    public static T[] SubArray<T>(this T[] data, int StartIndex)
    {
        return data.SubArray(StartIndex, data.Length - 1);
    }

    // Helper to append any number of byte arrays
    public static byte[] JoinByteArrays(params byte[][] ByteArrays)
    {
        // Count the total number of bytes
        int TotalBytes = 0;
        for (int i = 0; i < ByteArrays.Length; ++i)
        {
            TotalBytes += ByteArrays[i].Length;
        }

        // Copy all the bytes into the new array
        byte[] JoinedArray = new byte[TotalBytes];

        int NumBytesCopied = 0;
        for (int i = 0; i < ByteArrays.Length; ++i)
        {
            ByteArrays[i].CopyTo(JoinedArray, NumBytesCopied);
            NumBytesCopied += ByteArrays[i].Length;
        }

        return JoinedArray;
    }

    // Gets the peer ID from the game session, 1 for server or 0 for standalone
    public static int GetPeerId()
    {
        SceneTree Tree = GetTree();
        if (Tree != null && Tree.HasNetworkPeer())
        {
            return GetTree().GetNetworkUniqueId();
        }

        return 0;
    }

    public static bool IsGameClockActive()
    {
        if (GetGameSynchronizer() != null && GetGameSynchronizer().IsGameClockActive())
        {
            return true;
        }

        return false;
    }

    // Is the game session started in non-standalone?
    public static bool IsNetworkActive()
    {
        return GetPeerId() != 0;
    }
    
    // Is the game session the server?
    public static bool IsServer()
    {
        return GetPeerId() == GetServerId();
    }

    ///<summary>Returns true if we are on a networked connection and we are a client</summary>
    public static bool IsClient()
    {
        return IsNetworkActive() && !IsServer();
    }

    // Get the PeerId of the server
    public static int GetServerId()
    {
        return GetGameSession().GetNetworkMaster();
    }

    // Gets the net mode of the local client
    public static MDNetMode GetNetMode()
    {
        return GI != null ? GI.GetNetMode() : MDNetMode.Standalone;
    }

    // Gets the SceneTree from the GameInstance
    public static SceneTree GetTree()
    {
        return (GI != null && GI.IsInsideTree()) ? GI.GetTree() : null;
    }

    // Returns true if the types are equal or is SubClass is a a subclass of Base
    public static bool IsSameOrSubclass(Type SubClass, Type Base)
    {
        return SubClass.IsSubclassOf(Base) || SubClass == Base;
    }

    // Returns the attribute object for the specified type, climbing the hierarchy until Node is reached or the attribute is found
    public static T FindClassAttribute<T>(Type InstanceType) where T : Attribute
    {
        if (IsSameOrSubclass(InstanceType, typeof(Node)) == false)
        {
            return null;
        }

        T FoundAtr = Attribute.GetCustomAttribute(InstanceType, typeof(T)) as T;
        if (FoundAtr != null)
        {
            return FoundAtr;
        }

        if (InstanceType != typeof(Node))
        {
            return FindClassAttribute<T>(InstanceType.BaseType);
        }

        return null;
    }

    // Returns a list of all the unique members for a Node, including the hierarchy
    public static List<MemberInfo> GetTypeMemberInfos(Node CurNode)
    {
        List<MemberInfo> Members = new List<MemberInfo>();
        Type NodeType = CurNode.GetType();
        while (NodeType != typeof(Node))
        {
            Members.AddRange(NodeType.GetFields(BindFlagsAllMembers));
            Members.AddRange(NodeType.GetProperties(BindFlagsAllMembers));
            NodeType = NodeType.BaseType;
        }

        List<MemberInfo> DeDupedMembers = new List<MemberInfo>();
        foreach(MemberInfo Member in Members)
        {
            bool IsUnique = true;
            foreach(MemberInfo DeDupedMember in DeDupedMembers)
            {
                if (DeDupedMember.DeclaringType == Member.DeclaringType && DeDupedMember.Name == Member.Name)
                {
                    IsUnique = false;
                    break;
                }
            }

            if (IsUnique)
            {
                DeDupedMembers.Add(Member);
            }
        }

        return DeDupedMembers;
    }

    public static MemberInfo GetMemberByName(Node CurNode, String Name)
    {
        Type NodeType = CurNode.GetType();
        MemberInfo Member = NodeType.GetField(Name, BindFlagsAllMembers);
        if (Member == null)
        {
            Member = NodeType.GetProperty(Name, BindFlagsAllMembers);
        }

        return Member;
    }

    public static MDRemoteMode GetMethodRpcType(Node Node, String Method)
    {
        Type NodeType = Node.GetType();
        MethodInfo Info = NodeType.GetMethod(Method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (Info == null)
        {
            return MDRemoteMode.Unkown;
        }

        if (Info.GetCustomAttribute(typeof(RemoteAttribute)) != null)
        {
            return MDRemoteMode.Remote;
        } 
        else if (Info.GetCustomAttribute(typeof(RemoteSyncAttribute)) != null)
        {
            return MDRemoteMode.RemoteSync;
        }
        else if (Info.GetCustomAttribute(typeof(PuppetAttribute)) != null)
        {
            return MDRemoteMode.Puppet;
        }
        else if (Info.GetCustomAttribute(typeof(PuppetSyncAttribute)) != null)
        {
            return MDRemoteMode.PuppetSync;
        }
        else if (Info.GetCustomAttribute(typeof(MasterAttribute)) != null)
        {
            return MDRemoteMode.Master;
        }
        else if (Info.GetCustomAttribute(typeof(MasterSyncAttribute)) != null)
        {
            return MDRemoteMode.MasterSync;
        }

        return MDRemoteMode.Unkown;
    }

    ///<summary>Converts a size in bytes such as the one from OS.GetStaticMemoryUsage() into a more human readable format</summary>
    public static String HumanReadableSize(double len)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1) {
            order++;
            len = len/1024;
        }

        return String.Format("{0:0.##} {1}", len, sizes[order]);
    }
}