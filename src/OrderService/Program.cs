using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Endpoints;
using OrderService.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrderDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddHttpClient<CartClient>(c =>
    c.BaseAddress = new Uri("http://localhost:5001")); // Cart Service portu

builder.Services.AddHttpClient<InventoryClient>(c =>
    c.BaseAddress = new Uri("http://localhost:5002")); // Inventory Service portu

builder.Services.AddScoped<OrderManager>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
//builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    //app.UseSwagger();
    //app.UseSwaggerUI();
}

app.MapOrderEndpoints();
app.Run();