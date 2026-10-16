using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace OrderService.Messaging;

public class OrderEventPublisher : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<OrderEventPublisher> _logger;
    private const string ExchangeName = "order.events";

    private OrderEventPublisher(IConnection connection, IChannel channel, ILogger<OrderEventPublisher> logger)
    {
        _connection = connection;
        _channel = channel;
        _logger = logger;
    }

    public static async Task<OrderEventPublisher> CreateAsync(IConfiguration config, ILogger<OrderEventPublisher> logger)
    {
        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"] ?? "localhost",
            UserName = config["RabbitMQ:User"] ?? "dev",
            Password = config["RabbitMQ:Password"] ?? "dev"
        };

        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Fanout, durable: true);

        return new OrderEventPublisher(connection, channel, logger);
    }

    public async Task PublishOrderEventAsync(Guid orderId, string userId, string status, decimal amount)
    {
        var evt = new
        {
            OrderId = orderId,
            UserId = userId,
            Status = status,
            Amount = amount,
            OccurredAt = DateTime.UtcNow
        };

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));
        var props = new BasicProperties
        {
            Persistent = true
        };

        await _channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: "",
            mandatory: false,
            basicProperties: props,
            body: body
        );

        _logger.LogInformation(
            "Published order event: {OrderId} status={Status}", orderId, status);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}