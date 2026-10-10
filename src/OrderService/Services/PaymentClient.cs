namespace OrderService.Services;

public class PaymentClient
{
    private readonly HttpClient _http;

    public PaymentClient(HttpClient http) => _http = http;

    public async Task<PaymentResult> ChargeAsync(
        Guid orderId, decimal amount, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/payments")
        {
            Content = JsonContent.Create(new { orderId, amount })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        var resp = await _http.SendAsync(request);

        if (!resp.IsSuccessStatusCode)
            return new PaymentResult(false, null, "Payment service error");

        var payment = await resp.Content.ReadFromJsonAsync<PaymentDto>();
        return payment is null
            ? new PaymentResult(false, null, "Empty response")
            : new PaymentResult(
                payment.Status == "Success",
                payment.Id,
                payment.FailureReason);
    }
}

public record PaymentResult(bool Success, Guid? PaymentId, string? FailureReason);
public record PaymentDto(Guid Id, string Status, string? FailureReason);