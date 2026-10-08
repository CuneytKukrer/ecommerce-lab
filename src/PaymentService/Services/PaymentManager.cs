using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Models;

namespace PaymentService.Services;

public class PaymentManager
{
    private readonly PaymentDbContext _db;
    private readonly MockPaymentProvider _provider;
    private readonly ILogger<PaymentManager> _logger;

    public PaymentManager(
        PaymentDbContext db,
        MockPaymentProvider provider,
        ILogger<PaymentManager> logger)
    {
        _db = db;
        _provider = provider;
        _logger = logger;
    }

    public async Task<(Payment? payment, string? error)> ProcessAsync(
        Guid orderId, decimal amount, string idempotencyKey)
    {
        // 1. Daha önce bu key ile işlem yapılmış mı?
        var existing = await _db.Payments
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Idempotent hit: payment {PaymentId} already exists for key {Key}",
                existing.Id, idempotencyKey);
            return (existing, null); // İlk sonucu döndür, yeni işlem YAPMA
        }

        // 2. Yeni ödeme kaydı oluştur
        var payment = new Payment
        {
            OrderId = orderId,
            Amount = amount,
            IdempotencyKey = idempotencyKey,
            Status = PaymentStatus.Pending
        };

        _db.Payments.Add(payment);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Race condition: aynı key ile paralel iki istek geldi.
            // Unique index sayesinde buraya düşer.
            _logger.LogWarning(
                "Race condition detected for key {Key}. Returning existing payment.",
                idempotencyKey);

            var winner = await _db.Payments
                .FirstAsync(p => p.IdempotencyKey == idempotencyKey);
            return (winner, null);
        }

        // 3. Ödeme sağlayıcısına gönder
        var (success, txnId, failureReason) =
            await _provider.ChargeAsync(orderId, amount);

        // 4. Sonucu kaydet
        payment.Status = success ? PaymentStatus.Success : PaymentStatus.Failed;
        payment.ProviderTransactionId = txnId;
        payment.FailureReason = failureReason;
        payment.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Payment {PaymentId} for order {OrderId}: {Status}",
            payment.Id, orderId, payment.Status);

        return (payment, success ? null : failureReason);
    }

    public async Task<Payment?> GetAsync(Guid id)
        => await _db.Payments.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<List<Payment>> GetByOrderAsync(Guid orderId)
        => await _db.Payments.Where(p => p.OrderId == orderId).ToListAsync();
}