# MDFramework
A multiplayer C# game framework for Godot 3 dependent on the GDNet module (https://github.com/PerduGames/gdnet3).

As this is being built along side my own Godot project, I will generally add features as I need them.

## Why?
At the time of writing this framework, I found Godot's existing high-level networking framework too limited.
So I wanted to build something that had the features and ease-of-use of a high-level networking framework but also offered the flexibility of something like ENet.

# Features
* [Command line parameter parsing](#command-line-arguments) that can be queried at any time
* A simple [logging system](#logging) with logging categories and different log levels for each category for both writing to file and stdout
* A [console command](#command-console) prompt that allows you to add commands from single-instance classes
* Attribute-based [field replication](#field-replication)
* A simpler [profiler](#profiler) class to determine execution time of a code block

# Installation
0. (Optional) I recommend forking the repo so you can track your changes to it, easily merge updates, and reuse it in other projects you have.

1. Add the repo as a submodule to your Godot project's directory (for me I added it to `src\MDFramework`).

2. Add the MDFramework files to your `Project.csproj`, inside of `<ItemGroup>`, making sure the path matches where you cloned the repo. 

```xml
    <Compile Include="src\MDFramework\MDAttributes\MDCommand.cs" />
    <Compile Include="src\MDFramework\MDAttributes\MDReplicated.cs" />
    <Compile Include="src\MDFramework\MDAttributes\MDRpc.cs" />
    <Compile Include="src\MDFramework\MDExtensions\MDControlExtensions.cs" />
    <Compile Include="src\MDFramework\MDExtensions\MDNodeExtensions.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDArguments.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDCommands.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDLog.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDProfiler.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDSerialization.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDStatics.cs" />
    <Compile Include="src\MDFramework\MDInterface\MDConsole.cs" />
    <Compile Include="src\MDFramework\MDInterface\MDInterfaceManager.cs" />
    <Compile Include="src\MDFramework\MDNetworking\MDGameSession.cs" />
    <Compile Include="src\MDFramework\MDNetworking\MDRemoteCaller.cs" />
    <Compile Include="src\MDFramework\MDNetworking\MDReplicator.cs" />
    <Compile Include="src\MDFramework\MDGameInstance.cs" />
    <Compile Include="src\MDFramework\MDNetEntity.cs" />
    <Compile Include="src\MDFramework\MDPlayer.cs" />
```

3. Setup your `project.godot` to AutoLoad either `MDGameInstance` or your subclass of it.

```ini
[autoload]
GameInstance="*res://src/MDFramework/MDGameInstance.cs"
```
**Note:** It **must** be called `GameInstance` for the framework to work.

# How to use MDFramework

## Command Line Arguments
TODO

## Command Console
In game, the command console can be opened with the `~` key. Command history can be navigated using the `Up` and `Down` arrow keys.

In your code, you can add more commands by adding the `[MDCommand()]` attribute to any methods you wish to have as commands.
Then from that same class call `MDCommands.RegisterCommandAttributes(this);` to have those commands registered.
For classes extending `Node`, a good place to call it would be in `_Ready()`, `Node` classes have an extension helper for this, so you can just call `this.RegisterCommandAttributes();`.

Only a single instance of a class can be registered for commands, this is because commands are invoked via their method name, which are the same for all instances of a class.

## Logging
TODO

## Replication
There are 2 methods of replication with this framework. RPCs (calling a function on a remote system) and field replication (copying the data of a variable from the server to clients).

### Network Ownership
Only the server can set who has network ownership of a node. By default, all nodes except for `MDPlayer` player instances will have the server as their network owner.
For `MDPlayer` instances, the server automatically assigns the corresponding peer as the owner of their `MDPlayer` instance.
Standalone clients and the server always have network ownership rights, even if a client is set as network owner.
How network ownership affects replication is described in the [RPCs](#rpcs) section below.

### RPCs
RPC functions can only exist on Node objects. There are 3 types of RPC functions: Server, Client, and Broadcast commands.

#### Server
Server functions can only be called from the Server itself or from the network owner of the Node (the server by default).
Server functions are a good way for a client to get data to the server, usually from the `MDPlayer` class, as each client is the network owner of an `MDPlayer` instance.

#### Client
Client functions can only be called from the Server and are sent to the network owner of the RPCs object.
For example, the server could notify the player that they died via a Client function on `MDPlayer`.

#### Broadcast
Broadcast functions can only be called from the Server and are sent to every client and triggered on server and clients instances of the RPCs object.

### Field Replication
Setting up replicating has a similar pattern to setting up console commands. Any field on a `Node` class marked with the `MDReplicated()` attribute can be replicated. The registered `Node` **must** have its name set before being registered, and the name must be the same on the server and all clients as this is how `MDReplicator` determines where to send replicated data. Fields are always reliably replicated, although order isn't necessarily guaranteed since all fields are replicated as a post-process - they are not sent out as soon as you assign the variable. To register your `MDReplicated()` fields on your node, call `this.RegisterReplicatedFields();`, ideally in your `_Ready()` function.

**Note:** Only the server can set the value of replicated values. Values set by the client will not be replicated. If you want the client to update a field, use a Server RPC.

### Supported Types for replication
The following types are able to be used for field replication and RPC parameters.
* bool
* byte
* char
* float
* long
* ulong
* int
* uint
* short
* ushort
* string
* enums (converted to int and back internally)
* Dictionary/List/Array of the above types // TODO
* Any of the above types on Classes/Structs (Only structs for RPC params) // TODO

## Profiler
MDProfiler is a _very_ simple profiler. It will track the time it takes for a block of code to run and if enabled, log it.

To use MDProfiler in code is simple:
```csharp
#if DEBUG
using (MDProfiler Profiler = new MDProfiler("IDENTITYING STRING HERE"))
#endif
{
    // Code you want to profile here
}
```
The debug check is recommended to not have it run in your release builds.

To enable the profile logging, add `-logprofile` to your command line args when launching Godot/your game.
Currently, this will enable logging for all MDProfiler instances and can get very log spammy.
```log
[2018-09-17 00:15:40.932][PEER 0] [LogProfiler::Info] Profiling [IDENTITYING STRING HERE] took 20us
```

# TODO
* An ability to enable command prompt in release builds
* Command prompt auto-complete with help text
* Notification of a change in a replicated field
* Client/Server/Broadcast functions (RPCs)
* Reliability settings of RPCs
* Assign RPCs to channels
* Distance based replication relevancy
* UI management framework
* Automatic Field<->Node binding
* Enable only specific instances of profile logging (rather than the entire system on/off)
* More features...
* Optimize, threading, convert performance critical features to C++
* De-spaghetti Replication<->Serialization
* Figure out a way to do replication without string compares/serializing names
* Maybe use a large byte array buffer when serializing data instead of creating a ton of small byte[]
