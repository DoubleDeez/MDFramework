using Godot;
using System;

public static class MDStatics
{
    // MDStatics needs a reference to a Godot object to really be useful, so the GameInstance sets a reference to itself here
    public static MDGameInstance GI {get; set;}

    // Useful to return instead of null
    public static byte[] EmptyByteArray {get;} = new byte[0];

    // Get the global game session from the game instance
    public static MDGameSession GetGameSession()
    {
        return GI.GameSession;
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
    public static int GetPeerID()
    {
        if (GetTree().HasNetworkPeer())
        {
            return GetTree().GetNetworkUniqueId();
        }

        return 0;
    }

    // Is the game session started in non-standalone?
    public static bool IsNetworkActive()
    {
        return GetPeerID() != 0;
    }

    // Gets the net mode of the local client
    public static MDNetMode GetNetMode()
    {
        return GI.GetNetMode();
    }

    // Gets the SceneTree from the GameInstance
    public static SceneTree GetTree()
    {
        return GI.GetTree();
    }

    // Returns true if the types are equal or is SubClass is a a subclass of Base
    public static bool IsSameOrSubclass(Type SubClass, Type Base)
    {
        return SubClass.IsSubclassOf(Base) || SubClass == Base;
    }
}