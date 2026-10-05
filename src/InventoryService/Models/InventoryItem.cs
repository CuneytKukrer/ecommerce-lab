namespace InventoryService.Models;

public class InventoryItem
{
    public int ProductId { get; set; }
    public int Available { get; set; }
    public int Reserved { get; set; }
    public int Version { get; set; }    
}