namespace PaymentService.Services;

public class MockPaymentProvider
{
    private readonly Random _random = new();

    // %80 başarılı, %20 başarısız
    public Task<(bool success, string? transactionId, string? failureReason)> ChargeAsync(
        Guid orderId, decimal amount)
    {
        var roll = _random.Next(100);
        if (roll < 80)
        {
            return Task.FromResult<(bool, string?, string?)>(
                (true, $"txn_{Guid.NewGuid():N}", null));
        }
        return Task.FromResult<(bool, string?, string?)>(
            (false, null, "Insufficient funds"));
    }
}