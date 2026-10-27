# DddTactics 完整方案（最终版）

## 一、定位

**一个零依赖的 DDD 战术层基础设施包，提供按事件类型独立配置的显式分发策略、分发副产品式的拓扑元数据生成、以及可替换的应用层验证管道。不绑定任何消息分发库，不绑定任何 UI 框架，不要求使用全部组件。**

它解决的是战术层“从想清楚到写对”之间的摩擦——尤其是那些编译器管不到、文档说不清、但生产事故会替你记住的地方。

## 二、竞品对标（最终校准）

| 维度               | ABP          | NetCorePal                                                    | Dddify    | DKNet                                                                                 | Hindstorm      | **DddTactics**                   |
| :--------------- | :----------- | :------------------------------------------------------------ | :-------- | :------------------------------------------------------------------------------------ | :------------- | :------------------------------- |
| 事件分发策略           | 隐式，UoW 结束时发布 | 领域事件：与 CommandHandler 同事务，同步执行；集成事件：CAP Outbox 异步，**处理器独立事务** | 有分发，时机未公开 | `DKNet.EfCore.Events` 固定 AfterSave（SaveChanges 成功后），**deliberately not configurable** | 不做分发           | **按事件类型独立配置**                    |
| 开发者能否改变分发时机      | 否            | 否（按类别固定）                                                      | 未公开       | **否**                                                                                 | —              | **是**                            |
| 领域事件处理器约束        | 无显式约束        | **必须幂等；只能发一个命令；不可操作领域模型**                                     | 未公开       | 未公开                                                                                   | —              | 除禁止 `SaveChanges` 外无约束           |
| 领域事件处理器**可以**做什么 | 无显式约束        | 可检索数据、调用外部服务、发布集成事件、发命令                                       | 未公开       | 未公开                                                                                   | —              | 除禁止 `SaveChanges` 外无限制           |
| 拓扑识别条件           | 无            | 继承 `Entity<TId>` + `IAggregateRoot`                           | 无         | 无拓扑                                                                                   | 手动标注 Attribute | 仅需 `IHasDomainEvents`            |
| 拓扑来源             | —            | 源生成器自动，全链路                                                    | —         | 无                                                                                     | 反射读取标注         | **分发副产品（结构零标注，策略 Attribute 标注）** |
| 源生成器可回退          | 不涉及          | 不可禁用                                                          | 不涉及       | 不涉及                                                                                   | 不涉及            | **显式 MSBuild 回退**                |

**核心差异**：

