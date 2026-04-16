using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InventoryManagement.Services;

public interface IGeminiInvoiceExtractionService
{
    /// <summary>
    /// Executes the full Gemini invoice extraction pipeline and returns structured invoice data.
    /// </summary>
    /// <param name="request">Extraction request containing credentials, model, image content, and prompts.</param>
    /// <param name="cancellationToken">Cancellation token used to cancel remote API operations.</param>
    /// <returns>A structured invoice containing supplier and item information plus OCR text.</returns>
    /// <remarks>
    /// Expected output: parsed invoice data from the supplied invoice image.
    /// Possible errors: throws <see cref="GeminiExtractionException"/> when provider calls or parsing fail.
    /// </remarks>
    Task<GeminiExtractedInvoice> ExtractInvoiceAsync(GeminiInvoiceExtractionRequest request, CancellationToken cancellationToken = default);
}

public sealed class GeminiInvoiceExtractionRequest
{
    public required string ApiKey { get; init; }
    public required string Model { get; init; }
    public required string ImageMimeType { get; init; }
    public required byte[] ImageBytes { get; init; }
    public required string ImageToTextPrompt { get; init; }
    public required string StructuredExtractionPrompt { get; init; }
}

public sealed class GeminiExtractedInvoice
{
    public string? SupplierName { get; set; }
    public List<GeminiExtractedInvoiceItem> Items { get; set; } = new();
    public string? RawOcrText { get; set; }
}

public sealed class GeminiExtractedInvoiceItem
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public sealed class GeminiExtractionException : Exception
{
    /// <summary>
    /// Initializes an extraction exception with the failing pipeline step and contextual message.
    /// </summary>
    /// <param name="step">Pipeline step where the failure occurred.</param>
    /// <param name="message">Error message that describes the failure.</param>
    /// <param name="innerException">Underlying exception that triggered this extraction failure, when available.</param>
    /// <remarks>
    /// Expected output: an exception instance that preserves step-specific context.
    /// Possible errors: no custom exceptions are thrown by this constructor.
    /// </remarks>
    public GeminiExtractionException(string step, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Step = step;
    }

    public string Step { get; }
}

