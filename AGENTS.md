# AGENTS.md

> 项目 Agent 入口文件。**完整项目指南见 [Godot/CLAUDE.md](Godot/CLAUDE.md)**，改代码前先读它。
> 项目介绍、架构与使用示例见根目录 **[README.md](README.md)**。

本仓库是 **GGF**（Godot 4.7 + C# .NET 8 的游戏框架与 TheGame 项目），目录结构：

- `Godot/` — 引擎工程（框架 + TheGame 游戏）
  - `Godot/CLAUDE.md` — 项目总览、双层架构、开发命令、**玩法功能文档索引**
  - `Godot/docs/` — 框架系统文档；`Godot/docs/features/` — 玩法功能文档（每玩法一篇，功能完成即更新）
  - `Godot/.claude/` — **AI 开发工作流**（见下方强调）
  - `Godot/.mcp.json` — CodeGraph 代码知识图谱 MCP 配置
- `Configs/` — Luban 配表（`GameConfig/Datas/*.xlsx` → 生成 C# 代码与数据）

## ⚡ AI 开发工作流（重点）

> 详见 [README.md](README.md) 的「🤖 AI 开发工作流」一节与 `Godot/.claude/docs/skills-reference.md`。

- **技能 Skills**：`Godot/.claude/skills/` 下 17 个活跃技能，推荐入口：
  - 框架开发 → `/ggf-dev`（红线 + 模块文档路由 + API 速查）
  - 配表增删改查 / 导表 → `/luban-dev`
  - 代码深度审查 → `/grill-with-docs`；架构分析 → `/improve-codebase-architecture`；现状审计 → `/project-stage-detect`
- **代理 Agents**：`Godot/.claude/agents/`（godot-csharp-specialist、gameplay-programmer 等专职子代理）
- **钩子 Hooks**：`Godot/.claude/hooks/`（Pre/PostToolUse 校验、SessionStart、Notification、Stop）
- **MCP**：CodeGraph 知识图谱（`Godot/.mcp.json`，SQLite 索引符号/文件/调用关系）

## 关键约定（速查）

- 构建：`cd Godot/GodotProject && dotnet build`
- 配表操作：编辑 `Configs/GameConfig/Datas/` 后跑 `Configs/GameConfig/gen_code_bin_to_project_lazyload.bat` 重新生成
- 改玩法代码前：先读 `Godot/docs/features/` 对应文档；功能完成同步更新该文档
- 所有断言以实际代码为准；与 `Godot/CLAUDE.md` 冲突时以实际代码为准
