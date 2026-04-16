using InventoryManagement.Data;
using InventoryManagement.Security;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Pages.Gemini;

[Authorize(Roles = Roles.Manager)]
public class SettingsModel : PageModel
{
    private const string LegacyImageToTextPrompt = "Extract all readable text from this invoice image. Preserve line breaks. Return plain text only.";
    private const string LegacyStructuredExtractionPrompt = "You are given OCR text from an invoice. Return only JSON in this exact format: {\"supplierName\":\"string\",\"items\":[{\"productName\":\"string\",\"quantity\":number}]}. Use quantity as integer >= 1. If unknown, use 1. Do not include markdown.";
    private const string PreviousImageToTextPrompt = "Extract all readable text from this invoice image. Preserve line breaks, table rows, headers, and column names exactly as they appear. Pay special attention to vendor/provider/supplier names and item columns such as quantity, qty, amount, units, pcs, count, description, and product name. Return plain text only.";
    private const string PreviousStructuredExtractionPrompt = "You are given OCR text from an invoice. Identify the supplier name from labels such as supplier, vendor, provider, sold by, from, bill from, or company. Extract invoice line items and map product names from description or item name fields. Extract quantity from columns or labels such as quantity, qty, amount, units, unit(s), pcs, pieces, or count. Return only JSON in this exact format: {\"supplierName\":\"string\",\"items\":[{\"productName\":\"string\",\"quantity\":number}]}. Use quantity as integer >= 1. If a row has an amount column that clearly represents item count, use it as quantity. Do not include markdown or explanations.";
    private const string CurrentImageToTextPrompt = "Extract all readable text from this invoice image. Preserve line breaks, table rows, headers, column names, top header text, and text placed near logos exactly as they appear. Pay special attention to vendor/provider/supplier names, company branding near the logo, and item columns such as quantity, qty, amount, units, pcs, count, description, and product name. Return plain text only.";
    private const string CurrentStructuredExtractionPrompt = "You are given OCR text from an invoice. Identify the supplier name from labels such as supplier, vendor, provider, sold by, from, bill from, company, or business name. If there is no explicit label, infer the supplier from the company name, brand name, or text located in the header area near the logo. Extract invoice line items and map product names from description or item name fields. Extract quantity from columns or labels such as quantity, qty, amount, units, unit(s), pcs, pieces, or count. Return only JSON in this exact format: {\"supplierName\":\"string\",\"items\":[{\"productName\":\"string\",\"quantity\":number}]}. Use quantity as integer >= 1. If a row has an amount column that clearly represents item count, use it as quantity. Do not include markdown or explanations.";

    private readonly ApplicationDbContext _context;
    private readonly IGeminiModelCatalogService _geminiModelCatalogService;

    /// <summary>
    /// Initializes the Gemini settings page model with configuration data and model catalog services.
    /// </summary>
    /// <param name="context">Application database context used to store Gemini settings and model metadata.</param>
    /// <param name="geminiModelCatalogService">Service used to fetch available Gemini models from provider APIs.</param>
    /// <remarks>
    /// Expected output: a page model ready to load and persist Gemini configuration.
    /// Possible errors: dependency resolution errors may occur if required services are missing.
    /// </remarks>
    public SettingsModel(ApplicationDbContext context, IGeminiModelCatalogService geminiModelCatalogService)
    {
        _context = context;
        _geminiModelCatalogService = geminiModelCatalogService;
    }

    [BindProperty]
    public string ApiKeyInput { get; set; } = string.Empty;

    [BindProperty]
    public string? SelectedModel { get; set; }

    [BindProperty]
    public string InvoiceImageToTextPrompt { get; set; } = GeminiInvoicePromptDefaults.ImageToTextPrompt;

    [BindProperty]
    public string InvoiceStructuredExtractionPrompt { get; set; } = GeminiInvoicePromptDefaults.StructuredExtractionPrompt;

    public bool HasApiKey { get; private set; }

    public DateTime? ModelsLastRefreshedUtc { get; private set; }

