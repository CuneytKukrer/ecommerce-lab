using PaymentService.Services;

namespace PaymentService.Endpoints;

public record ProcessPaymentRequest(Guid OrderId, decimal Amount);

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/payments");

        group.MapGet("/{id:guid}", async (Guid id, PaymentManager mgr) =>
        {
            var p = await mgr.GetAsync(id);
            return p is null ? Results.NotFound() : Results.Ok(p);
        });

        group.MapGet("/order/{orderId:guid}", async (Guid orderId, PaymentManager mgr) =>
            Results.Ok(await mgr.GetByOrderAsync(orderId)));

        group.MapGet("/order/{orderId:guid}/latest", async (Guid orderId, PaymentManager mgr) =>
        {
            var payments = await mgr.GetByOrderAsync(orderId);
            var latest = payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
            return latest is null ? Results.NotFound() : Results.Ok(latest);
        });

        group.MapPost("/", async (
            ProcessPaymentRequest req,
            HttpContext ctx,
            PaymentManager mgr) =>
        {
            // Idempotency-Key header'ı zorunlu
            if (!ctx.Request.Headers.TryGetValue("Idempotency-Key", out var key)
                || string.IsNullOrWhiteSpace(key))
            {
                return Results.BadRequest(new { error = "Idempotency-Key header is required" });
            }

            var (payment, error) = await mgr.ProcessAsync(
                req.OrderId, req.Amount, key.ToString()!);

            if (payment is null)
                return Results.BadRequest(new { error });

            if (payment.Status == Models.PaymentStatus.Failed)
                return Results.Ok(new { payment, error });

            return Results.Ok(payment);
        });
    }
}