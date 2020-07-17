namespace MD
{
    /// <summary>
    /// The mode the framework is running in.
    /// </summary>
    public enum MDNetMode
    {
        Standalone,
        Server,
        Client
    }

    /// <summary>
    /// All the possible method/member remote modes supported in godot.
    /// </summary>
    public enum MDRemoteMode
    {
        Unknown,
        Master,
        MasterSync,
        Remote,
        RemoteSync,
        Puppet,
        PuppetSync
    }
}