namespace OrderService.Services;

public class InventoryClient
{
    private readonly HttpClient _http;

    public InventoryClient(HttpClient http) => _http = http;

    public async Task<bool> ReserveAsync(int productId, int quantity)
    {
        var resp = await _http.PostAsJsonAsync("/inventory/reserve",
            new { productId, quantity });
        return resp.IsSuccessStatusCode;
    }

    public async Task ReleaseAsync(int productId, int quantity)
    {
        await _http.PostAsJsonAsync("/inventory/release",
            new { productId, quantity });
    }
}