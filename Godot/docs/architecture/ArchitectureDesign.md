# 领域架构与程序设计经验（Architecture Design）

> 适用对象：TheGame 开发中反复用到的**跨玩法架构设计经验**（表现/领域分层、可测准绳、单一真值、DDD 落地取舍、依赖注入方向等），区别于 `docs/features/`（单玩法功能）与 `docs/engine-reference/`（Godot 引擎）。
> 目的：让 AI 设计或改代码前，先对齐项目的架构取向与红线，避免偏离既有套路。

---

## 1. 核心分层原则：表现与业务彻底分离

- **表现层（Godot 节点/UI）不放任何业务真值。** `RestaurantWorld` 已退化为纯表现壳——只绑定/驱动节点、实例化预制体、**订阅领域事件落地表现**，不持有状态、不做决策。
- **业务真值与决策放纯 C# 领域服务**（`*Core`/`*Service` 静态类），无 Godot/框架依赖，可 headless 单测。
- 为什么要这样：后续所有 BUG（厨师重复做菜、战斗不回营业、点单残留锁）都能**快速定位到某个领域服务**，而不是在节点里翻。这是本次重构最大的投资回报。

红线：改逻辑先定位对应领域 Service，表现壳只订阅事件落地。

## 2. "静态纯 C# 类" 与 "Singleton 壳" 的分工

| 形态 | 用途 | 示例 |
|------|------|------|
| 静态 `*Core`/`*Service`（纯 C#） | **可测逻辑**、领域决策 | `RestaurantMenuCore`、`GuestService`、`StaffDispatchService` |
| `Singleton<T>` 系统壳 | **跨场景真值 + 框架生命周期**，逻辑 delegate 到 Core | `DayCycleSystem`（持 FSM owner + phase/day 真值） |

关键坑：`Singleton<T>` `Instance` 首次访问会触发框架注入（`UpdateDriver`），**在纯测试进程会 NRE**。所以生来可测的逻辑必须放静态类，System 只做壳、几乎不需测。这条"可测性"约束反推了分层设计。

## 3. 单一真值 owner，删除中间桥

- 每个业务真值**只有一个 owner**；调用方直接连对应 Service/System，**不设静态转发桥**。
- 反例教训：历史 `RestaurantSession` 作静态会话桥，把每个真值转发一遍，职责混乱、随规模积累腐化。重构后整体删除，调用方直连 `DayCycleSystem`/`OfficialMenuService`/`GuestService` 等。
- 判断标准：一旦出现"为转发而存在"的层，就是腐化信号，尽早删。

## 4. 系统间协作：调用 vs 事件

**准则（红线）**：有明确因果、时序上必须串行的同步步骤，用**调用/委托注入**（含"组合根接线的纯 .NET 领域事件"——本质是多播委托注入，发布方不 import 执行方）；**禁止**用 `GF.Event` 那种"发完即忘"的事件总线把命令伪装成通知。只有真正"可选的广播"（多个互不相关的听众、各自都可选、不要求时序与完成保证）才用事件。

- 正例（应用场景）："每天结束必须清理仓库过期食材"——单一听众（库存域）、且要求**必然完成**。`DayCycleSystem` 只广播纯 .NET `DayEnded(int)`（不 import `GameInventory`），装配层（`ProcedureGame` 组合根）用 `+=` 订阅后调 `InventorySystem.PurgeStorageExpired` + 打日志；清理归口库存域，触发方与执行方经组合根接线，`+=` 即委托注入，"必须执行"由组合根的显式接线保证。
- 反例（过度事件化）：改用 `GF.Event` 事件总线广播（发布/订阅隐式关联、无组合根显式接线）。三类代价：
  1. **因果断裂**——发布方无法验证"清理发生了"，单看发布方代码规则不存在了；
  2. **订阅者寿命不可控**——订阅未挂上/重复挂/释放未退订，导致漏清或重复清，静默失效难排查；
  3. **接口变松**——事件 ID/回调签名错误全部运行期才炸，直调编译期就挡错。
