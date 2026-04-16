using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Data;

public class GeminiSettings
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Provider { get; set; } = "GoogleGemini";

    [MaxLength(256)]
    public string? ApiKey { get; set; }

    [MaxLength(200)]
    public string? SelectedModel { get; set; }

    [MaxLength(8000)]
    public string? InvoiceImageToTextPrompt { get; set; }

    [MaxLength(8000)]
    public string? InvoiceStructuredExtractionPrompt { get; set; }

    public DateTime? ModelsLastRefreshedUtc { get; set; }

    public ICollection<GeminiAvailableModel> AvailableModels { get; set; } = new List<GeminiAvailableModel>();
}
