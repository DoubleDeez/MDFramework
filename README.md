# MDFramework
A multiplayer C# game framework for Godot 3.3.

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
1. Add the repo as a submodule to your Godot project's directory (for me I added it to `src\MDFramework`).
```bash
git submodule add https://github.com/DoubleDeez/MDFramework.git src/MDFramework
```

2. Setup your `project.godot` to AutoLoad either `MDGameInstance` or your subclass of it:

```ini
[autoload]
GameInstance="*res://src/MDFramework/MDGameInstance.cs"
```

3. Checkout the [wiki](https://github.com/DoubleDeez/MDFramework/wiki) for details on how to use MDFramework.

4. When exporting, under the resources tab, make sure to include `*.ini` to package the config files.

# Examples

Checkout the [Examples Repo](https://github.com/DoubleDeez/MDFramework-Examples) for examples on how to use various features of MDFramework.

# Help

If you're struggling to get something working or have a question either [log an issue](https://github.com/DoubleDeez/MDFramework/issues) or [reach out on discord](https://discord.gg/UH49eHK).
