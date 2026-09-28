# VS Code 断点调试（Godot 4.7 + C#）

> 适用版本：Godot 4.7-stable (mono) / .NET 8 / VS Code + C# Dev Kit（`ms-dotnettools.csharp`）+ Godot Tools（`geequlim.godot-tools`）
> 配置文件：`GodotProject/.vscode/launch.json`（唯一调试配置：一键 F5）、`tasks.json`（编译任务）、`settings.json`（引擎路径）
> 本文只讲「怎么让断点命中」，框架自身的运行时调试器（FPS 图标 + Console/Profiler）见 [DebuggerSystem.md](DebuggerSystem.md)。

## 1. 前提：VS Code 必须打开 GodotProject 目录

VS Code 只读取**工作区根目录**下的 `.vscode/`。本仓库的 `.vscode` 在 `Godot/GodotProject/` 里，所以：

- ✅ `File > Open Folder` → `.../Godot/GodotProject`（`GodotProject.csproj`、`GodotProject.sln` 所在目录）
- ❌ 打开上层 `.../Godot/` 目录 → 找不到 `launch.json`，调试下拉框里空空的，也解析不了 `.csproj`

## 2. 用法：一键 F5

`Ctrl+Shift+D` → 选「启动并调试（一键 F5）」→ 按 **F5**（之后每次直接 F5 即可）。

```
VS Code 按 F5
  → preLaunchTask "build"：dotnet build → .godot/mono/temp/bin/Debug/GodotProject.dll(+.pdb)
  → 用 --path <工作区> 直接拉起 Godot 游戏进程（不经过编辑器）
  → .NET 调试器在进程启动时就挂上；GodotProject.dll 一装载，预先打好的断点立即绑定
```

- `GameEntry` / `ProcedureLaunch` 这种**启动期**代码也能断到（DLL 装载前断点处于「待绑定」状态）。
- **不需要**先在 Godot 编辑器里按 F5；Godot 编辑器同时开着也不影响（游戏是独立进程，编辑器不参与这次调试）。
- 不用手动附加、不用在一堆同名进程里挑 PID —— 这是本仓库唯一配置的调试方式（原因见 §5）。
- `GD.Print` / 框架日志原样打进终端（`console: integratedTerminal`）；想改成「调试控制台」就写 `internalConsole`。
- 只编译不起游戏：`Ctrl+Shift+B`（默认 build 任务）。
- **新增 `.cs` 文件**（新 UIForm / Entity / 流程）后，先跑一次 `Ctrl+Shift+P > Tasks: Run Task > build-solutions`（`godot --build-solutions`）重新生成解决方案，否则新文件不在编译单元里。

最小可复制版本（`program` 换成你机器上的 Godot 路径）：

```jsonc launch.json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "启动并调试（一键 F5）",
      "type": "coreclr",
      "request": "launch",
      // 先跑 tasks.json 里的 build（dotnet build），编译失败会自动中止调试
      "preLaunchTask": "build",
      // 与 .vscode/settings.json 的 godotTools.editorPath.godot4 保持一致，换 Godot 目录时两处一起改
      "program": "D:\\Godot\\Godot_v4.7-stable_mono_win64\\Godot_v4.7-stable_mono_win64\\Godot_v4.7-stable_mono_win64.exe",
      // 不带 --editor：直接跑游戏，不经过编辑器
      "args": ["--path", "${workspaceFolder}"],
      "cwd": "${workspaceFolder}",
      // integratedTerminal：GD.Print / 框架日志原样输出在终端里（带颜色）。
      // 想用「调试控制台」看输出就改成 internalConsole。
      "console": "integratedTerminal",
      "stopAtEntry": false,
      "justMyCode": true,
      "requireExactSource": false,
      "symbolOptions": {
        "searchPaths": ["${workspaceFolder}\\.godot\\mono\\temp\\bin\\Debug"],
        "searchMicrosoftSymbolServer": false
      }
    }
  ]
}
```

