using static Godot.NetworkedMultiplayerENet;

public static class MDPeerConfigs
{
    private static bool _serverRelay = true;
    private static bool _alwaysOrdered = false;
    private static int _channelCount = 3;
    private static CompressionModeEnum _compressionMode = CompressionModeEnum.None;
    private static int _transferChannel = -1;

    public static bool ServerRelay
    {
        get => _serverRelay;
        set => _serverRelay = value;
    }

    public static bool AlwaysOrdered
    {
        get => _alwaysOrdered;
        set => _alwaysOrdered = value;
    }

    public static int ChannelCount
    {
        get => _channelCount;
        set => _channelCount = value;
    }

    public static CompressionModeEnum CompressionMode
    {
        get => _compressionMode;
        set => _compressionMode = value;
    }

    public static int TransferChannel
    {
        get => _transferChannel;
        set => _transferChannel = value;
    }
}