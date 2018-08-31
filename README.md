# MDFramework
A multiplayer C# game framework for Godot 3 dependent on the GDNet module (https://github.com/PerduGames/gdnet3)

Being built along side my own Godot project.

# Features
* Command line parameter parsing that can be queried at any time
* A simple logging system with logging categories and different log levels for each category for both writing to file and stdout
* A console command prompt that allows you to add commands from single-instance classes

# Installation
1. Clone the repo to your Godot project's directory.

2. Add the MDFramework files to your `Project.csproj`, inside of `<ItemGroup>`, making sure the path matches where you cloned the repo. 

```xml
    <Compile Include="src\MDFramework\MDAttributes\MDCommandAttribute.cs" />
    <Compile Include="src\MDFramework\MDExtensions\MDControlExtension.cs" />
    <Compile Include="src\MDFramework\MDExtensions\MDNodeExtension.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDArguments.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDCommands.cs" />
    <Compile Include="src\MDFramework\MDHelpers\MDLog.cs" />
    <Compile Include="src\MDFramework\MDInterface\MDConsole.cs" />
    <Compile Include="src\MDFramework\MDInterface\MDInterfaceManager.cs" />
    <Compile Include="src\MDFramework\MDGame.cs" />
    <Compile Include="src\MDFramework\MDGameInstance.cs" />
    <Compile Include="src\MDFramework\MDGameSession.cs" />
    <Compile Include="src\MDFramework\MDNetEntity.cs" />
    <Compile Include="src\MDFramework\MDPlayer.cs" />
```

3. Setup your `project.godot` to AutoLoad either `MDGameInstance` or your subclass of it.

```ini
[autoload]
GameInstance="*res://src/MDFramework/MDGameInstance.cs"
```
**Note:** It **must** be called `GameInstance` for the project to work.

# TODO
* An ability to disable command prompt (especially for release builds)
* Command prompt auto-complete with help text
* Command history
* Instance based replication (use name to find the node?)
  * Easy replication of fields
  * Client/Server/Broadcast functions
* more...
