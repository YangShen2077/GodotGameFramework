---
name: luban-dev
description: Luban 游戏配置全栈工具，支持枚举/Bean/数据表的增删改查、代码生成、GGF 集成。触发场景：(1) 编辑游戏配置数据（配置表/数据表/道具表/技能表/奖励表/活动表），(2) 新增/修改/删除配置表结构，(3) 定义枚举/Bean/字段，(4) 导表/生成配置代码，(5) 编写 luban.conf 或 Schema 定义，(6) Luban 类型系统/校验器问题。即使用户未明确说"Luban"，只要是编辑游戏配置数据，也应使用此技能。
---

# Luban 数据配置工具

## GGF 项目核心约定

- **生成格式**：`cs-bin`（C# 代码）+ `bin`（二进制数据）
- **命名空间**：`GameConfig`（非默认 `cfg`）
- **数据加载**：`ConfigSystem.Instance.Tables` 懒加载，底层走 Godot `FileAccess` 读取 `res://TheGame/DataTables/GameConfigs/{0}.bytes`
- **代码位置**：`Godot/GodotProject/TheGame/GameScripts/GameProto/GameConfig/`（Luban 自动生成，勿手改）
- **数据位置**：`Godot/GodotProject/TheGame/DataTables/GameConfigs/`（`.bytes`）
- **配置工程**：`Configs/GameConfig/`（位于仓库根，即 `Godot/` 的上一级）
- **表定义**：Excel（`__tables__.xlsx`/`__beans__.xlsx`/`__enums__.xlsx`）+ XML Schema（`Defines/`）
- **导出数据 → 使用导出脚本，不要手动拼 dotnet 命令**

### 导出脚本

所有脚本位于 `Configs/GameConfig/`，脚本内部会 `cd` 到自身目录，可用完整路径或相对仓库根的路径调用。

| 脚本 | 用途 | 说明 |
|:---|:---|:---|
| `gen_code_bin_to_project_lazyload` | 客户端（**推荐**，懒加载模板） | AI 调用此脚本 |
| `gen_code_bin_to_project` | 客户端（标准模板） | 非懒加载 |
| `gen_code_bin_to_server` | 服务端 | - |

### AI 调用导表命令

根据操作系统选择对应扩展名（`AI_MODE=1` 跳过 `.bat` 结尾的 `pause`）：

**Windows（从 Godot/ 目录）：**
```bash
cmd //c "set AI_MODE=1 && ..\\Configs\\GameConfig\\gen_code_bin_to_project_lazyload.bat"
```

**macOS/Linux：**
```bash
bash ../Configs/GameConfig/gen_code_bin_to_project_lazyload.sh
```

### 新增配置表流程

1. 在 `Datas/__tables__.xlsx` 注册新表
2. 创建 `Datas/xxx.xlsx` 数据表
3. 在 `__beans__.xlsx` / `__enums__.xlsx` 定义复合类型/枚举（可选）
4. 运行导出脚本生成代码和数据
5. 在 `TheGame/GameScripts/Manager/` 封装 `XxxConfigMgr`

### 配置管理红线

- **不要直接修改** `TheGame/GameScripts/GameProto/GameConfig/` 下的生成代码
- 复杂模块封装 `XxxConfigMgr`，业务代码通过管理器访问
- `ConfigSystem.cs` 和 `ExternalTypeUtil.cs` 是桥接文件，改逻辑需同步 `Configs/GameConfig/CustomTemplate/` 模板
- 新增字段前向兼容，删除/改名字段不兼容

---

## 配置表操作工具（luban_helper.py）

通过 Python 脚本直接 CRUD Excel 配置表，无需手动编辑 xlsx。

### 前置条件

Python 3.8+，`pip install openpyxl`

### 执行方式

```bash
python scripts/luban_helper.py --data-dir ../Configs/GameConfig/Datas <command>
```

`--data-dir` 必须放在子命令之前。PowerShell 中用 `;` 分隔命令，JSON 参数推荐用 `--file` 从文件读取。

**参数类型**：位置参数（大写，如 `NAME`、`TABLE`）直接传值，不加 `--` 前缀；可选参数带 `--` 前缀。例如 `table get test.TbItem`（正确）而非 `table get --name test.TbItem`（错误）。

### 命令速查

| 分类 | 命令 | 功能 |
|------|------|------|
| 枚举 | `enum list/get/add/update/delete` | 枚举 CRUD |
| 结构 | `bean list/get/add/update/delete` | Bean CRUD |
| 表 | `table list/get/add/update/delete` | 表 CRUD |
| 字段 | `field list/add/update/delete/disable/enable` | 字段操作 |
| 数据 | `row list/get/query/add/update/delete` | 数据行操作 |
| 批量 | `batch fields/rows` | 批量操作 |
| 导入导出 | `export/import` | JSON 导入导出 |
| 验证 | `validate` / `ref` | 数据验证 / 引用检查 |
| 类型 | `type list/validate/suggest/search/guide/info` | 类型系统 |
| 自动 | `auto list/create` | 自动导入表（`#` 前缀，不推荐） |
| 管理 | `rename/copy/diff/template` | 表管理工具 |

### 操作规范

- **只读操作**（list/get/query/search）：直接执行
- **写入操作**（add/update/delete）：**必须确认**后再执行
- **删除操作**：先 `ref` 检查引用，提醒风险，二次确认
- **修改前**：先 `table get` / `field list` 确认结构，`row get` 避免主键冲突
- **修改后**：用 `validate` 验证

### 分组自动推断

添加字段时不指定 `--group`，自动推断：
- `c`（客户端）：name, desc, icon, image, model, effect, sound, ui 等
- `s`（服务器）：server, logic, damage, hp, mp, exp, level, rate 等
- `cs`（两端）：id, 其他无法判断的字段

