using System.Net;

namespace OrderService.Services;

public class PaymentClient
{
    private readonly HttpClient _http;

    public PaymentClient(HttpClient http) => _http = http;

    public async Task<PaymentResult> ChargeAsync(
    Guid orderId, decimal amount, string idempotencyKey)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/payments")
            {
                Content = JsonContent.Create(new { orderId, amount })
            };
            request.Headers.Add("Idempotency-Key", idempotencyKey);

            var resp = await _http.SendAsync(request);

            if (resp.StatusCode == HttpStatusCode.RequestTimeout
                || resp.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                return new PaymentResult(PaymentOutcome.Unknown, null, "Timeout");
            }

            if (!resp.IsSuccessStatusCode)
                return new PaymentResult(PaymentOutcome.Failed, null, "Payment error");

            var payment = await resp.Content.ReadFromJsonAsync<PaymentDto>();
            return payment?.Status switch
            {
                "Success" => new PaymentResult(PaymentOutcome.Success, payment.Id, null),
                "Failed" => new PaymentResult(PaymentOutcome.Failed, payment.Id, payment.FailureReason),
                _ => new PaymentResult(PaymentOutcome.Unknown, payment?.Id, null)
            };
        }
        catch (TaskCanceledException) // Timeout
        {
            return new PaymentResult(PaymentOutcome.Unknown, null, "Client timeout");
        }
        catch (HttpRequestException) // Ağ hatası
        {
            return new PaymentResult(PaymentOutcome.Unknown, null, "Network error");
        }
    }

    public async Task<PaymentResult> GetPaymentStatusAsync(
    Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"/payments/order/{orderId}/latest", ct);

            if (resp.StatusCode == HttpStatusCode.NotFound)
                return new PaymentResult(PaymentOutcome.Unknown, null, "No payment found");

            var payment = await resp.Content.ReadFromJsonAsync<PaymentDto>(ct);
            return payment?.Status switch
            {
                "Success" => new PaymentResult(PaymentOutcome.Success, payment.Id, null),
                "Failed" => new PaymentResult(PaymentOutcome.Failed, payment.Id, payment.FailureReason),
                _ => new PaymentResult(PaymentOutcome.Unknown, payment?.Id, null)
            };
        }
        catch (Exception ex)
        {
            return new PaymentResult(PaymentOutcome.Unknown, null, ex.Message);
        }
    }
}

public enum PaymentOutcome
{
    Success,      // Ödeme başarılı
    Failed,       // Ödeme başarısız (kesin)
    Unknown       // Timeout/ağ hatası — belirsiz
}

public record PaymentResult(PaymentOutcome Outcome, Guid? PaymentId, string? FailureReason);
public record PaymentDto(Guid Id, string Status, string? FailureReason);