    public List<SelectListItem> ModelOptions { get; private set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Loads current Gemini settings and available model options for initial page rendering.
    /// </summary>
    /// <returns>A task representing asynchronous page data loading.</returns>
    /// <remarks>
    /// Expected output: API key status, selected model, prompts, and model options populated.
    /// Possible errors: data access exceptions can propagate during settings load.
    /// </remarks>
    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    /// <summary>
    /// Saves the provided Gemini API key to persistent settings.
    /// </summary>
    /// <returns>A redirect to the settings page with success or validation status.</returns>
    /// <remarks>
    /// Expected output: stored API key updated when input is non-empty.
    /// Possible errors: data access exceptions can propagate during save.
    /// </remarks>
    public async Task<IActionResult> OnPostSaveApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            ErrorMessage = "API key is required.";
            return RedirectToPage();
        }

        var settings = await GetOrCreateSettingsAsync();
        settings.ApiKey = ApiKeyInput.Trim();

        await _context.SaveChangesAsync();

        StatusMessage = "Gemini API key saved.";
        return RedirectToPage();
    }

    /// <summary>
    /// Refreshes available Gemini models from the provider and stores them in the database.
    /// </summary>
    /// <returns>A redirect to the settings page with refresh status.</returns>
    /// <remarks>
    /// Expected output: available model list replaced with latest provider data.
    /// Possible errors: HTTP request failures are handled; data access exceptions may propagate.
    /// </remarks>
    public async Task<IActionResult> OnPostRefreshModelsAsync()
    {
        var settings = await GetOrCreateSettingsAsync();

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            ErrorMessage = "Set the Gemini API key before refreshing models.";
            return RedirectToPage();
        }

        try
        {
            var models = await _geminiModelCatalogService.GetAvailableModelsAsync(settings.ApiKey);

            _context.GeminiAvailableModels.RemoveRange(settings.AvailableModels);
            settings.AvailableModels.Clear();

            foreach (var model in models)
            {
                settings.AvailableModels.Add(new GeminiAvailableModel
                {
                    ModelName = model.Name,
                    DisplayName = model.DisplayName
                });
            }

            if (!settings.AvailableModels.Any(m => string.Equals(m.ModelName, settings.SelectedModel, StringComparison.OrdinalIgnoreCase)))
            {
                settings.SelectedModel = null;
            }

            settings.ModelsLastRefreshedUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            StatusMessage = $"Refreshed {settings.AvailableModels.Count} Gemini models.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Failed to refresh models from Gemini API. Verify the API key and try again.";
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Persists the selected Gemini model after validating it exists in refreshed model data.
    /// </summary>
    /// <returns>A redirect to the settings page with success or validation status.</returns>
    /// <remarks>
    /// Expected output: selected model stored for later invoice extraction calls.
    /// Possible errors: data access exceptions can propagate during save.
    /// </remarks>
    public async Task<IActionResult> OnPostSelectModelAsync()
    {
        var settings = await GetOrCreateSettingsAsync();

        if (string.IsNullOrWhiteSpace(SelectedModel))
        {
            ErrorMessage = "Select a model before saving.";
            return RedirectToPage();
        }

        var isKnownModel = settings.AvailableModels
            .Any(m => string.Equals(m.ModelName, SelectedModel, StringComparison.OrdinalIgnoreCase));

        if (!isKnownModel)
        {
            ErrorMessage = "Selected model is not in the refreshed Gemini model list.";
            return RedirectToPage();
        }

        settings.SelectedModel = SelectedModel;
        await _context.SaveChangesAsync();

        StatusMessage = $"Gemini model '{SelectedModel}' selected.";
        return RedirectToPage();
    }

    /// <summary>
    /// Saves invoice extraction prompt templates used by Gemini extraction workflows.
    /// </summary>
    /// <returns>A redirect to the settings page with success or validation status.</returns>
    /// <remarks>
    /// Expected output: prompts persisted and used as defaults for extraction.
    /// Possible errors: data access exceptions can propagate during save.
    /// </remarks>
    public async Task<IActionResult> OnPostSaveInvoicePromptsAsync()
    {
        if (string.IsNullOrWhiteSpace(InvoiceImageToTextPrompt) || string.IsNullOrWhiteSpace(InvoiceStructuredExtractionPrompt))
        {
            ErrorMessage = "Both invoice prompts are required.";
            return RedirectToPage();
        }

        var settings = await GetOrCreateSettingsAsync();
        settings.InvoiceImageToTextPrompt = InvoiceImageToTextPrompt.Trim();
        settings.InvoiceStructuredExtractionPrompt = InvoiceStructuredExtractionPrompt.Trim();

        await _context.SaveChangesAsync();

        StatusMessage = "Invoice extraction prompts saved.";
        return RedirectToPage();
    }

    /// <summary>
    /// Loads persisted settings into bindable properties and model dropdown options.
    /// </summary>
    /// <returns>A task representing asynchronous settings load.</returns>
    /// <remarks>
    /// Expected output: bind properties synchronized with persisted settings values.
    /// Possible errors: data access exceptions can propagate during settings retrieval.
    /// </remarks>
    private async Task LoadPageDataAsync()
    {
        var settings = await GetOrCreateSettingsAsync();

        HasApiKey = !string.IsNullOrWhiteSpace(settings.ApiKey);
        ModelsLastRefreshedUtc = settings.ModelsLastRefreshedUtc;
        SelectedModel = settings.SelectedModel;
        InvoiceImageToTextPrompt = settings.InvoiceImageToTextPrompt ?? GeminiInvoicePromptDefaults.ImageToTextPrompt;
        InvoiceStructuredExtractionPrompt = settings.InvoiceStructuredExtractionPrompt ?? GeminiInvoicePromptDefaults.StructuredExtractionPrompt;

        ModelOptions = settings.AvailableModels
            .OrderBy(m => m.ModelName)
            .Select(m => new SelectListItem
            {
                Value = m.ModelName,
                Text = string.IsNullOrWhiteSpace(m.DisplayName)
                    ? m.ModelName
                    : $"{m.DisplayName} ({m.ModelName})",
                Selected = string.Equals(m.ModelName, settings.SelectedModel, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();
    }

    /// <summary>
    /// Retrieves existing Gemini settings or creates a default settings record when none exists.
    /// </summary>
    /// <returns>The existing or newly created settings entity.</returns>
    /// <remarks>
    /// Expected output: a settings entity with normalized invoice prompt defaults.
    /// Possible errors: data access exceptions can propagate during retrieval and save operations.
    /// </remarks>
    private async Task<GeminiSettings> GetOrCreateSettingsAsync()
    {
        var settings = await _context.GeminiSettings
            .Include(s => s.AvailableModels)
            .SingleOrDefaultAsync(s => s.Provider == "GoogleGemini");

        if (settings != null)
        {
            settings.InvoiceImageToTextPrompt = NormalizePrompt(
                settings.InvoiceImageToTextPrompt,
                [LegacyImageToTextPrompt, PreviousImageToTextPrompt, CurrentImageToTextPrompt],
                GeminiInvoicePromptDefaults.ImageToTextPrompt);

            settings.InvoiceStructuredExtractionPrompt = NormalizePrompt(
                settings.InvoiceStructuredExtractionPrompt,
                [LegacyStructuredExtractionPrompt, PreviousStructuredExtractionPrompt, CurrentStructuredExtractionPrompt],
                GeminiInvoicePromptDefaults.StructuredExtractionPrompt);

            return settings;
        }

        settings = new GeminiSettings
        {
            InvoiceImageToTextPrompt = GeminiInvoicePromptDefaults.ImageToTextPrompt,
            InvoiceStructuredExtractionPrompt = GeminiInvoicePromptDefaults.StructuredExtractionPrompt
        };

        _context.GeminiSettings.Add(settings);
        await _context.SaveChangesAsync();
        await _context.Entry(settings).Collection(s => s.AvailableModels).LoadAsync();
        return settings;
    }

    /// <summary>
    /// Normalizes prompt text by replacing blank or legacy default values with the latest default prompt.
    /// </summary>
    /// <param name="currentValue">Current prompt value from persistence.</param>
    /// <param name="legacyDefaults">Known historical default prompt values.</param>
    /// <param name="latestDefault">Current default prompt to apply when normalization is needed.</param>
    /// <returns>The original prompt value or the latest default when normalization applies.</returns>
    /// <remarks>
    /// Expected output: prompt text aligned to current defaults while preserving user customizations.
    /// Possible errors: no custom exceptions are thrown by this method.
    /// </remarks>
    private static string NormalizePrompt(string? currentValue, IReadOnlyCollection<string> legacyDefaults, string latestDefault)
    {
        if (string.IsNullOrWhiteSpace(currentValue) || legacyDefaults.Contains(currentValue, StringComparer.Ordinal))
        {
            return latestDefault;
        }

        return currentValue;
    }
}
