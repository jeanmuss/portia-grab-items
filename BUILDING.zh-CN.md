# 编译波西亚取物插件

[英文编译说明](BUILDING.md)

使用系统自带的 Windows PowerShell 和微软框架的编译器，准备一个本地引用目录，包含：

- 游戏中的 `Assembly-CSharp.dll`、`mscorlib.dll`、`System.dll`、`System.Core.dll`、`UnityEngine.dll` 和已有的 `UnityEngine.*Module.dll` 文件。
- BepInEx 5 中的 `BepInEx.dll` 和 `0Harmony.dll`。

必须使用游戏的 CLR 2.0 程序集：`mscorlib` 版本为 2.0.0.0，`System.Core` 版本为 3.5.0.0，不能用系统的第四版运行库代替。

```powershell
.\build.ps1 -ReferenceDirectory C:\PortiaModReferences
```

脚本使用 `/noconfig /nostdlib+` 避免隐式引用第四版运行库，并从插件源码提取版本号。生成的插件压缩包位于 `dist/`，同时附带独立的中英文使用说明。脚本不会启动游戏或修改安装目录。

不要提交引用程序集、个人存档、日志或游戏反编译代码。
