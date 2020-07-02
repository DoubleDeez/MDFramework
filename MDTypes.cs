using Godot;
using System;

public enum MDNetMode {
    Standalone,
    Server,
    Client
}

public enum MDRemoteMode {
    Unkown,
    Master,
    MasterSync,
    Remote,
    RemoteSync,
    Puppet,
    PuppetSync
}