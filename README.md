# MDFramework
A multiplayer C# game framework for Godot 3.2.

As this is being built along side my own Godot project, I will generally add features as I need them.

## Why?
There are a lot of features from other game engines that I'm used to, so I wanted to build something that made those features available in Godot.

# Features
* [Command line parameter parsing](#command-line-arguments) that can be queried at any time
* A simple [logging system](#logging) with logging categories and different log levels for each category for both writing to file and stdout
* A [console command](#command-console) prompt that allows you to add commands from single-instance classes
* A simpler [profiler](#profiler) class to determine execution time of a code block
* Bind editor-created nodes to member variables automatically with [node binding](#node-binding)

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
    <Compile Include="src\MDFramework\MDGameInstance.cs" />
```

3. Setup your `project.godot` to AutoLoad either `MDGameInstance` or your subclass of it:

```ini
[autoload]
GameInstance="*res://src/MDFramework/MDGameInstance.cs"
```
**Note:** It **must** be called `GameInstance` for the framework to work.

# How to use MDFramework
## Command Line Arguments
TODO docs

## Command Console
In game, the command console can be opened with the `~` key. Command history can be navigated using the `Up` and `Down` arrow keys.

In your code, you can add more commands by adding the `[MDCommand()]` attribute to any methods you wish to have as commands.
Then from that same class call `MDCommands.RegisterCommandAttributes(this);` to have those commands registered.
For classes extending `Node`, they aren't registered automatically as commands is a debug feature, so a good place to call it would be in `_Ready()`, `Node` classes have an extension helper for this, so you can just call `this.RegisterCommandAttributes();`.

Only a single instance of a class can be registered for commands, this is because commands are invoked via their method name, which are the same for all instances of a class.

## Logging
TODO docs

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
Node-type fields and properties on Node classes can be marked with the `[MDBindNode()]` attribute.
The MD framework will automatically assign a child node with the same name to that member.
You can specify the path to look for or specify a different name to look for by passing it the attribute:
`[MDBindNode("/root/MyNode")]` or `[MDBindNode("MyChildNode")]`.

# TODO
In no particular order:
* Ability to enable command prompt in release builds
* Command prompt auto-complete with help text
* Ability to call console commands on the server from the client
* UI management framework
* Enable only specific instances of profile logging (rather than the entire system on/off)
* Save system (Serialize a class to file)
* Optimizations
* Make a test project that gives examples on using all the features that can also be used to test them for development
* Built in way for Server to spawn a node locally and on clients
