using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Data
{
    public class Product
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        [Required]
        public string UPC { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
        public decimal Cost { get; set; }
        public int Quantity { get; set; }
        public DateTime? DateLastPurchased { get; set; }
        public int? EstimatedTimeToReceiveWeeks { get; set; }
        [Range(0, int.MaxValue)]
        public int LowStockThreshold { get; set; }
        public bool IsDiscontinued { get; set; }
        public bool IsLowStock => !IsDiscontinued && Quantity < LowStockThreshold;
        public ICollection<Category> Categories { get; set; } = new List<Category>();
        public ICollection<PurchaseOrderProduct> PurchaseOrderProducts { get; set; } = new List<PurchaseOrderProduct>();
    }
}