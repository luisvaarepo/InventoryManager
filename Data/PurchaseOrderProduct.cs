namespace InventoryManagement.Data
{
    public class PurchaseOrderProduct
    {
        public int PurchaseOrderId { get; set; }
        public PurchaseOrder PurchaseOrder { get; set; } = null!;
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public int QuantityAdded { get; set; }
    }
}