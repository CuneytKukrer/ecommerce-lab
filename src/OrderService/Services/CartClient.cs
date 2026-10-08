using System.Text.Json;

namespace OrderService.Services;

public class CartClient
{
    private readonly HttpClient _http;

    public CartClient(HttpClient http) => _http = http;

    public async Task<CartDto?> GetCartAsync(string userId)
    {
        var resp = await _http.GetAsync($"/cart/{userId}");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<CartDto>();
    }
}

public record CartDto(string UserId, List<CartItemDto> Items);
public record CartItemDto(int ProductId, int Quantity);