namespace CartService.Models;
//CartItem immutable olduğundan record kullandık. AI: Immutable nesneler, oluşturulduktan sonra değiştirilemeyen nesnelerdir. Bu, verilerin güvenliğini ve tutarlılığını artırır.
public record CartItem(int ProductId, int Quantity);
