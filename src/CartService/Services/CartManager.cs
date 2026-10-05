using CartService.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace CartService.Services
{
    public class CartManager
    {
        private readonly IDatabase _redis;
        private readonly ILogger<CartManager> _logger;

        //Kritik nokta: TTL(30 gün). Aktif olmayan sepetler otomatik silinir.Bu, Redis'in en güçlü yanı.
        private static readonly TimeSpan CartTtl = TimeSpan.FromDays(30);

        public CartManager(IConnectionMultiplexer redis, ILogger<CartManager> logger)
        {
            _redis = redis.GetDatabase();
            _logger = logger;
        }

        private static string Key(string userId) => $"cart:{userId}";
        public async Task<Cart> GetCartAsync(string userId)
        {
            var json = await _redis.StringGetAsync(Key(userId));

            if (json.IsNullOrEmpty)
                return new Cart { UserId = userId };

            var jsonString = json.ToString();
            return JsonSerializer.Deserialize<Cart>(jsonString)
                   ?? new Cart { UserId = userId };
        }

        public async Task<Cart> AddItemAsync(string userId, CartItem item)
        {
            var cart = await GetCartAsync(userId);

            var existing = cart.Items.FirstOrDefault(i => i.ProductId == item.ProductId);
            if (existing is not null)
            {
                cart.Items.Remove(existing);
                cart.Items.Add(existing with { Quantity = existing.Quantity + item.Quantity });
            }
            else
            {
                cart.Items.Add(item);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await SaveAsync(cart);
            return cart;
        }

        public async Task<Cart> UpdateItemAsync(string userId, int productId, int quantity)
        {
            var cart = await GetCartAsync(userId);
            cart.Items.RemoveAll(i => i.ProductId == productId);
            if (quantity > 0)
                cart.Items.Add(new CartItem(productId, quantity));

            cart.UpdatedAt = DateTime.UtcNow;
            await SaveAsync(cart);
            return cart;
        }

        public async Task<Cart> RemoveItemAsync(string userId, int productId)
        {
            var cart = await GetCartAsync(userId);
            cart.Items.RemoveAll(i => i.ProductId == productId);
            cart.UpdatedAt = DateTime.UtcNow;
            await SaveAsync(cart);
            return cart;
        }
        private async Task SaveAsync(Cart cart)
        {
            var json = JsonSerializer.Serialize(cart);
            await _redis.StringSetAsync(Key(cart.UserId), json, CartTtl);
        }
    }
}
