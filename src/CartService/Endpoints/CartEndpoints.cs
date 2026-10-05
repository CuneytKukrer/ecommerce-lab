using CartService.Models;
using CartService.Services;

namespace CartService.Endpoints;

public static class CartEndpoints
{
    public static void MapCartEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/cart/{userId}");

        group.MapGet("/", async (string userId, CartService.Services.CartManager svc) =>
            Results.Ok(await svc.GetCartAsync(userId)));

        group.MapPost("/items", async (string userId, CartItem item, CartService.Services.CartManager svc) =>
        {
            var cart = await svc.AddItemAsync(userId, item);
            return Results.Ok(cart);
        });

        group.MapPut("/items/{productId:int}", async (string userId, int productId, int quantity, CartService.Services.CartManager svc) =>
        {
            var cart = await svc.UpdateItemAsync(userId, productId, quantity);
            return Results.Ok(cart);
        });

        group.MapDelete("/items/{productId:int}", async (string userId, int productId, CartService.Services.CartManager svc) =>
        {
            var cart = await svc.RemoveItemAsync(userId, productId);
            return Results.Ok(cart);
        });
    }
}