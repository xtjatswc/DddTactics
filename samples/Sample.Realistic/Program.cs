using DddTactics.EFCore;
using DddTactics.Topology;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sample.Realistic;

var services = new ServiceCollection();
services.AddSingleton<NotificationLog>();
services.AddDbContext<AppDbContext>((sp, o) =>
    o.UseInMemoryDatabase("sample-realistic")
     .AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>()));
services.AddDddTacticsEfCore(ServiceLifetime.Scoped);
services.AddScoped<IDomainEventHandler<OrderShipped>, OrderShippedBeforeHandler>();
services.AddScoped<IDomainEventHandler<OrderShipped>, OrderShippedAfterHandler>();

await using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

Console.WriteLine("=== Sample.Realistic：同事件双策略 + 孤儿事件 ===\n");

var member = new Member(Guid.NewGuid());
db.Members.Add(member);
var order = new Order(Guid.NewGuid(), "ORD-1");
order.Ship();
db.Orders.Add(order);

await db.SaveChangesAsync();

var loaded = await db.Members.FirstAsync();
Console.WriteLine($"会员最近发货订单已更新: {loaded.LastShippedOrderId == order.Id}");
var log = scope.ServiceProvider.GetRequiredService<NotificationLog>();
Console.WriteLine($"发货通知已发送: {log.Notified.Contains(order.Id)}");

var report = new TopologyReporter().Build();
Console.WriteLine("\n--- Mermaid (ELK：边标注 BeforeSave/AfterSave) ---");
Console.WriteLine(report.ToMermaid(useElk: true));
Console.WriteLine("\n--- 文本树 ---");
Console.WriteLine(report.ToTextTree());

if (report.Orphans.Count > 0)
{
    Console.WriteLine("--- 孤儿事件告警 ---");
    foreach (var orphan in report.Orphans)
    {
        Console.WriteLine($"  ⚠ {orphan.Message}");
    }
}
