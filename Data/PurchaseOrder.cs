using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Data
{
    public enum PurchaseOrderStatus
    {
        InProcess = 0,
        Completed = 1
    }

    public class PurchaseOrder
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string? OrderedByUserId { get; set; }
        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.InProcess;
        public ICollection<PurchaseOrderProduct> PurchaseOrderProducts { get; set; } = new List<PurchaseOrderProduct>();
    }
}