using static Godot.NetworkedMultiplayerENet;

namespace MD
{
    /// <summary>
    /// Peer configuration
    /// </summary>
    public static class MDPeerConfigs
    {
        public static bool ServerRelay { get; set; } = true;

        public static bool AlwaysOrdered { get; set; } = false;

        public static int ChannelCount { get; set; } = 3;
        public static CompressionModeEnum CompressionMode { get; set; } = CompressionModeEnum.None;

        public static int TransferChannel { get; set; } = -1;
    }
}