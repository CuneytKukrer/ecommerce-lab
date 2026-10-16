using NotificationService.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<OrderEventConsumer>();

var app = builder.Build();

app.MapGet("/", () => "Notification Service is running");

app.Run();