*   **vs NetCorePal**：其分发时机由框架按事件类别固定分配。领域事件与 CommandHandler 同事务、同步执行，处理器被官方约束为“必须幂等、只能发一个命令、不可操作领域模型”；集成事件通过 CAP Outbox 异步投递，处理器在独立事务中执行，异常由 CAP 重试，不回滚命令。开发者无法将某个领域事件改为 AfterSave，也无法将某个集成事件改为 BeforeSave。DddTactics 允许对每个事件类型独立选择。[-1](https://netcorepal.github.io/netcorepal-cloud-framework/en/events/domain-event-handler/#domain-event-handling)[-14](https://netcorepal.github.io/netcorepal-cloud-framework/zh/transactions/transactions/#_2)
*   **vs DKNet**：其 `DKNet.EfCore.Events` 固定 AfterSave，文档明确写出分发时机“deliberately not configurable”，且“no retry, ordering knob, or dead-letter path”。DddTactics 允许按事件类型选择 BeforeSave 或 AfterSave。[-2](https://socket.dev/nuget/package/dknet.efcore.events/overview/10.1.23#1)
*   **vs Hindstorm**：其拓扑依赖手动标注 Attribute，明确选择“不提供基类型，只读标签”。DddTactics 的拓扑结构来自分发器使用，无需为拓扑结构额外标注；分发策略通过 `[DispatchMode]` 显式标注。

## 三、交付物清单

| 包                          | 内容                                                                                           | 依赖               |
| :------------------------- | :------------------------------------------------------------------------------------------- | :--------------- |
| **DddTactics.Core**        | `Entity<TId>`、`AggregateRoot<TId>`、`ValueObject`、强类型 ID 范式、`IDomainEvent`、`IHasDomainEvents` | 无                |
| **DddTactics.EFCore**      | `SaveChanges` 拦截器（同步+异步）、按事件类型独立配置的 BeforeSave/AfterSave 分发器、`RepositoryBase`、最小规格模式         | EF Core 10       |
| **DddTactics.Topology**    | 源生成器（输出拓扑结构 Attribute）、`[DispatchMode]` 标注合并、Mermaid（带策略标注，ELK/Dagre）、孤儿事件告警                 | 仅依赖 Core         |
| **DddTactics.AppServices** | `AppService` 基类、验证管道（MediatR / Wolverine / 原生）                                               | FluentValidation |
| **DddTactics.Analyzers**   | 3 条 Roslyn 规则                                                                                | Roslyn SDK       |
| **DddTactics.Templates**   | `dotnet new ddd-sln` 模板                                                                      | 引用上述包            |

## 四、核心设计决策

### 4.1 强类型 ID

`readonly record struct`，内置 `New()` / `From()`。`AggregateRoot<TId>` 约束 `TId : struct` 引导使用，不强制。

### 4.2 事件分发策略（按事件类型独立配置，无默认值）

`DomainEventDispatchMode` 枚举：`BeforeSave` / `AfterSave`。**配置粒度是事件类型**。

*   **BeforeSave**：处理器与聚合变更同事务。处理器中禁止调用 `SaveChanges`，分发器通过 `AsyncLocal<bool>` 检测并抛出异常。第 2 周验证多线程传播行为。
*   **AfterSave**：`SavingChanges` 中提取事件，存入 `ConditionalWeakTable`；`SavedChanges` 中分发；`SaveChangesFailed` 中丢弃。

### 4.3 拓扑元数据生成器（结构零标注，策略 Attribute 标注）

**设计选择**：拓扑图需要展示“哪个处理器走 BeforeSave、哪个走 AfterSave”。这个信息在编译期只能通过处理器类上的标记获得。Hindstorm 的 NuGet 文档提供了完整的论证：“Edges are data. Reflection can’t see inside a method body, so it can’t infer that an aggregate raises an event from the `new` statement. Instead the edge is stated as an attribute argument baked into assembly metadata”。DddTactics 的 `[DispatchMode]` 遵循同一逻辑：运行时分发策略配置在编译期不可见，因此需要显式标注。拓扑结构（聚合-事件-处理器关系）由源生成器自动产出，**零额外标注**。[-3](https://feed.nuget.org/packages/Hindstorm/0.3.0#1)[-9](https://socket.dev/nuget/package/hindstorm/overview/0.3.0#1)

**孤儿事件告警触发条件**：如果某个 `IDomainEvent` 类型被至少一个 `AggregateRoot` 发出，但没有注册任何 `IDomainEventHandler<T>` 实现（或实现未标记 `[DispatchMode]`），则告警。告警信息包含事件类型全名和发出它的聚合类型。

Mermaid 默认 ELK 布局，提供 Dagre 回退。输出：Mermaid（带策略标注）、文本树、孤儿事件告警。

### 4.4 应用层验证管道

`AppService` 基类只提供验证管道和执行模板方法。三个独立实现：MediatR `IPipelineBehavior`、Wolverine middleware、原生 `IAppServiceValidator`。

### 4.5 Analyzer 规则

| 规则        | 内容                        | 级别 | 限制    |
| :-------- | :------------------------ | :- | :---- |
| `TACT001` | 聚合根属性中直接引用另一聚合根           | 警告 | 仅同程序集 |
| `TACT002` | 领域事件类名不以过去式结尾             | 信息 | 启发式   |
| `TACT003` | `ValueObject` 子类含可写 `set` | 警告 | —     |

### 4.6 退出机制

| 层级     | 替换方式                         |
| :----- | :--------------------------- |
| 事件分发   | 自定义 `IDomainEventDispatcher` |
| 仓储     | 不继承 `RepositoryBase`         |
| DI 注册  | MSBuild 禁用生成器 + 手动扩展方法       |
| 拓扑生成   | 不用分发器时手动标注；或完全禁用生成器          |
| 强类型 ID | 裸 `Guid`/`int` 也可编译          |

## 五、解决的痛点

**痛点一：分发时机由框架替开发者决定。** NetCorePal 的领域事件与集成事件分配由框架按类别固定，DKNet 固定 AfterSave 且文档明确“deliberately not configurable”。DddTactics 强制开发者对每个事件类型显式选择。[-2](https://socket.dev/nuget/package/dknet.efcore.events/overview/10.1.23#1)

**痛点二：跨聚合引用的编译器盲区。** 强类型 ID 消除 ID 类型混淆。Analyzer TACT001 在 IDE 层面拦截聚合根直接引用。

**痛点三：搭结构的重复劳动与事件流向的不可见。** `dotnet new` 模板 + 三档渐进示例。拓扑生成器作为分发副产品自动产出结构，边标注分发策略，孤儿事件告警提示缺失处理器。

**痛点四：被重框架锁死的恐惧。** 每个组件可替换。NetCorePal 的生成器不可禁用，DKNet 的分发时机不可配置，DddTactics 提供显式回退路径。

## 六、设计原则

**原则一：战术层内摩擦最小，战术层外明确拒绝。**

**原则二：单元测试友好是默认属性。** Core 零依赖。

**原则三：AI 协同通过可读约束实现。**

## 七、最适配场景（五个核心场景 + 一个技术备注）

### 场景一：同一事件需要双路径处理（核心差异化场景）

**业务背景**：会员系统的 `OrderShipped` 事件触发两个副作用——更新会员聚合的“最近发货订单”字段（需要与发货操作同事务）和通知外部物流系统（需要事务提交后异步执行）。

**NetCorePal 的写法**：领域事件处理器与 CommandHandler 同事务、同步执行。根据官方文档，处理器可以检索数据、调用外部服务、发布集成事件，但如果需要修改会员聚合的状态，必须发出命令（且只能发一个命令）。外部通知需要通过集成事件转换器走 CAP Outbox 链路，集成事件处理器在独立事务中执行。NetCorePal 将两类操作分配给两种机制，并施加“必须幂等、只能发一个命令”的约束。这是有意的分层设计。[-1](https://netcorepal.github.io/netcorepal-cloud-framework/en/events/domain-event-handler/#domain-event-handling)[-14](https://netcorepal.github.io/netcorepal-cloud-framework/zh/transactions/transactions/#_2)

**DddTactics 的写法**：`OrderShipped` 注册为 BeforeSave（处理器直接查询会员聚合、修改字段），同时注册 AfterSave 处理器（发布外部通知）。DddTactics 将两类操作放在同一个事件的两个处理器中，不施加幂等或命令数量约束。

**选择取决于团队偏好**：如果团队接受“处理器职责严格受限、必须幂等”的约束，NetCorePal 的设计更一致。如果团队希望副作用代码集中、不介意自行维护处理器职责边界，DddTactics 更直接。

### 场景二：领域事件需要 AfterSave（NetCorePal 和 DKNet 均无法表达）

**业务背景**：审计日志聚合需要记录“任何聚合的变更”，但审计写入不应阻塞主业务事务。

**NetCorePal 的困境**：审计事件如果定义为领域事件，必须在主事务中同步写入；如果定义为集成事件，需要走 CAP Outbox，对于“同一进程内的审计日志”过重。DKNet 固定 AfterSave，但无法在同一事件上混合 BeforeSave 和 AfterSave。

**DddTactics 的解法**：将审计事件注册为 AfterSave，事务提交后同步执行审计写入。失败不影响主事务，不需要 CAP 基础设施。

### 场景三：多租户系统中不同租户需要不同分发策略

**业务背景**：SaaS 平台中，免费租户的积分发放可接受最终一致性（AfterSave），付费租户必须与支付同事务（BeforeSave）。

**NetCorePal 的困境**：分发策略由框架按事件类别固定分配，无法根据运行时上下文动态切换。DKNet 固定 AfterSave，同样无法切换。

**DddTactics 的解法**：`DomainEventDispatchMode` 配置可基于运行时条件（如租户 ID）动态解析，分发器支持 `IDispatchModeResolver`。

### 场景四：渐进式迁移——从 BeforeSave 到 AfterSave

**业务背景**：已有系统最初所有事件都用 BeforeSave，但随着业务增长，某些处理器开始调用外部 API，导致事务持有时间过长。团队希望逐步迁移。

**NetCorePal 的困境**：分发时机由框架固定，迁移意味着改变事件的类别定义。DKNet 固定 AfterSave，无法从 BeforeSave 迁移（因为起点就不支持 BeforeSave）。

**DddTactics 的解法**：逐个事件类型修改 `DomainEventDispatchMode` 配置即可。BeforeSave 和 AfterSave 处理器可共存于同一个事件。

### 场景五：纯领域层的不变量验证（最小示例）

**业务背景**：`LoyaltyProgram` 聚合发布后不可编辑，`Order` 状态机不可回退。这些规则来自 `ddd-by-example` 仓库，该仓库明确使用 dispatch-before-save 和事务性 Outbox。[-7](https://github.com/richardchanjr90-cpu/ddd-by-example#1)

**DddTactics 的解法**：`Sample.Minimal` 展示这些不变量在 `AggregateRoot<TId>` 基类上的实现，配合强类型 ID 和拓扑自动产出。5 分钟内看到完整闭环，不需要启动数据库或消息队列。

### 技术备注：集成事件需要 BeforeSave（不推荐，但支持）

DddTactics 允许将集成事件配置为 BeforeSave，用于“消息发布必须与数据库事务严格原子”的特殊场景。**不推荐绕过 Outbox 模式**；消息队列的事务性发布与数据库事务的原子性协调需开发者自行实现。

## 八、示例仓库

| 示例                     | 覆盖的亮点验证                                                                                                        |
| :--------------------- | :------------------------------------------------------------------------------------------------------------- |
| **Sample.Minimal**     | `LoyaltyProgram` 发布后不可编辑 + 强类型 ID + 拓扑结构自动产出（ELK 布局）                                                           |
| **Sample.Realistic**   | `OrderShipped` 双处理器混合策略（场景一）+ BeforeSave 回滚验证 + **拓扑边标注分发策略** + 孤儿事件告警 + TACT001 触发 + 最小 Outbox + **两套完整代码对比** |
| **Sample.Alternative** | AfterSave 不阻塞验证 + 源生成器回退（MSBuild 切换）+ 不用基类时的手动拓扑标注                                                             |

每个示例附 `AGENTS.md`：聚合根无 public setter、跨聚合用 ID、事件过去式命名、BeforeSave 处理器禁止 SaveChanges。

## 九、实施顺序

| 周  | 交付                                                                     | 验证标准                                                                                                                                                                                                                                                                                                                                                                                               |
| :- | :--------------------------------------------------------------------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1  | DddTactics.Core                                                        | 领域层独立编译，`LoyaltyProgram` 发布后不可编辑的单元测试通过                                                                                                                                                                                                                                                                                                                                                            |
| 2  | DddTactics.EFCore + 按事件类型独立配置的两种策略                                     | **`[Theory]` 覆盖四种组合**：`BeforeSave_RollsBack_WhenHandlerThrows`（断言 `Order.Paid` 未持久化）、`BeforeSave_Commits_WhenHandlerSucceeds`、`AfterSave_DoesNotRollBack_WhenHandlerThrows`（断言 `Order.Shipped` 已持久化）、`AfterSave_Commits_WhenHandlerSucceeds`。**独立 `[Fact]`**：`SameEvent_TwoHandlers_DifferentStrategies`——`OrderShipped` 注册 BeforeSave（更新会员聚合）和 AfterSave（发布通知）各一个，断言两者互不阻塞。`AsyncLocal` 多线程行为一并验证 |
| 3  | DddTactics.Topology + Mermaid ELK/Dagre + `[DispatchMode]` 标注 + 孤儿事件告警 | Sample.Minimal 自动产出拓扑结构（ELK 默认），场景一拓扑边正确标注 BeforeSave/AfterSave，移除处理器后告警正确显示事件类型和发出聚合。**运行时合并作为可选增强，不在本轮验证范围**                                                                                                                                                                                                                                                                                     |
| 4  | DddTactics.AppServices 三版本                                             | MediatR 和 Wolverine 各有一个可运行验证                                                                                                                                                                                                                                                                                                                                                                      |
| 5  | DddTactics.Analyzers + Templates                                       | `dotnet new ddd-sln` 可编译、可测试、可输出拓扑、Analyzer 触发                                                                                                                                                                                                                                                                                                                                                     |
| 6  | Sample.Realistic + Sample.Alternative + 文档                             | 覆盖五个场景、技术备注、分发策略迁移、Analyzer 限制、拓扑用法                                                                                                                                                                                                                                                                                                                                                                |

## 十、明确不做的范围

*   不内置 UnitOfWork
*   不封装查询侧
*   不绑定消息分发库
*   不负责战略设计
*   不承诺 AfterSave 的 Exactly-Once 语义
*   不试图“适应任何场景”
*   不与 NetCorePal 在“可视化深度”上竞争（其全链路覆盖 + 快照历史是既有优势）
*   不与 Hindstorm 在“独立拓扑工具”上竞争
*   **不与 DKNet 在包覆盖广度上竞争**——DKNet 的 28 个包覆盖企业应用全栈，DddTactics 的 6 个包聚焦战术层分发与拓扑
*   **不推荐将集成事件配置为 BeforeSave**（仅作为技术备注中的特殊能力）
*   **不将“处理器无约束”作为核心卖点**
*   **不强制处理器幂等**——NetCorePal 要求处理器必须幂等，DddTactics 不强制，开发者需自行保证[-1](https://netcorepal.github.io/netcorepal-cloud-framework/en/events/domain-event-handler/#domain-event-handling)
*   **不限制处理器发出命令的数量**——NetCorePal 限制单个处理器只能发一个命令，DddTactics 不限制[-1](https://netcorepal.github.io/netcorepal-cloud-framework/en/events/domain-event-handler/#domain-event-handling)

## 十一、最适配场景（一句话概括）

**一个 .NET 10 的模块化单体系统，团队需要领域事件协调跨聚合操作，且不同事件（甚至同一事件的不同处理器）对事务一致性的要求不同。** NetCorePal 的按类别固定分配和 DKNet 的固定 AfterSave 都无法表达“同一事件双路径”或“领域事件需要 AfterSave”。DddTactics 让团队对每个事件类型独立选择分发策略，拓扑结构作为分发副产品自动产出，随时可替换任意组件。

## 十二、一句话定位

**“一个让你在不绑定任何框架的前提下，对每个事件类型独立选择分发策略、让拓扑作为分发副产品自动产生的 DDD 战术层工具包。它解决的是战术层‘从想清楚到写对’之间的摩擦。”**
