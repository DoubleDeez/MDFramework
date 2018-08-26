# MDFramework
A multiplayer C# game framework for Godot 3 dependent on GDNet (https://github.com/PerduGames/gdnet3)

# Installation
1. Clone the repo to your Godot project's directory.

2. Add the MDFramework files to your `Project.csproj`, inside of `<ItemGroup>`, making sure the path matches where you cloned the repo. 

```xml
<Content Include="MDFramework\*.cs" />
```

3. Setup your `project.godot` to AutoLoad either `MDGameInstance` or your subclass of it.

```ini
[autoload]
MDGameInstance="*res://MDFramework/MDGameInstance.cs"
```
