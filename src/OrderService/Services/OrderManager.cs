using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;

namespace OrderService.Services;

public class OrderManager
{
    private readonly OrderDbContext _db;
    private readonly CartClient _cart;
    private readonly InventoryClient _inventory;
    private readonly PaymentClient _payment;
    private readonly ILogger<OrderManager> _logger;

    public OrderManager(
        OrderDbContext db,
        CartClient cart,
        InventoryClient inventory,
        PaymentClient payment,
        ILogger<OrderManager> logger)
    {
        _db = db;
        _cart = cart;
        _inventory = inventory;
        _payment = payment;
        _logger = logger;
    }

    public async Task<Order?> GetAsync(Guid id)
        => await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);

    public async Task<List<Order>> GetByUserAsync(string userId)
        => await _db.Orders.Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    public async Task<(Order? order, string? error)> CheckoutAsync(string userId)
    {
        // 1. Sepeti al
        var cart = await _cart.GetCartAsync(userId);
        if (cart is null || cart.Items.Count == 0)
            return (null, "Cart is empty or not found");

        // 2. Sipariş oluştur (PENDING)
        var order = new Order
        {
            UserId = userId,
            Items = cart.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                PriceAtPurchase = 0m // TODO: Product Service
            }).ToList(),
            Status = OrderStatus.Pending
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} created (PENDING)", order.Id);

        // 3. Stok rezervasyonu (compensation ile)
        var reserved = new List<(int productId, int quantity)>();
        foreach (var item in cart.Items)
        {
            var ok = await _inventory.ReserveAsync(item.ProductId, item.Quantity);
            if (!ok)
            {
                // Compensation: öncekileri geri al
                foreach (var (pid, qty) in reserved)
                    await _inventory.ReleaseAsync(pid, qty);

                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                _logger.LogWarning(
                    "Checkout failed for order {OrderId}: insufficient stock for product {ProductId}",
                    order.Id, item.ProductId);

                return (null, $"Insufficient stock for product {item.ProductId}");
            }
            reserved.Add((item.ProductId, item.Quantity));
        }

        // 4. Ödeme isteği gönder (Idempotency-Key = orderId)
        var totalAmount = 0m; // TODO: fiyat entegrasyonu
        var paymentResult = await _payment.ChargeAsync(order.Id, totalAmount, order.Id.ToString());

        if (paymentResult.Outcome == PaymentOutcome.Unknown)
        {
            order.Status = OrderStatus.ReconciliationRequired;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogWarning(
                "Order {OrderId} requires reconciliation: payment outcome unknown",
                order.Id);

            return (order, "Payment outcome unknown — reconciliation required");
        }

        if (paymentResult.Outcome != PaymentOutcome.Success)
        {
            // Compensation: tüm rezervasyonları geri al
            foreach (var (pid, qty) in reserved)
                await _inventory.ReleaseAsync(pid, qty);

            order.Status = OrderStatus.Failed;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogWarning(
                "Payment failed for order {OrderId}: {Reason}",
                order.Id, paymentResult.FailureReason);

            return (order, paymentResult.FailureReason ?? "Payment failed");
        }

        // 5. Ödeme başarılı → CONFIRMED
        order.Status = OrderStatus.Confirmed;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} CONFIRMED", order.Id);

        return (order, null);
    }
}