using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Data;

public class GeminiAvailableModel
{
    public int Id { get; set; }

    public int GeminiSettingsId { get; set; }

    public GeminiSettings? GeminiSettings { get; set; }

    [Required]
    [MaxLength(200)]
    public string ModelName { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? DisplayName { get; set; }
}
