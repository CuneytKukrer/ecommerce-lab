using InventoryService.Data;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Services;

public class InventoryManager
{
    private readonly InventoryDbContext _db;
    private readonly ILogger<InventoryManager> _logger;

    public InventoryManager(InventoryDbContext db, ILogger<InventoryManager> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<InventoryItem?> GetAsync(int productId)
        => await _db.Inventory.FirstOrDefaultAsync(x => x.ProductId == productId);

    public async Task<bool> ReserveAsync(int productId, int quantity)
    {
        // Pessimistic lock: SELECT ... FOR UPDATE
        var item = await _db.Inventory
            .FromSqlRaw("SELECT * FROM inventory WHERE product_id = {0} FOR UPDATE", productId)
            .FirstOrDefaultAsync();

        if (item is null) return false;
        if (item.Available < quantity) return false;

        item.Available -= quantity;
        item.Reserved += quantity;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Reserved {Qty} of product {Pid}. Available: {Avail}, Reserved: {Res}",
            quantity, productId, item.Available, item.Reserved);

        return true;
    }

    public async Task<bool> ReleaseAsync(int productId, int quantity)
    {
        var item = await _db.Inventory
            .FromSqlRaw("SELECT * FROM inventory WHERE product_id = {0} FOR UPDATE", productId)
            .FirstOrDefaultAsync();

        if (item is null) return false;
        if (item.Reserved < quantity) return false;

        item.Reserved -= quantity;
        item.Available += quantity;
        await _db.SaveChangesAsync();

        return true;
    }
}