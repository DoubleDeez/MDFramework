# MDFramework
A multiplayer C# game framework for Godot 3.2.

As this is being built along side my own Godot project, I will generally add features as I need them.

## Why?
There are a lot of features from other game engines that I'm used to, so I wanted to build something that made those features available in Godot.

# Features
* Automatically [replicate members](#automatic-member-replication) without calling `Rset`.
* Allows the server to [spawn nodes over the network](#networked-node-spawning) to all clients.
* [Command line parameter parsing](#command-line-arguments) that can be queried at any time
* A simple [logging system](#logging) with logging categories and different log levels for each category for both writing to file and stdout
* A [console command](#command-console) prompt that allows you to add commands from single-instance classes
* A simple [profiler](#profiler) class to determine execution time of a code block
* Bind editor-created nodes to member variables automatically with [node binding](#node-binding)
* A [Game Session and Player Info](#game-Session-and-player-info) system for managing networked multiplayer

# Installation
0. (Optional) I recommend forking the repo so you can track your changes to it, easily merge updates, and reuse it in other projects you have.

1. Add the repo as a submodule to your Godot project's directory (for me I added it to `src\MDFramework`).
```bash
git submodule add https://github.com/DoubleDeez/MDFramework.git src/MDFramework
```

2. Add the MDFramework files to your `Project.csproj`, inside of `<ItemGroup>`, making sure the path matches where you cloned the repo. 

```xml
    <Compile Include="src\MDFramework\MDAttributes\MDBindNode.cs" />
    <Compile Include="src\MDFramework\MDAttributes\MDCommand.cs" />
    <Compile Include="src\MDFramework\MDAttributes\MDReplicated.cs" />
    <Compile Include="src\MDFramework\MDExtensions\MDControlExtensions.cs" />
    <Compile Include="src\MDFramework\MDExtensions\MDNodeExtensions.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDArguments.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDCommands.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDLog.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDProfiler.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDStatics.cs" />
    <Compile Include="src\MDFramework\MDInterface\MDConsole.cs" />
    <Compile Include="src\MDFramework\MDInterface\MDInterfaceManager.cs" />
    <Compile Include="src\MDFramework\MDNetworking\MDGameSession.cs" />
    <Compile Include="src\MDFramework\MDNetworking\MDPlayerInfo.cs" />
    <Compile Include="src\MDFramework\MDNetworking\MDReplicator.cs" />
    <Compile Include="src\MDFramework\MDGameInstance.cs" />
```
or to include all C# files:
```xml
    <Compile Include="**\*.cs" />
```

3. Setup your `project.godot` to AutoLoad either `MDGameInstance` or your subclass of it:

```ini
[autoload]
GameInstance="*res://src/MDFramework/MDGameInstance.cs"
```
**Note:** It **must** be called `GameInstance` for the framework to work.

# How to use MDFramework
## Game Session and Player Info

### Game Session Basics
The `MDGameSession` class is initialized by the `MDGameInstance` and can by accessed by any `Node` by calling `this.GetGameSession()` or elsewhere by using `MDStatics.GetGameSession()`.

The class has 3 useful methods for managing your networked session.

For starting the server you can call:
```csharp
bool StartServer(int Port, int MaxPlayers);
```

For connecting to the server from the client:
```csharp
bool StartClient(string Address, int Port);
```

And finally for disconnecting (as either server or client)
```csharp
void Disconnect();
```

There's also a method to start the session without the network but still trigger the Game Session events to make it easier when implementing your game:
```csharp
bool StartStandalone();
```

Those 4 methods are all available as console commands to make it easier for you to start testing.

### Game Session Events
There are 5 events that you can hook into for building your game.

The `OnPlayerJoinedEvent` and `OnPlayerLeftEvent` events will pass in the player's peer Id.

There's also `OnSessionStartedEvent`, `OnSessionFailedEvent`, and `OnSessionEndedEvent` so that you can be notified of the session's state.

By default, [UPNP](https://docs.godotengine.org/en/latest/classes/class_upnp.html) is enabled and its status is printed to the log. To disable UPNP, override `bool UseUPNP();` on your GameInstance.

### Player Info

The `MDPlayerInfo` class is the location for you to store all the information specific to a player.
A player info instance is create for each player on each player's game, and the corresponding player is set as the network master for their player info node.

The player info is meant to be extended.

To do this, you must override the Game Instance class to specify your player info class, just change the autoload path to point to your extended game instance class. (See step 3 of [Installation](#installation))

Then override `GetPlayerInfoType()` on your Game Instance and return the type of your player info:
```csharp
protected override Type GetPlayerInfoType()
{
    return typeof(MyPlayerInfo);
}
```

On your custom player info class, override `PerformFullSync()` to do any sync-ing that needs to happen when a new player joins.

## Automatic Member Replication
Thanks to C#'s reflection, `MDReplicated` is able to detect if a value has changed and call `Rset` for you automatically.

To use it, mark your Node's field or property with the `[MDReplicated]` attribute. You can also set one of Godot's RPCModes (Master, Puppet, Remote, etc), but if you don't `MDReplicator` will automatically set it to `Puppet` for you.

By default, it uses reliable replication, but you can change that by using `[MDReplicated(MDReliablity.Unreliable)]`, which is recommended if the value changes very frequently.

By default, only the network master for the node will send out updated values, unless if the member is marked with `[Master]`, in which case the puppets will send values - with more than 1 puppet, this will lead to some unnecessary `Rset` calls.

When a new player connects, they will receive `[MDReplicated]` values a short time after connect (about 1 second).

If you wish to be notified on the client when a replicated value changes, use a custom property setter:
```cs
    float _BarrelRotation = 0;
    [MDReplicated]
    float BarrelRotation
    {
        get { return _BarrelRotation; }
        set
        {
            _BarrelRotation = value;
            Barrel.Rotation = value;
        }
    }
```

See the [Automatic Registration](#automatic-registration) section for information on how these are populated and how you can configure that.

## Networked Node Spawning
The server has the ability to create a node for all clients over the network. It works by calling `this.SpawnNetworkedNode()` which is available on any `Node` object.

`SpawnNetworkedNode()` has 3 overloads, allowing you to spawn a Node from a C# Type, a PackedScene, or a path string to a scene:
```csharp
    public Node SpawnNetworkedNode(Type NodeType, string NodeName, int NetworkMaster = -1, Vector3? SpawnPos = null);
    public Node SpawnNetworkedNode(PackedScene Scene, string NodeName, int NetworkMaster = -1, Vector3? SpawnPos = null);
    public Node SpawnNetworkedNode(string ScenePath, string NodeName, int NetworkMaster = -1, Vector3? SpawnPos = null);
```

`SpawnPos` is only used if the spawned node is a `Node2D` or `Spatial`. For `Node2D`, only the `x` and `y` components are used.

They all return the reference to the Server's instance of the new node. The Node is added as a child to whatever Node you call `SpawnNetworkedNode()` on.
When the server removes the Networked Node from the tree, it will be removed for all players.
When a new player connects, all previously networked Nodes will be created for that player. Synchronizing properties will have to be done manually after that.

## Command Line Arguments
The class `MDArguments` provides many helpers for checking and parsing the command line arguments that your game launched with.

To check if a particular argument exists:
```csharp
if (MDArguments.HasArg("logprofile"))
{
    MDLog.Info(LOG_CAT, "Profiling [{0}] took {1}us", ProfileName, GetMicroSeconds());
}
```

To get the value that an argument was set to, you can use `MDArguments.GetArg()`, `MDArguments.GetArgInt()`, or `MDArguments.GetArgFloat()`, depending on what type you need:
```csharp
// Expects -server=port
if (MDArguments.HasArg("server"))
{
    int Port = MDArguments.GetArgInt("server");
    StartServer(Port);
}
// Expects -client=[IPAddress:Port]
else if (MDArguments.HasArg("client"))
{
    string ClientArg = MDArguments.GetArg("client");
    string[] HostPort = ClientArg.Split(":");
    StartClient(HostPort[0], HostPort[1].ToInt());
}
```

## Command Console
In game, the command console can be opened with the `~` key. Command history can be navigated using the `Up` and `Down` arrow keys. To auto complete the command, hit `Tab`.

In your code, you can add more commands by adding the `[MDCommand()]` attribute to any methods you wish to have as commands.
Then from that same class call `this.RegisterCommandAttributes();` to have those commands registered or see the [Automatic Registration](#automatic-registration) section to have it done automatically.
For classes extending `Node`, they aren't registered automatically as commands are a debug feature, so a good place to call it would be in `_Ready()`, `Node` classes have an extension helper for this, so you can just call `this.RegisterCommandAttributes();`.

Only a single instance of a class can be registered for commands, this is because commands are invoked via their method name, which are the same for all instances of a class.

To call a command on the server from the client, prefix the command with `ServerCommand`, for example `ServerCommand Disconnect`.

You can override `IsConsoleAvailable()` on your GameInstance implementation to change when the console is available, by default it's only available in DEBUG builds.

## Logging
MDLog proivdes various levels of logging and allows you to have separate log levels of logging for console and file. Each category can be set independently.

The available log levels are:
```json
    Trace,
    Debug,
    Info,
    Warn,
    Error,
    Fatal,
    Force
```

To log with a specific level, use the log level as the function. For example, to log an error use `MDLog.Error()`.
The first parameter is the log category string, the second is the format string, and then any arguments to want to pass to your format string:
```csharp
MDLog.Info(LOG_CAT, "Peer [ID: {0}] connected", PeerId);
```
Will give something like
```log
[2020-04-12 16:48:03.404][58][SERVER] [LogGameSession::Info] Peer [ID: 168151027] connected
```
Conditional variants are also available, where the first parameter is a condition that must be true for the log to happen:
```csharp
bool Success = error == Error.Ok;
MDLog.CInfo(Success, LOG_CAT, "Connecting to server at {0}:{1}", Address, Port);
```

Logging with a log level of `Fatal` will trigger a break if the debugger is attached.

To configure the log level for a specific category call:
```csharp
MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel FileLogLevel, MDLogLevel ConsoleFileLogLevel));
```

You can change the log level of a category at runtime using the command `SetLogLevel [Category] [LogLevel]`. For example `SetLogLevel LogMDGameSession Debug`.

## Profiler
MDProfiler is a _very_ simple profiler. It will track the time it takes for a block of code to run and if enabled, log it.
It's intended to be used to compare timings that are also captured with MDProfiler, don't expect measurements to be accurate to real-world timings.

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

## Node Binding
Node-type fields and properties on Node classes can be marked with the `[MDBindNode]` attribute.
The MD framework will automatically assign a child node with the same name to that member.
You can specify the path to look for or specify a different name to look for by passing it the attribute:
`[MDBindNode("/root/MyNode")]` or `[MDBindNode("MyChildNode")]`, this will pass the path to Godot's `Node.GetNodeOrNull()` method.

See the [Automatic Registration](#automatic-registration) section for information on how these are populated and how you can configure that.

## Automatic Registration
Many of MDFramework's features require new nodes to be registered with the MDFramework system. By default MDGameInstance will perform these registrations for you automatically.

There are 2 ways to override that behaviour, the first is to override `public virtual bool RequireAutoRegister();` in your GameInstance to return `true`.
This will then check the Node's class's attributes for `[MDAutoRegister]` before registering. Saving you CPU time from iterating every field on the class when a new node is added.

The second method is to instead give the class you don't want to auto register an attribute:
```cs
[MDAutoRegister(MDAutoRegisterType.None)]
public class NodeThatWontRegister : Node
{}
```

Both these methods will require that you call the registration methods manually:
```cs
this.PopulateBindNodes();
this.RegisterReplicatedAttributes();
this.RegisterCommandAttributes();
```

By default you have to manually register `MDCommand` using `this.RegisterCommandAttributes();` but you can configure a class to auto register everything using
```cs
[MDAutoRegister(MDAutoRegisterType.Debug)]
public class NodeThatAutoRegistersEverything : Node
{}
```

This will autoregister all features, including debug ones like commands.

# Contributing
There are many ways to contribute
* Report bugs using the GitHub issue tracker
* Suggest features that would improve the framework using the GitHub issue tracker
* Submit a pull request that improves documentation or fixes an issue

# TODO
* Enable only specific instances of profile logging (rather than the entire system on/off)
* Optimizations
    * MDReplicated should frame-slice over a set amount of frames rather than sending every frame
    * Add interpolation to relevant MDReplicated values (floats, Vectors, etc)
* Make a test project that gives examples on using all the features that can also be used to test them for development
* Save system (Serialize a class to file)
* Output profiler results to csv
* UI management framework
