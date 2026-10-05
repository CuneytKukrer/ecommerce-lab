using InventoryService.Data;
using InventoryService.Endpoints;
using InventoryService.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<InventoryDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<InventoryManager>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-migrate on startup (dev only)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    db.Database.EnsureCreated();

    if (!db.Inventory.Any())
    {
        db.Inventory.AddRange(
            new InventoryService.Models.InventoryItem { ProductId = 101, Available = 100, Reserved = 0, Version = 0 },
            new InventoryService.Models.InventoryItem { ProductId = 102, Available = 50, Reserved = 0, Version = 0 },
            new InventoryService.Models.InventoryItem { ProductId = 103, Available = 10, Reserved = 0, Version = 0 }
        );
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    //app.UseSwaggerUI();
}

app.MapInventoryEndpoints();
app.Run();