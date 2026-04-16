using System;

namespace InventoryManagement.Data
{
    public class Audit
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string? Action { get; set; }
        public string? TableName { get; set; }
        public int? RecordId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Details { get; set; }
    }
}