namespace MD
{
    public enum MDNetMode
    {
        Standalone,
        Server,
        Client
    }

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