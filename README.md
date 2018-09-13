# MDFramework
A multiplayer C# game framework for Godot 3 dependent on the GDNet module (https://github.com/PerduGames/gdnet3).

Being built along side my own Godot project.

# Features
* Command line parameter parsing that can be queried at any time
* A simple logging system with logging categories and different log levels for each category for both writing to file and stdout
* A console command prompt that allows you to add commands from single-instance classes

# Installation
1. Clone the repo to your Godot project's directory (as a submodule if you're also using git).

2. Add the MDFramework files to your `Project.csproj`, inside of `<ItemGroup>`, making sure the path matches where you cloned the repo. 

```xml
    <Compile Include="src\MDFramework\MDAttributes\MDCommand.cs" />
    <Compile Include="src\MDFramework\MDAttributes\MDReplicated.cs" />
    <Compile Include="src\MDFramework\MDExtensions\MDControlExtensions.cs" />
    <Compile Include="src\MDFramework\MDExtensions\MDNodeExtensions.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDArguments.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDCommands.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDLog.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDSerialization.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDStatics.cs" />
    <Compile Include="src\MDFramework\MDInterface\MDConsole.cs" />
    <Compile Include="src\MDFramework\MDInterface\MDInterfaceManager.cs" />
    <Compile Include="src\MDFramework\MDGame.cs" />
    <Compile Include="src\MDFramework\MDGameInstance.cs" />
    <Compile Include="src\MDFramework\MDGameSession.cs" />
    <Compile Include="src\MDFramework\MDNetEntity.cs" />
    <Compile Include="src\MDFramework\MDPlayer.cs" />
    <Compile Include="src\MDFramework\MDReplicator.cs" />
```

3. Setup your `project.godot` to AutoLoad either `MDGameInstance` or your subclass of it.

```ini
[autoload]
GameInstance="*res://src/MDFramework/MDGameInstance.cs"
```
**Note:** It **must** be called `GameInstance` for the framework to work.

# How to use MDFramework

## Command Console
In game, the command console can be opened with the `~` key.

In your code, you can add more commands by adding the `[MDCommand()]` attribute to any methods you wish to have as commands.
Then from that same class call `MDCommands.RegisterCommandAttributes(this);` to have those commands registered.
For classes extending `Node`, a good place to call it would be `_Ready()`, `Node` classes have an extension helper for this, so you can just call `this.RegisterCommandAttributes();`.

Only a single instance of a class can be registered for commands, this is because commands are invoked via their method name, which are the same for all instances of a class.

## Replication
There are 2 methods of replication with this framework. RPCs (calling a function on a remote system) and field replication (copying the data of a variable from the server to clients).

### RPCs
// TODO

### Field Replication
Setting up replicating has a similar pattern to setting up console commands. Any field on a `Node` class marked with the `MDReplicated()` attribute can be replicated. The registered `Node` **must** have its name set before being registered, and the name must be the same on the server and all clients as this is how `MDReplicator` determines where to send replicated data. Fields are always reliably replicated, although order isn't necessarily guaranteed since all fields are replicated as a post-process - they are not sent out as soon as you assign the variable.

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
* Dictionary/List/Array of the above types
* Any of the above types on Classes/Structs

# TODO
* An ability to disable command prompt (especially for release builds)
* Command prompt auto-complete with help text
* Command history
* Instance based replication (use name to find the node?)
  * Easy replication of fields
  * Notification of a change in a replicated field
  * Client/Server/Broadcast functions (RPCs)
* Relevancy replication prioritization
* Distance based replication relevancy
* UI management framework
* Automatic Field<->Node binding
* More...
* Optimize, threading, convert performance critical features to C++
