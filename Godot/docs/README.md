# GGF 文档索引

> GGF (Godot Game Framework) — Godot 4.7 + C# (.NET 8)，[Game Framework](https://gameframework.cn/) 的 Godot 移植。
> 本目录为各系统的深度介绍文档。项目总览与开发命令见仓库根 `CLAUDE.md`。

## 框架核心

| 文档 | 内容 |
|------|------|
| [FrameworkCore.md](FrameworkCore.md) | 双层架构、启动/关闭序列、GodotComponent 生命周期、GF 门面、ReferencePool、日志系统、SingletonNode、PhysicsCheck2D、GTween、LayerMask、NodeExtension |
| [EventSystem.md](EventSystem.md) | EventPool 机制、EventId 约定、Fire vs FireNow、订阅退订、池化回收禁忌 |
| [FsmSystem.md](FsmSystem.md) | IFsm/FsmState 泛型设计、状态生命周期、SetData/GetData、销毁与池化 |
| [ProcedureSystem.md](ProcedureSystem.md) | 流程 = 顶层 FSM、启动链路、TheGame 流程链 Launch→Update→Prelode→Game、新增流程教程 |
| [DebuggerSystem.md](DebuggerSystem.md) | 运行时调试器：FPS 图标 + Console/Information/Profiler/Other 多级页签、BBCode-IMGUI 绘制模型、框架日志+Godot 原生日志双源捕获、Profiler 含 Resource/WebRequest/Download 代理计数、自定义调试窗口 |

## 资源与内容

| 文档 | 内容 |
|------|------|
| [ResourceSystem.md](ResourceSystem.md) | ResourceMode 现状、异步加载队列（Asset+Binary）、IResourceLoadHelper 可配置加载辅助器、子包加载（热更+Package本地）、ExportInspector 导出工作流、ResourcesCollectionConstant |
| [DataTableSystem.md](DataTableSystem.md) | Luban 管线（Excel→C#+二进制）、ConfigSystem 懒加载、新增表步骤、4 张配置表、Config 子包热更时序 |
| [DataNodeSystem.md](DataNodeSystem.md) | 树形数据结构、路径访问语义、Variable 池化类型 |
| [SettingSystem.md](SettingSystem.md) | ConfigFile → user://Settings.cfg、Save/Load 语义、与 EasySave 的区别 |
| [LocalizationSystem.md](LocalizationSystem.md) | TSV 字典格式、语言决定链、IStringKey 刷新机制、翻译工作流（TopMenu → Generate File → Localization File） |

## 游戏对象

| 文档 | 内容 |
|------|------|
| [EntitySystem.md](EntitySystem.md) | 实体生命周期、实体组+实例池、EntityId 配置驱动、TheGame 继承树（ActorEntity/Cat/Anger/GanTan/LightningBall/DropItem）、新增实体步骤 |
| [UISystem.md](UISystem.md) | UIForm 生命周期与触发条件（OnCover/Reveal/Pause/Resume/Refocus）、UI 组 Refresh 遮挡/暂停算法、OpenUIFormAsync、脚本生成器（Ge/Logic 双文件）工作流 |
| [SoundSystem.md](SoundSystem.md) | 声音组与 Audio Bus 映射、代理抢占算法、PlaySoundParams、AudioStreamPlayer 桥接 |
| [SceneSystem.md](SceneSystem.md) | 场景加载流程、实例挂载位置、LoadSceneMode（Single/Additive 双模式）、LoadSceneAsync、LoadSceneUpdate 进度事件、与 ResourceComponent 的关系 |
| [ObjectPoolSystem.md](ObjectPoolSystem.md) | ObjectBase/IObjectPool 设计、四参数语义、与 ReferencePool 对照、NodePool 系统（IPoolable + GF.ObjectPool 懒加载） |
| [NodePoolSystem.md](NodePoolSystem.md) | NodePool 通用节点池：IPoolable 接口、配置驱动注册、懒加载 Instantiate、孤儿节点设计、NodePoolInspectorPlugin 编辑器扫描 |
| [ArchiveSystem.md](ArchiveSystem.md) | 通用存档系统：ArchiveSystem\<T,U\> 泛型设计、Catalogue/Data 分离模式、CRUD API、AES 存档加密（Rijindael + ArchiveSetting）、EasySave 持久化、GameData 游戏侧定制 |

## 网络与热更

