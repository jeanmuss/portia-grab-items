# Building Portia Grab Items

[Chinese build instructions](BUILDING.zh-CN.md)

Use Windows PowerShell and the .NET Framework C# compiler included with Windows. Supply a local reference directory with:

- From your game: `Assembly-CSharp.dll`, `mscorlib.dll`, `System.dll`, `System.Core.dll`, `UnityEngine.dll`, and the available `UnityEngine.*Module.dll` files.
- From BepInEx 5: `BepInEx.dll` and `0Harmony.dll`.

Use the game's CLR 2.0 assemblies: `mscorlib` version 2.0.0.0 and `System.Core` version 3.5.0.0. Do not substitute .NET 4 runtime references.

```powershell
.\build.ps1 -ReferenceDirectory C:\PortiaModReferences
```

The script uses `/noconfig /nostdlib+` to avoid implicit .NET 4 references. It derives the release version from the plugin source and produces a plugin-only ZIP in `dist/`, containing both language versions of the user documentation. It does not launch the game or modify an installed game.

Do not commit the reference assemblies. They, personal saves, logs and decompiled game sources are not release content.
