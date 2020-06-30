# MDFramework
A multiplayer C# game framework for Godot 3.2.

As this is being built along side my own Godot project, I will generally add features as I need them.

## Why?
There are a lot of features from other game engines that I'm used to, so I wanted to build something that made those features available in Godot.

# Features
* Automatically [replicate members](https://github.com/DoubleDeez/MDFramework/wiki/Automatic-Member-Replication) without calling `Rset`.
* Allows the server to [spawn nodes over the network](https://github.com/DoubleDeez/MDFramework/wiki/Networked-Node-Spawning) to all clients.
* [Command line parameter parsing](https://github.com/DoubleDeez/MDFramework/wiki/Parsing-Command-Line-Arguments) that can be queried at any time
* A simple [logging system](https://github.com/DoubleDeez/MDFramework/wiki/Logging-System) with logging categories and different log levels for each category for both writing to file and stdout
* A [console command](https://github.com/DoubleDeez/MDFramework/wiki/Command-Console) prompt that allows you to add commands from single-instance classes
* A simple [profiler](https://github.com/DoubleDeez/MDFramework/wiki/Performance-Profiling) class to determine execution time of a code block
* Bind editor-created nodes to member variables automatically with [node binding](https://github.com/DoubleDeez/MDFramework/wiki/Node-Binding)
* A [Game Session and Player Info](https://github.com/DoubleDeez/MDFramework/wiki/Game-Session) system for managing networked multiplayer
* A [Game Synchronizer](https://github.com/DoubleDeez/MDFramework/wiki/Game-Synchronizer) to automatically synchronize the game when a new player joins

# Installation
0. (Optional) I recommend forking the repo so you can track your changes to it, easily merge updates, and reuse it in other projects you have.

1. Add the repo as a submodule to your Godot project's directory (for me I added it to `src\MDFramework`).
```bash
git submodule add https://github.com/DoubleDeez/MDFramework.git src/MDFramework
```

2. Add the MDFramework files to your `Project.csproj`, inside of `<ItemGroup>`, making sure the path matches where you cloned the repo. 

```xml
    <Compile Include="src\MDFramework\Examples\BasicNetworkLobby\BasicNetworkLobby.cs" />
    <Compile Include="src\MDFramework\Examples\BasicNetworkLobby\CustomGameInstance.cs" />
    <Compile Include="src\MDFramework\Examples\BasicNetworkLobby\PeerSynchStatusRow.cs" />
    <Compile Include="src\MDFramework\Examples\BasicNetworkLobby\SyncInterface.cs" />
    <Compile Include="src\MDFramework\Examples\PredictiveSynchronizationExample\ActorSpawner.cs" />
    <Compile Include="src\MDFramework\Examples\PredictiveSynchronizationExample\PredictiveActor.cs" />
    <Compile Include="src\MDFramework\MDAttributes\MDAutoRegister.cs" />
    <Compile Include="src\MDFramework\MDAttributes\MDBindNode.cs" />
    <Compile Include="src\MDFramework\MDAttributes\MDCommand.cs" />
    <Compile Include="src\MDFramework\MDAttributes\MDReplicated.cs" />
    <Compile Include="src\MDFramework\MDExtensions\MDControlExtensions.cs" />
    <Compile Include="src\MDFramework\MDExtensions\MDNodeExtensions.cs" />
    <Compile Include="src\MDFramework\MDExtensions\MDVector2Extensions.cs" />
    <Compile Include="src\MDFramework\MDExtensions\MDVector3Extensions.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDArguments.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDInput.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDCommands.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDLog.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDProfiler.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDStatics.cs" />
    <Compile Include="src\MDFramework\MDInterface\MDConsole.cs" />
    <Compile Include="src\MDFramework\MDInterface\MDInterfaceManager.cs" />
    <Compile Include="src\MDFramework\MDNetworking\MDGameSession.cs" />
    <Compile Include="src\MDFramework\MDNetworking\MDPlayerInfo.cs" />
    <Compile Include="src\MDFramework\MDNetworking\MDReplicator.cs" />
    <Compile Include="src\MDFramework\MDNetworking\MDGameSynchronizer.cs" />
    <Compile Include="src\MDFramework\MDGameInstance.cs" />
    <Compile Include="src\MDFramework\MDTypes.cs" />
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

4. Checkout the [wiki](https://github.com/DoubleDeez/MDFramework/wiki) for details on how to use MDFramework.

# Contributing
There are many ways to contribute
* Report bugs using the GitHub issue tracker
* Suggest features that would improve the framework using the GitHub issue tracker
* Submit a pull request that improves documentation or fixes an [https://github.com/DoubleDeez/MDFramework/issues](issue)
    * When working on a pull request, it's recommended that you create a temporary feature branch in your own fork of MDFramework as it will be squash merged which can mess up the history of the branch you did the work in.
