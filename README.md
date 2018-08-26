# MDFramework
A multiplayer C# game framework for Godot 3 dependent on GDNet (https://github.com/PerduGames/gdnet3)

# Installation
Clone the repo to your Godot project's directory.

Add the MDFramework files to your `Project.csproj`, inside of `<ItemGroup>`, making sure the path matches where you cloned the repo. 

```xml
<Compile Include="MDFramework\MDGameInstance.cs" />
<Compile Include="MDFramework\MDGameMode.cs" />
<Compile Include="MDFramework\MDGameSession.cs" />
<Compile Include="MDFramework\MDNetEntity.cs" />
<Compile Include="MDFramework\MDPlayer.cs" />
```

Setup your project AutoLoad to load either `MDGameInstance` or your subclass of it.