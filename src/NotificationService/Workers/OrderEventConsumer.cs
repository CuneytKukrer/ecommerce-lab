using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Workers;

public class OrderEventConsumer : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly ILogger<OrderEventConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public OrderEventConsumer(IConfiguration config, ILogger<OrderEventConsumer> logger)
    {
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:Host"] ?? "localhost",
            UserName = _config["RabbitMQ:User"] ?? "dev",
            Password = _config["RabbitMQ:Password"] ?? "dev"
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        await _channel.ExchangeDeclareAsync(
            exchange: "order.events",
            type: ExchangeType.Fanout,
            durable: true,
            cancellationToken: ct);

        var queueResult = await _channel.QueueDeclareAsync(
            queue: "notification.order.queue",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        var queueName = queueResult.QueueName;

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: "order.events",
            routingKey: "",
            cancellationToken: ct);

        _logger.LogInformation(
            "Notification consumer started, listening on queue: {Queue}", queueName);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var evt = JsonSerializer.Deserialize<OrderEvent>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (evt is null) return;

                _logger.LogInformation(
                    "[NOTIFICATION] Order {OrderId} for user {UserId} → {Status} (amount: {Amount})",
                    evt.OrderId, evt.UserId, evt.Status, evt.Amount);

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process order event");
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        // CancellationToken iptal edilene kadar bekle
        await Task.Delay(Timeout.Infinite, ct);
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        await base.StopAsync(ct);
    }
}

public record OrderEvent(
    Guid OrderId, string UserId, string Status, decimal Amount, DateTime OccurredAt);