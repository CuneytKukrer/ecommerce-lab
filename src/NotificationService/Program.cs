using NotificationService.Workers;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<OrderEventConsumer>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("NotificationService")) // servis adı değişecek
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317")));

var app = builder.Build();

app.MapGet("/", () => "Notification Service is running");

app.Run();