---

## 官方 CLI 能力补充（排错 / Schema 设计）

以下命令来自官方 Luban.Agent（需已构建 `Tools/Luban.Agent.dll`）。**日常导表仍走上方 GGF 脚本**，仅当排错、查结构、做 Schema 设计时按下述使用。

### 分层排错（生成失败排查）

1. 加 `--errorFormat json` 解析 `errors[]`
2. 看 `category`：`schema` / `data` / `validation` / `codegen` / `cli`
3. 有 `file` + `location` + `fieldPath` 先修对应单元格/字段
4. 排查顺序：CLI(`-conf`/`-t`/`-c`/`-d`) → Schema(group/value_type/继承) → Data(类型/枚举/必填/分隔符) → Validation(ref/range/path) → Codegen(关键字/非法标识符)

> 原则：修好数据或按程序意图改 schema；**禁止削弱校验来通过生成**（除非用户明确要求并说明风险）。

```bash
# 只校验（Agent CLI）
dotnet Luban.Agent.dll validate --conf luban.conf -t all
# 分层报错（主 CLI）
dotnet Luban.dll --conf luban.conf -t all -f --strict --errorFormat json -x outputSaver=null
# 导出 / 查询 schema
dotnet Luban.Agent.dll schema --conf luban.conf -t all
dotnet Luban.dll --conf luban.conf -t all -c schema-json -x outputCodeDir=./schema-out
```

### Schema 设计原则

- **契约优先**：程序维护 Schema，策划填 Data。先登记表，再填数据
- 复杂 GamePlay（技能/行为树）优先 OOP 继承/多态，不塞字符串；敏感字段用 `s` group 保护，勿泄漏到 `c`
- 选型：扁平行表 → Excel + `read_schema_from_file`；多模块复用 bean → XML `Defines/*.xml` / `__beans__`；多态 → 抽象 bean + 子类，Excel 填类型名/别名；一对多嵌套 → `list,Bean`
- 类型要点：容器 `list,T` / `map,K,V`（**元素不可 `list,int?`**）；可空 `T?`；引用 `int#ref=module.TbX`；字段名建议 snake_case
- 检查清单：主键与 `mode`（map/list/one）匹配、group 覆盖两端、多态子类均定义且可区分、用 `-c schema-json` 或 MCP `GetSchema` 复核

### 校验器速查（发布建议 `--strict`）

| 能力 | 示例 |
|:---|:---|
| 非默认 | `int!`、`int?!` |
| 引用 | `int#ref=item.TbItem`；可跳过 0 `int#ref=item.TbItem?` |
| 范围 | `int#range=[1,100]` |
| 路径 | `string#path=unity` + `-x pathValidator.rootDir=...` |
| 集合大小 | `(list#size=4),int` |
| 允许值 | `int#set=1;2;3` |
| 正则 | `string#regex=^[a-z]+$` |

容器约束加在容器上 `(list#size=n),T`；可空为 null 时多数引用校验会跳过。详见 [validators.md](references/validators.md)。

### Excel 填表约定

- A1 以 `##` 开头才能识别（否则整张 sheet 忽略）；`##var` 字段名 / `##type` 类型 / `##group` 分组(`c`/`s`/`e`) / `##` 注释行 / `#` 开头列=注释列不导出
- 空字符串填 `""`；枚举可填名字或 alias；嵌套/多态按样表填，不 DIY 分隔符。详见 [excel-format.md](references/excel-format.md)

### 运行时加载

- 一个 `Tables` 聚合所有表（项目即 `ConfigSystem.Instance.Tables`，已符合），避免每表静态全局单例，便于热更/测试
- `-c` 与 `-d` 格式匹配（项目：`cs-bin` + `bin`）；客户端不要加载仅 `s` group 的表/字段

---

## 参考文档（按需加载）

| 场景 | 文档 | 内容 |
|------|------|------|
| GGF 集成 / ConfigSystem / 生成脚本 / Excel规范 | [ggf-integration.md](references/ggf-integration.md) | 项目结构、加载器、导出脚本、路径约定 |
| 操作工具命令详解 / Excel结构 / 数据填写格式 | [operating-guide.md](references/operating-guide.md) | luban_helper.py 完整命令参考 |
| 类型系统和语法 | [type-system.md](references/type-system.md) | 基础/容器/自定义/可空类型、Mapper、constalias |
| Schema 定义（XML/Excel）/ 多态规范 | [schema.md](references/schema.md) | enum/bean/table 定义、字段属性、多态bean、flags约束 |
| luban.conf 配置 | [luban-conf.md](references/luban-conf.md) | 完整配置项、分组策略、级联选项、topModule |
| 校验器类型和用法 / ref行为 | [validators.md](references/validators.md) | ref/range/path/size/set/index/!、Ref字段命名、ResolveRef |
| Excel 数据格式 | [excel-format.md](references/excel-format.md) | 标题行、容器格式、多态、标签 |
| CLI 命令行参数 | [command-reference.md](references/command-reference.md) | 完整参数列表、代码/数据目标、--variant/--timeZone |
| JSON/XML/YAML/Lua 数据源 | [data-sources.md](references/data-sources.md) | 非Excel数据源格式、多态鉴别符 |
| 运行时加载 / 类型映射 / 本地化工作流 | [runtime.md](references/runtime.md) | Godot 加载、代码风格、本地化完整流程 |

示例：`examples/item-system/`（CSV+XML）、`examples/skill-system/`（JSON+多态）

脚本：`scripts/luban_helper.py`（操作工具）、`scripts/requirements.txt`（依赖）

官方文档：https://www.datable.cn/docs/intro
