using InventoryService.Services;

namespace InventoryService.Endpoints;

public record ReserveRequest(int ProductId, int Quantity);

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/inventory");

        group.MapGet("/{productId:int}", async (int productId, InventoryManager mgr) =>
        {
            var item = await mgr.GetAsync(productId);
            return item is null ? Results.NotFound() : Results.Ok(item);
        });

        group.MapPost("/reserve", async (ReserveRequest req, InventoryManager mgr) =>
        {
            var ok = await mgr.ReserveAsync(req.ProductId, req.Quantity);
            return ok ? Results.Ok() : Results.BadRequest("Insufficient stock");
        });

        group.MapPost("/release", async (ReserveRequest req, InventoryManager mgr) =>
        {
            var ok = await mgr.ReleaseAsync(req.ProductId, req.Quantity);
            return ok ? Results.Ok() : Results.BadRequest("Invalid release");
        });
    }
}