public class GeminiInvoiceExtractionService : IGeminiInvoiceExtractionService
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes an invoice extraction service with an HTTP client configured for Gemini API access.
    /// </summary>
    /// <param name="httpClient">HTTP client used to execute Gemini generate-content requests.</param>
    /// <remarks>
    /// Expected output: a service instance ready to run extraction operations.
    /// Possible errors: no custom exceptions are thrown by this constructor.
    /// </remarks>
    public GeminiInvoiceExtractionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Runs OCR and structured extraction steps against Gemini, then parses the final invoice result.
    /// </summary>
    /// <param name="request">Extraction request containing API key, model, prompts, and image bytes.</param>
    /// <param name="cancellationToken">Cancellation token used to cancel provider requests.</param>
    /// <returns>A structured invoice populated from Gemini responses.</returns>
    /// <remarks>
    /// Expected output: supplier name, normalized line items, and raw OCR text.
    /// Possible errors: throws <see cref="GeminiExtractionException"/> for OCR, extraction, HTTP, or parsing failures.
    /// </remarks>
    public async Task<GeminiExtractedInvoice> ExtractInvoiceAsync(GeminiInvoiceExtractionRequest request, CancellationToken cancellationToken = default)
    {
        var imageBase64 = Convert.ToBase64String(request.ImageBytes);

        string ocrText;
        try
        {
            ocrText = await GenerateTextAsync(
                request.Model,
                request.ApiKey,
                [
                    new GeminiTextRequestPart { Text = request.ImageToTextPrompt },
                    new GeminiInlineImagePart(request.ImageMimeType, imageBase64)
                ],
                cancellationToken);
        }
        catch (Exception ex) when (ex is not GeminiExtractionException)
        {
            throw new GeminiExtractionException("image-to-text", "Gemini failed during invoice OCR step.", ex);
        }

        string structuredJson;
        try
        {
            structuredJson = await GenerateTextAsync(
                request.Model,
                request.ApiKey,
                [
                    new GeminiTextRequestPart { Text = request.StructuredExtractionPrompt },
                    new GeminiTextRequestPart { Text = ocrText }
                ],
                cancellationToken);
        }
        catch (Exception ex) when (ex is not GeminiExtractionException)
        {
            throw new GeminiExtractionException("text-to-object", "Gemini failed during structured extraction step.", ex);
        }

        GeminiExtractedInvoice invoice;
        try
        {
            invoice = DeserializeInvoice(structuredJson);
        }
        catch (Exception ex)
        {
            throw new GeminiExtractionException("parse-structured-output", "Gemini returned a response that could not be parsed as invoice JSON.", ex);
        }

        invoice.RawOcrText = ocrText;

        return invoice;
    }

    /// <summary>
    /// Sends a generate-content request to Gemini and returns the first non-empty text response.
    /// </summary>
    /// <param name="model">Model identifier used for the request endpoint.</param>
    /// <param name="apiKey">API key used to authorize the request.</param>
    /// <param name="parts">Request content parts passed to Gemini.</param>
    /// <param name="cancellationToken">Cancellation token used to cancel the HTTP operation.</param>
    /// <returns>The trimmed text returned by Gemini.</returns>
    /// <remarks>
    /// Expected output: non-empty generated text from the provider.
    /// Possible errors: throws <see cref="GeminiExtractionException"/> for HTTP failures or empty responses.
    /// </remarks>
    private async Task<string> GenerateTextAsync(string model, string apiKey, IReadOnlyList<object> parts, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}",
            new GeminiGenerateContentRequest
            {
                Contents =
                [
                    new GeminiContent
                    {
                        Parts = parts
                    }
                ]
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var snippet = Truncate(body, 700);
            throw new GeminiExtractionException(
                "gemini-http",
                $"Gemini API call failed with HTTP {(int)response.StatusCode} ({response.StatusCode}). Response: {snippet}");
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(cancellationToken: cancellationToken);
        var text = payload?.Candidates?
            .SelectMany(c => c.Content?.Parts ?? new List<GeminiTextPart>())
            .Select(p => p.Text)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new GeminiExtractionException("empty-response", "Gemini response did not contain any text output.");
        }

        return text.Trim();
    }

    /// <summary>
    /// Converts a Gemini structured text response into a normalized invoice object.
    /// </summary>
    /// <param name="responseText">Structured response payload returned by Gemini.</param>
    /// <returns>A normalized invoice object with filtered items and cleaned values.</returns>
    /// <remarks>
    /// Expected output: parsed invoice object, or an empty invoice when payload resolves to null.
    /// Possible errors: propagates JSON parsing exceptions for invalid JSON content.
    /// </remarks>
    private static GeminiExtractedInvoice DeserializeInvoice(string responseText)
    {
        var json = StripCodeFence(responseText);

        var parsed = JsonSerializer.Deserialize<GeminiExtractedInvoice>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (parsed == null)
        {
            return new GeminiExtractedInvoice();
        }

        parsed.Items = parsed.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.ProductName))
            .Select(i => new GeminiExtractedInvoiceItem
            {
                ProductName = i.ProductName.Trim(),
                Quantity = i.Quantity <= 0 ? 1 : i.Quantity
            })
            .ToList();

        parsed.SupplierName = parsed.SupplierName?.Trim();

        return parsed;
    }

    /// <summary>
    /// Removes surrounding markdown code fences from generated content when present.
    /// </summary>
    /// <param name="input">Text that may include fenced markdown content.</param>
    /// <returns>Unfenced text content trimmed for downstream parsing.</returns>
    /// <remarks>
    /// Expected output: raw JSON or plain text without surrounding backtick fences.
    /// Possible errors: no custom exceptions are thrown by this method.
    /// </remarks>
    private static string StripCodeFence(string input)
    {
        var trimmed = input.Trim();
        if (!trimmed.StartsWith("```") || !trimmed.EndsWith("```"))
        {
            return trimmed;
        }

        var firstLineEnd = trimmed.IndexOf('\n');
        if (firstLineEnd < 0)
        {
            return trimmed;
        }

        var content = trimmed[(firstLineEnd + 1)..];
        var lastFence = content.LastIndexOf("```");
        return lastFence >= 0 ? content[..lastFence].Trim() : content.Trim();
    }

    /// <summary>
    /// Truncates a string to a fixed maximum length for concise diagnostic output.
    /// </summary>
    /// <param name="value">Input value to truncate.</param>
    /// <param name="maxLength">Maximum number of characters to preserve.</param>
    /// <returns>The original string when short enough; otherwise a truncated string with ellipsis.</returns>
    /// <remarks>
    /// Expected output: bounded-length text safe for logs and exception messages.
    /// Possible errors: no custom exceptions are thrown by this method.
    /// </remarks>
    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(empty response body)";
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }

    private sealed class GeminiGenerateContentRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = new();
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("parts")]
        public IReadOnlyList<object> Parts { get; set; } = Array.Empty<object>();
    }

    private sealed class GeminiTextRequestPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class GeminiInlineImagePart
    {
        /// <summary>
        /// Initializes an inline image request part for Gemini generate-content payloads.
        /// </summary>
        /// <param name="mimeType">MIME type of the embedded image content.</param>
        /// <param name="data">Base64-encoded image payload.</param>
        /// <remarks>
        /// Expected output: a request part containing valid inline image metadata and bytes.
        /// Possible errors: no custom exceptions are thrown by this constructor.
        /// </remarks>
        public GeminiInlineImagePart(string mimeType, string data)
        {
            InlineData = new GeminiInlineData { MimeType = mimeType, Data = data };
        }

        [JsonPropertyName("inline_data")]
        public GeminiInlineData InlineData { get; }
    }

    private sealed class GeminiInlineData
    {
        [JsonPropertyName("mime_type")]
        public string MimeType { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;
    }

    private sealed class GeminiGenerateContentResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiCandidateContent? Content { get; set; }
    }

    private sealed class GeminiCandidateContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiTextPart>? Parts { get; set; }
    }

    private sealed class GeminiTextPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