```jsonc setting.json
{
    // Godot Tools 插件用它来定位引擎；launch.json / tasks.json 里的 Godot 可执行文件路径也要跟这里保持一致。
    "godotTools.editorPath.godot4": "d:\\Godot\\Godot_v4.7-stable_mono_win64\\Godot_v4.7-stable_mono_win64\\Godot_v4.7-stable_mono_win64.exe",

    // 「选择解决方案」
    "dotnet.defaultSolution": "GodotProject.sln",

    // .godot/ 是引擎缓存 + 编译输出（mono/temp/bin），排除掉
    "files.watcherExclude": {
        "**/.godot/**": true
    },
    "search.exclude": {
        "**/.godot/**": true
    }
}
```

```jsonc tasks.json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "detail": "dotnet build（GodotProject.csproj → .godot/mono/temp/bin/Debug）",
      "command": "dotnet",
      "type": "process",
      "args": ["build", "${workspaceFolder}/GodotProject.csproj"],
      "group": {
        "kind": "build",
        "isDefault": true
      },
      "problemMatcher": "$msCompile"
    },
    {
      "label": "build-solutions",
      "detail": "Godot --build-solutions（新增 .cs 文件后重新生成解决方案并编译）",
      "type": "process",
      "command": "D:\\Godot\\Godot_v4.7-stable_mono_win64\\Godot_v4.7-stable_mono_win64\\Godot_v4.7-stable_mono_win64.exe",
      "args": ["--build-solutions", "--path", "${workspaceFolder}", "--no-window", "-q"],
      "problemMatcher": []
    }
  ]
}
```

## 3. 为什么能命中：产物与符号

```
GodotProject.csproj (Godot.NET.Sdk/4.7.0, net8.0)
  dotnet build ──► GodotProject/.godot/mono/temp/bin/Debug/GodotProject.dll
                                                   GodotProject.pdb   ← 断点靠它做「行号 ↔ IL」映射
  Godot 跑游戏（本配置直启，或编辑器 F5）──► 装载的就是上面这份 DLL
```

- Godot 编辑器自己 F5 时也编译到同一个目录、同一个 `Debug` 配置（`project.godot` 的 `[dotnet] project/assembly_name="GodotProject"`），
  所以**编辑器编译**和**VS Code `dotnet build`** 不会互相打架，谁后编译谁生效。
- 改完代码必须重新编译：F5 已用 `preLaunchTask: build` 自动做掉。
- 断点是在**编译期行号 ↔ pdb** 之间匹配的，所以「程序集与源码不同步」是断点失准的头号原因。

## 4. 断点不命中排查清单

**先确认断点位置每帧都会执行**：最省事的基准点是 [GameEntry.cs](../GodotProject/Framework/GodotGameFrameworkCore/Base/GameEntry.cs) 的 `OnUpdate`（`base.OnUpdate(delta);` 那一行）——
每帧都跑，不会被「启动期代码早就执行完了」这种情况干扰。别一上来就断 `ProcedureLaunch` / `OnOpen` 这类一次性入口。

