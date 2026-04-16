using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Data;

public class Supplier
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
