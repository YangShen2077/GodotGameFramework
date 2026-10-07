# 单元测试经验（Testing Guide）

> 适用版本：Godot 4.7 + .NET 8 ｜ 对应代码：`TheGame/GameScripts/*/XxxTest.cs`（gdUnit4 C# testsuite）
> 不是运行时系统，而是全项目通用的单测约定与技巧。各玩法文档（`docs/features/*.md`）的"测试"章节只写**功能特有**心得，通用部分链接到本文。

---

## 1. 概览

框架与玩法核心逻辑为**纯 C#**（不依赖 Godot 节点），可用 [gdUnit4](https://gdunit4.readthedocs.io/) C# 测试套件直接跑单测，脱离编辑器与配表即可验证核心逻辑。已有套件：

| 测试文件 | 覆盖对象 |
|------|------|
| `TheGame/GameScripts/Restaurant/CrowdFlowServiceTest.cs` | 客流报告生成器（结构不变量） |
| `TheGame/GameScripts/Restaurant/TableSeatCoreTest.cs` | 空桌/入座分配核心（空桌口径红线、接待资格、入座分配） |
| `TheGame/GameScripts/Combat/CombatResolverTest.cs` | 战斗结算核心（克制/命中/暴击/护甲/反击/连击） |
| `TheGame/GameScripts/Character/CharacterSystemTest.cs` | 角色属性系统 |
| `TheGame/GameScripts/Inventory/InventorySystemTest.cs` | 背包/库存系统 |

约定：被测类若为**纯 C# 可脱离 Godot 单测**（如 `FlowService` / `Resolver` / 领域模型），同步补一份 `XxxTest.cs`，随功能一起提交。

## 2. 运行方式

**headless 跑单个套件**（在 `GodotProject` 下，用真实 Godot 可执行文件路径替换 `<godot_exe>`）：

```
"<godot_exe>" --headless --path GodotProject -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd -a res://TheGame/GameScripts/<Module>/XxxTest.cs --ignoreHeadlessMode
```

也支持在 Godot 编辑器内打开 `GdUnit4` 面板选择套件运行。

> **⚠️ 运行脚本默认静默（headless）**：`GdUnitCmdTool.gd` 的 `_finalize` 只在 `OS.is_stdout_verbose()` 时打印 summary，因此上述命令**成功时无任何输出、`$LASTEXITCODE` 为空是正常的**，不代表没执行。
> 首次跑新套件想确认结果，直接带 Godot 的 `--verbose`：
>
> ```
> "<godot_exe>" --headless --verbose --path GodotProject -s res://addons/gdUnit4/bin/GdUnitCmdTool.gd -a res://TheGame/GameScripts/<Module>/XxxTest.cs --ignoreHeadlessMode
> ```
>
> 会输出 `PASSED/FAILED` 列表与 `Statistics: N test cases ...`。**不要**靠"插一个必然失败的断言再跑"来验证执行机制——先查工具的输出约定，避免空转。

## 3. 套件结构

- 每个套件声明 `[TestSuite]`，每个用例 `[TestCase]`，均置于对应命名空间（如 `GameCombat` / `GameRestaurant`）。
- 断言用 `using static GdUnit4.Assertions;` 的 `AssertThat(x).IsEqual/IsTrue/IsNotNull(...)`。
- 测试文件与被测类同目录、同命名空间，命名为 `XxxTest.cs`。

## 4. 通用技巧（核心）

### 4.1 造假数据，脱离 Godot 与配表

配表行类型（如 `GuestRace`）字段只读、仅能从 Luban `ByteBuf` 构造，纯 C# 直接 new 很难。按被测对象提供注入点，选用以下三种方式之一：

| 方式 | 适用 | 示例 |
|------|------|------|
| **数据源接口注入** | 业务逻辑依赖一组配表数据 → 抽 `IXxxDataProvider` 接口，生产用 `LubanXxxDataProvider`（读 `ConfigSystem`），测试注入匿名/内部假实现 | `CrowdFlowService.Provider` + `ICrowdDataProvider` |
| **ByteBuf 手工反序列化** | 需要直接造某个配表行对象 | 见 `CrowdFlowServiceTest` 的 `MakeRace/MakeIdentity/MakeElement`：`buf.WriteInt/WriteString/WriteSize` 后 `XxxConfig.DeserializeXxxConfig(buf)` |
| **直接构造领域模型** | 被测方法只依赖自己的领域模型（快照/DTO） | `CombatResolverTest` 直接 `new UnitSnapshot {...}` 完全控制命中/暴击/护甲/耐久 |

> 要点：**字段读写顺序与配表定义一致**，否则 `Deserialize` 会错位。`WriteIntList` 先 `WriteSize` 再逐个写。

### 4.2 固定随机种子保证确定性

随机来源可注入（如 `CrowdFlowService.RandomSource`），测试传固定种子 `new Random(seed)`，保证用例结果完全确定、可重复。

### 4.3 结构不变量断言而非精确值

随机/模糊逻辑不好断言精确输出时，转而断言**不变量**：

- 占比和 ≈ 1 且各项非负（`Math.Abs(sum - 1.0) < 0.01`）
- 条目数不超上限（优先收集 ≤3、适量补充 ≤2）
- 文案落在允许的有限档内（同行三档、客流三档）
- 每条记录含必要子串（如"忌…以…覆盖"）

### 4.4 精确值与浮点比较

期望精确数值的用例，用操作数构造到可预测结果（如注释写下 100×1.5×10/11 的推导）；浮点断言用近比较，如 `Math.Abs(a - b) < 0.001f`。

### 4.5 `[RequireGodotRuntime]` 特性

部分用例依赖 Godot 运行时（如用到反射枚举/浮点高层 API）需加 `[RequireGodotRuntime]`；纯 C# 不依赖的用例可省。以实际报错为准——缺运行时跑挂就补上，反之若不需要就删。

## 5. FAQ

- **为什么配表数据要抽 `ICrowdDataProvider`？** 配表类字段只读、仅从 `ByteBuf` 构造，直接造假行繁琐；抽接口后业务逻辑与配表解耦，测试注入假数据即可脱离 Godot/配表。
- **为什么固定随机种子？** 否则用例每次结果漂移，无法稳定回归。
- **惰性造假要选哪种？** 测试只依赖领域模型 → 直接构造（Combat）；依赖整组配表 → 接口注入 + ByteBuf 构造具体行（CrowdFlow）。

## 6. 后续/边界

- 目前仅覆盖纯 C# 核心逻辑；UI 表现层、场景切换（`LoadSceneAsync` 链路）尚未有自动化测试，仍靠手动运行游戏验证。
- 各玩法功能特有的测试要点，见 `docs/features/*.md` 各自"测试"小节（链接回本文通用部分）。