| 症状 | 原因 | 处理 |
|------|------|------|
| 断点是**空心圆**、悬停提示「尚未加载符号」 | 编译失败，或跑的是旧的 DLL/PDB | 重新 F5（会自动重编译）；先看 `Ctrl+Shift+B` 有没有编译错误 |
| 断点是**实心红点但从不停**（连每帧断点也不停） | ① **游戏被暂停**：编辑器 Game bar 的暂停（F9）和 Debugger 面板的 Break 都会停住 `_process/_physics_process`，此时每帧代码根本不执行 ② 程序集是旧的：源码行号已错位，断点被绑到别的语句上 ③ 断点那段代码在当前流程下确实不会走到（例如只在某个按钮回调里） | 先按 F9/恢复运行；再重新编译 + 重新 F5；最后确认代码路径会不会执行 |
| 断点命中到别的行 / 行号错位 | 运行中的程序集与当前源码不同步 | 结束会话重新 F5（会自动先编译） |
| 按 F5 后弹出 Godot「项目管理器」 | `program` 路径不对，或 `--path` 指向的目录里没有 `project.godot` | 检查 `launch.json` 的 `program` 与 `settings.json` 的 `godotTools.editorPath.godot4` 一致；工作区必须是 `GodotProject` |
| 某行断点打不动 / 总被跳过 | 该行被编译条件裁掉 | 日志宏是 `[Conditional]`：`ENABLE_LOG;ENABLE_INFO_AND_ABOVE_LOG`（见 `GodotProject.csproj` 的 `DefineConstants`），未定义符号的 `Log.Debug` 调用整行不生成 IL |
| 想在 `addons/` 的 EditorPlugin 里断点 | 那些代码跑在**编辑器进程**里，不在这个会话里 | 见 §5 |
| 想看 `GodotSharp.dll` 内部（引擎 API 怎么进来的） | `justMyCode` 把系统程序集过滤了 | 临时把 `justMyCode` 改成 `false`；Debug Console 里「无法查找或打开 PDB 文件」是噪音，不是错误 |
| F5 时 `dotnet build` 报文件被占用 | 上一次的游戏进程还没退出 | 结束残留的 `Godot_v4.7-stable_mono_win64.exe` 进程后重试 |

## 5. 为什么不能像 Unity 那样附加到编辑器

Unity 的 Play 模式跑在**编辑器进程内部**，所以「Attach to Unity Editor」就等于附加到游戏代码。
Godot 相反，官方文档写得很直接（[Game embedding](https://docs.godotengine.org/en/stable/tutorials/editor/game_embedding.html)）：

> The game always runs in a separate process, no matter the embedding mode used.

- 编辑器按 F5 只会 `spawn` 一个新进程（同一个 exe，不带 `--editor`）。编辑器与游戏之间只有一条 TCP 远程调试通道（`127.0.0.1:6007`），
  传的是 **GDScript** 调试、远程场景树、Profiler —— **不是** CLR 调试通道。
- **.NET 运行时是进程级的**：编辑器进程里装载的那份 `GodotProject.dll` 只执行 `addons/` 里的 EditorPlugin 代码；游戏逻辑在另一个进程的另一份装载里。
  所以「附加到编辑器」永远断不到游戏逻辑（断点还会显示成实心红点，因为编辑器里确实有同名模块的符号 —— 极具迷惑性）。
- 想要「游戏画面嵌在编辑器里」的观感：Godot 4.4+ 有 Game Embedding（编辑器顶部 Game 主界面 → 右侧下拉 → 关掉 `Make Window Floating on Next Play`）。
  但**嵌入只改变画面呈现，进程依然分开**，对调试模型没有任何影响，且嵌入模式下不支持改窗口模式/全屏等。
- 因此本仓库只配置了「VS Code 直接启动并调试」这一条路：编译、启动、附加一步到位，等价于 Unity 的 F5，且启动期代码也能断。

`addons/` 里的 C#（`ExportInspector`、`ComponentInsoector`、`TopMenu` 等 EditorPlugin）本机未配置调试入口；需要时再加一个 `--editor` 的 launch 配置（把上面的 `args` 改成 `["--path", "${workspaceFolder}", "--editor"]`、`console` 改成 `internalConsole` 即可），但注意它只覆盖编辑器代码，且由它再按 F5 起的游戏是「孙进程」，不在会话里。

## 6. 换了 Godot 版本 / 目录时要改的三处

| 文件 | 键 |
|------|-----|
| `.vscode/settings.json` | `godotTools.editorPath.godot4`（Godot Tools 插件用） |
| `.vscode/launch.json` | `program` |
| `.vscode/tasks.json` | `build-solutions` 任务的 `command` |

（路径必须一致；`${workspaceFolder}` 自动跟随工作区，无需修改。）