| 文档 | 内容 |
|------|------|
| [WebRequestSystem.md](WebRequestSystem.md) | 三层架构（Component/Manager/Helper）、TaskPool 排队与优先级、SendRequestAsync、超时约定、与 Download 模块的分工（小文本 vs 大文件） |
| [DownloadSystem.md](DownloadSystem.md) | 下载模块全貌：任务队列/断点续传/校验/DownloadFileAsync/热更集成/错误语义表 |
| [ResourceHotUpdateAudit.md](ResourceHotUpdateAudit.md) | 资源热更审计：风险项清单与修复状态（2026-07 复审，致命项全部修复 ✅，维持现状维护） |
| [CodeHotUpdateDesign.md](CodeHotUpdateDesign.md) | ⚠️ **已搁置** — C# 程序集热更方案设计（ALC），等待华佗团队完成 Godot 热更适配后重启

## 玩法功能

> 每个玩法功能一篇，记录需求规则、红线、架构与踩坑，让 AI 改代码前快速理解。模板与维护约定见 [features/README.md](features/README.md)（功能完成即更新）。

| 文档 | 内容 |
|------|------|
| [features/CombatSystem.md](features/CombatSystem.md) | 日间探索战斗（打木桩演示）：三预制体分离、场景切换链路、方案B 恢复标记、D4 临时代码数值 |
| [features/DayCycleSystem.md](features/DayCycleSystem.md) | 日循环：早→中→晚→经营后 四阶段 FSM、手动推进、方案B 阶段恢复 |
| [features/RestaurantSystem.md](features/RestaurantSystem.md) | 经营与客人（**总纲**）：玩法整体、端到端数据流、横切服务（HUD/手持）、配表总表、测试总览；子领域见下 |
| [features/GuestSystem.md](features/GuestSystem.md) | 客人系统：客人生成/自动入座/生命周期/耐心/上菜/结账、营业节奏 |
| [features/StaffSystem.md](features/StaffSystem.md) | 员工系统：员工安排/餐馆升级/员工 AI（厨师限量、服务员上菜点单） |
| [features/CookingSystem.md](features/CookingSystem.md) | 后厨与做菜：多灶台并行、备菜/取菜、InventorySystem 衔接 |
| [features/MenuOrderSystem.md](features/MenuOrderSystem.md) | 菜单与订单：正式菜单、点单选菜、订单队列（开放订单）、上菜核销 |
| [features/SatisfactionSystem.md](features/SatisfactionSystem.md) | 顾客满意度：同行组共享最终满意度 / 当晚平均 S（按人数加权）、厨艺体验分、出品质量逐份归因（DishQuality）、控制台公式日志 |
| [features/CrowdReportSystem.md](features/CrowdReportSystem.md) | 客流报告：今日概况 + 备料方向（优先/适量/特殊）、随机抽取六步流程、数据源注入 |
| [features/InventorySystem.md](features/InventorySystem.md) | 背包/仓库/商店：网格容器、容器级堆叠策略、跨区转移/买卖/烹饪 |
| [features/Common.md](features/Common.md) | 全局速度与暂停（Common）：Tab 循环 1x/2x/4x、打开配表 pauseScene 界面暂停场景、纯 C# GameSpeedService + 公共层 GameSpeedSystem |

## 项目设计经验（architecture/）

> GGF **框架**系统文档见上方 Engine/Framework 分区；本区存放 **TheGame 项目侧**的架构设计与测试经验，避免与框架文档混淆。

| 文档 | 内容 |
|------|------|
| [architecture/ArchitectureDesign.md](architecture/ArchitectureDesign.md) | 领域架构与程序设计经验（跨玩法）：表现/领域分层、可测准绳、单一真值 owner、聚合根不变量、DDD 落地取舍、应用层试点 OrderingService、DI 演进方向 |
| [architecture/TestingSystem.md](architecture/TestingSystem.md) | 单元测试经验：gdUnit4 C# 套件、headless 运行、脱离配表造假数据三种方式、固定随机种子、结构不变量断言 |

## 其他

| 文档 | 内容 |
|------|------|
| [engine-reference/](engine-reference/) | 引擎版本参考资料
- 文档风格约定：中文；开头引用块标注适用版本与代码路径；章节顺序为 概述 → 架构与数据流 → 文件清单 → 核心机制 → 组件与 API → FAQ → 已知边界；**所有断言以实际代码为准**，与 CLAUDE.md 冲突时以系统文档为准（CLAUDE.md 与本文档已于 2026-08 同步修订）。玩法功能文档另见 [features/README.md](features/README.md) 的完整模板
