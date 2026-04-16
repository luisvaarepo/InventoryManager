using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace InventoryManagement.Services;

/// <summary>
/// Defines methods to access and retrieve Gemini model catalog data.
/// </summary>
public interface IGeminiModelCatalogService
{
    /// <summary>
    /// Retrieves Gemini model metadata and returns models that support text generation requests.
    /// </summary>
    /// <param name="apiKey">Provider API key used to authenticate the model listing request.</param>
    /// <param name="cancellationToken">Cancellation token used to cancel the HTTP operation.</param>
    /// <returns>A read-only list of normalized model entries sorted by name.</returns>
    /// <remarks>
    /// Expected output: distinct model names without the API prefix and optional display labels.
    /// Possible errors: propagates HTTP/network exceptions and JSON deserialization exceptions.
    /// </remarks>
    Task<IReadOnlyList<GeminiModelItem>> GetAvailableModelsAsync(string apiKey, CancellationToken cancellationToken = default);
}

public sealed record GeminiModelItem(string Name, string? DisplayName);

/// <summary>
/// Implements <see cref="IGeminiModelCatalogService"/> to provide model catalog operations.
/// </summary>
public class GeminiModelCatalogService : IGeminiModelCatalogService
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a model catalog service with an HTTP client configured for Gemini API access.
    /// </summary>
    /// <param name="httpClient">HTTP client used to call Gemini model endpoints.</param>
    /// <remarks>
    /// Expected output: a service instance ready to execute model catalog requests.
    /// Possible errors: no custom exceptions are thrown by this constructor.
    /// </remarks>
    public GeminiModelCatalogService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Calls Gemini model endpoints and returns normalized model names supported for content generation.
    /// </summary>
    /// <param name="apiKey">Provider API key used to authenticate the model listing request.</param>
    /// <param name="cancellationToken">Cancellation token used to cancel the HTTP operation.</param>
    /// <returns>A read-only list of unique model entries.</returns>
    /// <remarks>
    /// Expected output: list of models compatible with generate-content operations.
    /// Possible errors: throws for unsuccessful HTTP responses and malformed payloads.
    /// </remarks>
    public async Task<IReadOnlyList<GeminiModelItem>> GetAvailableModelsAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"models?key={Uri.EscapeDataString(apiKey)}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GeminiModelsResponse>(cancellationToken: cancellationToken);
        var models = payload?.Models
            ?.Where(m => m.SupportedGenerationMethods?.Contains("generateContent", StringComparer.OrdinalIgnoreCase) == true)
            .Select(m => new GeminiModelItem(NormalizeModelName(m.Name), m.DisplayName))
            .Where(m => !string.IsNullOrWhiteSpace(m.Name))
            .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<GeminiModelItem>();

        return models;
    }

    /// <summary>
    /// Removes the Gemini API "models/" prefix from a model name when present.
    /// </summary>
    /// <param name="apiModelName">Raw model name returned by the provider API.</param>
    /// <returns>The normalized model name or an empty string when input is blank.</returns>
    /// <remarks>
    /// Expected output: model name suitable for API routing in later requests.
    /// Possible errors: no custom exceptions are thrown by this method.
    /// </remarks>
    private static string NormalizeModelName(string? apiModelName)
    {
        if (string.IsNullOrWhiteSpace(apiModelName))
        {
            return string.Empty;
        }

        const string prefix = "models/";
        return apiModelName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? apiModelName[prefix.Length..]
            : apiModelName;
    }

    private sealed class GeminiModelsResponse
    {
        [JsonPropertyName("models")]
        public List<GeminiModelDto>? Models { get; set; }
    }

    private sealed class GeminiModelDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("supportedGenerationMethods")]
        public List<string>? SupportedGenerationMethods { get; set; }
    }
}
