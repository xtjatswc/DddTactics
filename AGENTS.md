# DddTactics

本仓库实现《DddTactics 方案.md》：.NET 10 / EF Core 10 的 DDD 战术层基础设施。

## 包结构（src/）

| 包 | 职责 | 依赖 |
|----|------|------|
| `DddTactics.Core` | 零依赖核心：强类型 ID（`IStrongId<TSelf,TValue>` + `GuidId`）、`Entity<TId>`、`AggregateRoot<TId>`、`ValueObject`、`IDomainEvent`、`IHasDomainEvents`、`DomainEventDispatchMode`、`[DispatchMode]` | 无 |
| `DddTactics.EFCore` | `SaveChanges` 拦截器（同步+异步）、按事件类型独立配置的 BeforeSave/AfterSave 分发器、`RepositoryBase<TAggregate,TId>`、最小规格模式 | Core, EF Core |
| `DddTactics.Topology` (+ `.Generator`) | 源生成器（扫描 `IDomainEventHandler<T>` + 合并 `[DispatchMode]`）、运行时拓扑报告（Mermaid ELK/Dagre、文本树、孤儿事件告警） | Core |
| `DddTactics.AppServices` | `AppService` 基类 + 三套可替换验证管道（原生 / MediatR / Wolverine）+ FluentValidation 适配 | Core, FluentValidation, MediatR, WolverineFx |
| `DddTactics.Analyzers` | TACT001（聚合根引用另一聚合根）/ TACT002（事件名过去式）/ TACT003（ValueObject 含可写 set） | Roslyn |
| `DddTactics.Templates` | `dotnet new ddd-sln` 模板 | — |

## 关键设计决策（务必遵守，避免幻觉）

1. **分发时机无默认值**：处理器必须显式标注 `[DispatchMode(BeforeSave|AfterSave)]`，否则不会被分发（运行时跳过），且会被拓扑标记为"未声明时机"。
2. **BeforeSave 处理器禁止调用 `SaveChanges`**：拦截器在 `IsDispatchingBeforeSave` 期间检测到重入会抛 `InvalidOperationException`；处理器对主聚合的修改随主事务一并提交。
3. **AfterSave 处理器失败不回滚主事务**（至少一次语义，非 Exactly-Once）。
4. **拓扑识别只依赖 `IHasDomainEvents`**：聚合-事件-处理器关系由源生成器零标注自动产出；"策略"由 `[DispatchMode]` 显式标注。
5. **源生成器不沿 ProjectReference 传递**：消费方（含示例）必须直接引用 `DddTactics.Topology.Generator` 项目（作为 Analyzer）。发布为 NuGet 包时，生成器应进入 `analyzers` 目录。
6. **`AggregateRoot<TId>` 约束 `TId : struct`**：引导使用强类型 ID，但裸 `Guid`/`int` 也可编译。退出机制：可手动实现 `IHasDomainEvents` 而不继承基类（见 `Sample.Alternative`）。

## 构建与验证

```powershell
dotnet build DddTactics.sln -c Debug
dotnet test  DddTactics.sln -c Debug
dotnet run --project samples/Sample.Realistic/Sample.Realistic.csproj -c Debug
```

## 已知限制

- 孤儿事件告警为启发式：扫描入口程序集中所有 `IDomainEvent` 类型，若没有任何标注 `[DispatchMode]` 的处理器即告警（无法在编译期静态确定"被某聚合发出"）。
- Wolverine 验证中间件签名（`Func<Task> next` + 注入 `IServiceProvider`）为示意性，需按目标 Wolverine 版本的中间件约定核对。
- 动态运行时分发（场景三：按租户选择时机）通过自定义 `IDispatchModeResolver` 实现，未内置具体策略。
