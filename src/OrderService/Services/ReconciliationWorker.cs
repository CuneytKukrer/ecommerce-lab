using OrderService.Data;
using OrderService.Models;
using OrderService.Services;
using Microsoft.EntityFrameworkCore;

public class ReconciliationWorker : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<ReconciliationWorker> _logger;

    public ReconciliationWorker(IServiceProvider sp, ILogger<ReconciliationWorker> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var paymentClient = scope.ServiceProvider.GetRequiredService<PaymentClient>();
            var inventory = scope.ServiceProvider.GetRequiredService<InventoryClient>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ReconciliationWorker>>();
            
            var pending = await db.Orders
                .Where(o => o.Status == OrderStatus.ReconciliationRequired)
                .Take(50)
                .ToListAsync(ct);

            foreach (var order in pending)
            {
                // Payment Service'e sor: bu order için ödeme var mı?
                var result = await paymentClient.ChargeAsync(
                    order.Id, order.TotalAmount, order.Id.ToString());

                if (result.Outcome == PaymentOutcome.Success)
                {
                    order.Status = OrderStatus.Confirmed;
                    logger.LogInformation("Reconciled order {OrderId} → Confirmed", order.Id);
                }
                else if (result.Outcome == PaymentOutcome.Failed)
                {
                    // Compensation: rezervasyonları geri al
                    foreach (var item in order.Items)
                        await inventory.ReleaseAsync(item.ProductId, item.Quantity);

                    order.Status = OrderStatus.Failed;
                    logger.LogInformation("Reconciled order {OrderId} → Failed", order.Id);
                }
                // Unknown ise bir sonraki turda tekrar dener
            }

            await db.SaveChangesAsync(ct);
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}