- 何时事件才合适：多听众、互不依赖、缺一不影响规则成立，如 `Guest.PhaseChanged` 广播给表现层各自刷新（本项目即用"领域事件 → 表现订阅"）。
- 区分要点：**纯 .NET `event`**（本程序集、组合根显式 `+=` 接线、无需 args 契约对象）≈ 委托注入，允许；**`GF.Event` 事件总线**（跨程序集 args 契约 + ReferencePool/copy-on-forward + 双方隐式关联）才是"发完即忘"，普通跨域同步步骤不必背此成本。

## 5. 聚合根保护不变量

- 业务状态不变量由**聚合**作为唯一入口保护，不靠表现层自觉。
- 例：`Guest` 聚合（`Waiting→Approaching→Ordering→Dining→Leaving`）——非 Waiting 不可再分座、点单读条暂停耐心、上菜才出队；所有状态迁移必须经聚合方法，外部只读/转发。
- 多个服务交互同一实体时，只有聚合能保证状态不被打破。

## 6. 可测性是检验"纯"的试金石

- 项目红线：**核心逻辑必须可脱离 Godot/配表，用假数据注入做纯 C# headless 单测**。
- 真相由测试暴露：`SetPrompt` 同值事件、`PlayerHandService` 去重、厨师在途缺口——都是单测抓出来的。
- 一个模块能否 headless 单测，直接反映它够不够"纯"；做不到就说明还有框架/节点依赖没抽净。
- 造假方式标配：`internal` 构造 + 假数据源（`IGuestFoodSource`/`ICrowdDataProvider`）接口注入 + 固定随机种子，不经配表/ByteBuf。

## 7. 文档按"领域服务归属"组织，以代码为准核对

- 文档按**领域服务归属**拆分子篇（`GuestSystem`/`StaffSystem`/`CookingSystem`/`MenuOrderSystem`…），总纲只留概述/数据流/配表/测试总览，比按"页面/流程"组织更抗版本漂移。
- 严格以实际代码核对 API（命名、归属），拆分后 `git grep` 验收零残留引用。
- 教训：大文档中曾残留 4 处已删类的引用（`RestaurantSession`），文档与代码同步过期。

## 8. 迁移节奏：小步多次，别一次大重构

- 按模块逐个迁移（HudFlow → PlayerHand → Forecast → 删桥），每步可编译、可测、可独立提交。
- 出 BUG 能精确归因到当次迁移；`恢复标记 + 兜底覆盖`（如战斗返回 `ResumeBusiness`）就是小步迁移中靠实测补上的。
- 若跨文件多，先用 TodoWrite 列步骤、逐项完成，不要一蹴而就。

## 9. DDD 落地程度与取舍

本次重构**贯彻的是 DDD"战术模式"的子集**，未达完整 DDD：

**已落实（符合战术）**
- 聚合根 + 不变量（`Guest`）
- 领域事件（`Guest.PhaseChanged/OrderChanged/...`）
- 领域服务（`BusinessSystem`/`StaffDispatchService`/…，静态类实现）
- 简化内存仓储（`GuestService`）
- 基础设施接口（`IGuestFoodSource`/`ICrowdDataProvider`）
- 表现层剥离（`RestaurantWorld` 壳）

**未落实 / 偏差**
1. **缺显式应用层 / 用例编排层**——编排曾散在表现壳。已用 `OrderingService` 补第一个试点（见 §10）。
2. **领域逻辑偏"过程式 + 成串参数"**——如 `ChooseOrder(menu, racePrefer, identityPrefer, identityAvoid, rng)` 裸 5 参，非 Rich Domain Model 协作；`FoodTag` 列表等是裸结构，未建模成值对象。
3. **全局静态可达**——`ConfigSystem.Instance`、`GuestService.X` 处处静态直取，领域无法独立替换/演进（DI 改造方向见 §11）。
4. **限界上下文松散**——点单/做菜/上菜共用 `GuestService`，本质仍是一大上下文高内聚，未真正切开多个 Bounded Context。
5. **仓储仅内存版、无持久化抽象**——未抽 `IRepository` 接口，将来存盘需改领域。

