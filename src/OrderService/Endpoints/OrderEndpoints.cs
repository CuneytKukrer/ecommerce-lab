using OrderService.Services;

namespace OrderService.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/orders");

        group.MapGet("/{id:guid}", async (Guid id, OrderManager mgr) =>
        {
            var order = await mgr.GetAsync(id);
            return order is null ? Results.NotFound() : Results.Ok(order);
        });

        group.MapGet("/user/{userId}", async (string userId, OrderManager mgr) =>
            Results.Ok(await mgr.GetByUserAsync(userId)));

        group.MapPost("/checkout/{userId}", async (string userId, OrderManager mgr) =>
        {
            var (order, error) = await mgr.CheckoutAsync(userId);
            if (order is null) return Results.BadRequest(new { error });
            return Results.Created($"/orders/{order.Id}", order);
        });
    }
}