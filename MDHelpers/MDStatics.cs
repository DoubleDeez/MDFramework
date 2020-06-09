using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public static class MDStatics
{
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
            Members.AddRange(NodeType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Members.AddRange(NodeType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
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
}