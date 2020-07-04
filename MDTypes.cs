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

public enum MDListActions
{
    UNKOWN,
    MODIFICATION,
    ADD,
    INSERT,
    REMOVE_AT,
    REMOVE_RANGE,
    REVERSE_INDEX,
    REVERSE,
    CLEAR
}