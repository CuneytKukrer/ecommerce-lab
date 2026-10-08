using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;

namespace OrderService.Services;

public class OrderManager
{
    private readonly OrderDbContext _db;
    private readonly CartClient _cart;
    private readonly InventoryClient _inventory;
    private readonly ILogger<OrderManager> _logger;

    public OrderManager(
        OrderDbContext db,
        CartClient cart,
        InventoryClient inventory,
        ILogger<OrderManager> logger)
    {
        _db = db;
        _cart = cart;
        _inventory = inventory;
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
                PriceAtPurchase = 0m // TODO: Product Service'ten al
            }).ToList(),
            Status = OrderStatus.Pending
        };

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

                _logger.LogWarning(
                    "Checkout failed for user {UserId}: insufficient stock for {ProductId}",
                    userId, item.ProductId);

                return (null, $"Insufficient stock for product {item.ProductId}");
            }
            reserved.Add((item.ProductId, item.Quantity));
        }

        // 4. Siparişi kaydet
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Order {OrderId} created for user {UserId} with {ItemCount} items",
            order.Id, userId, order.Items.Count);

        return (order, null);
    }
}