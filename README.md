# MDFramework
A multiplayer C# game framework for Godot 3 dependent on GDNet (https://github.com/PerduGames/gdnet3)

# Installation
Clone the repo to your Godot project's directory.

Add the MDFramework files to your `Project.csproj`, inside of `<ItemGroup>`, making sure the path matches where you cloned the repo. 

```xml
<Content Include="MDFramework\*.cs" />
```

Setup your project AutoLoad to load either `MDGameInstance` or your subclass of it.