## 10. 应用层试点：`OrderingService`

把"点单一对客"用例的编排收进一个纯 C# 应用服务（`Begin`/`Cancel`/`Finish`），玩家与服务员的点单统一入口：

- `Begin(guest, 门卫谓词)`：阶段校验 + 业务门卫（营业中、未手持菜，**谓词注入以保持可测**）+ 暂停耐心
- `Cancel(guest)`：读条中止恢复耐心
- `Finish(guest, src)`：恢复耐心 + `ChooseOrder` 选菜入队转等餐回满
- 读条计时/锁/进度仍在表现层 `OrderMeterFlow`；服务不碰 Godot。

**要点**：应用服务要有真实用例语义（含前置校验、注入基础设施依赖、失败处理），不能做成对聚合方法的纯转发——那就成了"为了分层而分层"。

## 11. 演进方向（待办，非本期）

对标 Loxodon Framework 分层架构（View / Service / Domain Model / Infrastructure + Context 服务容器 DI）可渐进落地：

1. **组合根 + 接口字段注入**替代全局静态可达（`IGuestRepository`/`IRuntimeConfig`），仍是雏形 DI，不动用重量级容器。
2. 按**生命周期归类单例**：全局（`ConfigSystem`）vs 跨场景会话（`DayCycleSystem`），提升单例可解释性。
3. 仓储抽接口 + 可替换实现（内存 → 存档）。
4. 值对象收敛成串裸参数（如口味封装 `GuestTaste`），让聚合/命令自处理而非静态算。

**不必照搬**：完整 MVVM 数据绑定（BindingSet/TwoWay/类型转换器）适合足量 UI 绑定场景；本项目用"领域事件 → 表现订阅刷新"已等价，引入为过度设计。

## 12. 通用 OOP 原则 → 本项目落法（速查）

**项目断言（红线）**：脚本与场景（`.cs`/`.tscn`）作为引擎类的扩展，必须遵守**全部** OOP 原则；禁止写成过程式直接操作场景树、绕过分层拿真值。以下为各原则在本项目的落地方式（不列教科书定义，改代码前对照）：

| 原则 | 本项目落法 / 反例 |
|------|------|
| 单一职责 | 每个类一件事：System 管真值/生命周期、`*Core`/`*Service` 管逻辑、表现壳只订阅落地（见 §1/§2） |
| 开闭原则 | 用配表驱动与扩展钩子扩展，不改内部：`ItemPriceText`/`ItemShelfLifeText`、`OrderingService` 门卫谓词 |
| LSP 里氏替换 | 派生态无缝替换基类：`StorageForm` 只覆写 `ProvideSections`/`ItemShelfLifeText`，不破坏 `InventoryGroupFormBase` 契约 |
| 接口隔离 | 按需注入小接口/谓词（`IGuestFoodSource`、`CurrentDayProvider`），不塞大而全接口 |
| 依赖倒置 | 注入 Provider/谓词/接口替代全局静态直取（DI 组合根方向见 §11） |
| DRY 不重复 | 操作归口唯一入口：`PurgeStorageExpired`、金币 `Earn`/`TrySpend`/`SetGold`，不散落重复实现 |
| KISS 极简 | 能直调不引入事件/服务层；最小改动满足需求（见 §4） |
| YAGNI | 不为假设的未来加抽象：不做自动放置/自动排序，专项表不塞进物品表 |

## 13. 与本仓库其它文档的关系

- 框架/引擎能力见 `../` 各 `XxxSystem.md` 与 `../engine-reference/`（GGF 框架侧）。
- 单玩法功能与红线见 `../features/`（记每玩法需求/红线/配表/踩坑）。
- 单元测试写法/造假数据见本目录下的 [TestingSystem.md](TestingSystem.md)。
- 本文档记**跨玩法的架构取向与设计决策**，改架构前先读。