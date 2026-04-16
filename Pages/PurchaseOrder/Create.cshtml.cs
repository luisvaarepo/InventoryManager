using InventoryManagement.Data;
using InventoryManagement.Services;
using ProductEntity = InventoryManagement.Data.Product;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagement.Pages.PurchaseOrder
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IGeminiInvoiceExtractionService _geminiInvoiceExtractionService;

        /// <summary>
        /// Initializes the purchase order creation page model with data and Gemini extraction services.
        /// </summary>
        /// <param name="context">Application database context used for suppliers, products, and order persistence.</param>
        /// <param name="geminiInvoiceExtractionService">Service used to extract invoice data from uploaded images.</param>
        /// <remarks>
        /// Expected output: a page model ready for manual and AI-assisted purchase order creation.
        /// Possible errors: dependency resolution errors may occur when required services are missing.
        /// </remarks>
        public CreateModel(ApplicationDbContext context, IGeminiInvoiceExtractionService geminiInvoiceExtractionService)
        {
            _context = context;
            _geminiInvoiceExtractionService = geminiInvoiceExtractionService;
        }

        [BindProperty]
        public int SelectedSupplierId { get; set; }
        public IList<Supplier> Suppliers { get; set; } = new List<Supplier>();

        [BindProperty]
        public int SelectedProductId { get; set; }
        [BindProperty]
        public int ProductQuantity { get; set; }
        public IList<ProductEntity> SupplierProducts { get; set; } = new List<ProductEntity>();

        [BindProperty]
        public IFormFile? InvoiceImageFile { get; set; }

        [BindProperty]
        public string? InvoiceImageBase64 { get; set; }

        [BindProperty]
        public string? InvoiceImageMimeType { get; set; }

        public string? GeminiDebugMessage { get; private set; }

        // Represents the current invoice lines (products added to the order)
        [BindProperty]
        public List<OrderLine> OrderLines { get; set; } = new List<OrderLine>();

        [BindProperty]
        public string? OrderLinesJson { get; set; }

        [BindProperty]
        public string? UnmatchedExtractedItemsJson { get; set; }

        public List<UnmatchedExtractedItem> UnmatchedExtractedItems { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        /// <summary>
        /// Loads supplier and product data and refreshes serialized order state for the current selection.
        /// </summary>
        /// <returns>A task representing asynchronous page data loading.</returns>
        /// <remarks>
        /// Expected output: supplier list, supplier-specific products, and serialized line state are populated.
        /// Possible errors: data access exceptions can propagate during supplier and product queries.
        /// </remarks>
        private async Task LoadPageDataAsync()
        {
            Suppliers = await _context.Suppliers.AsNoTracking().ToListAsync();

            if (SelectedSupplierId > 0)
            {
                SupplierProducts = await _context.Products
                    .Where(p => p.SupplierId == SelectedSupplierId && !p.IsDiscontinued)
                    .AsNoTracking()
                    .ToListAsync();
            }
            else
            {
                SupplierProducts = new List<ProductEntity>();
            }

            UpdateOrderLinesJson();
            UpdateUnmatchedExtractedItemsJson();
        }

        /// <summary>
        /// Restores order line items from the serialized hidden field payload.
        /// </summary>
        /// <remarks>
        /// Expected output: <see cref="OrderLines"/> rehydrated when valid JSON exists.
        /// Possible errors: malformed JSON is swallowed and written to debug output.
        /// </remarks>
        private void RestoreOrderLinesFromJson()
        {
            if (!string.IsNullOrEmpty(OrderLinesJson))
            {
                try
                {
                    var restored = System.Text.Json.JsonSerializer.Deserialize<List<OrderLine>>(OrderLinesJson);
                    if (restored != null)
                        OrderLines = restored;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OrderLinesJson deserialization failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Restores unmatched extracted invoice items from serialized hidden field payload.
        /// </summary>
        /// <remarks>
        /// Expected output: <see cref="UnmatchedExtractedItems"/> rehydrated when valid JSON exists.
        /// Possible errors: malformed JSON is swallowed and written to debug output.
        /// </remarks>
        private void RestoreUnmatchedExtractedItemsFromJson()
        {
            if (!string.IsNullOrEmpty(UnmatchedExtractedItemsJson))
            {
                try
                {
                    var restored = System.Text.Json.JsonSerializer.Deserialize<List<UnmatchedExtractedItem>>(UnmatchedExtractedItemsJson);
                    if (restored != null)
                    {
                        UnmatchedExtractedItems = restored;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UnmatchedExtractedItemsJson deserialization failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Serializes current order lines for round-tripping across postbacks.
        /// </summary>
        /// <remarks>
        /// Expected output: <see cref="OrderLinesJson"/> contains serialized order lines.
        /// Possible errors: serialization exceptions may propagate for unsupported object graphs.
        /// </remarks>
        private void UpdateOrderLinesJson()
        {
            OrderLinesJson = System.Text.Json.JsonSerializer.Serialize(OrderLines);
        }

        /// <summary>
        /// Serializes unmatched extracted items for round-tripping across postbacks.
        /// </summary>
        /// <remarks>
        /// Expected output: <see cref="UnmatchedExtractedItemsJson"/> contains serialized unmatched items.
        /// Possible errors: serialization exceptions may propagate for unsupported object graphs.
        /// </remarks>
        private void UpdateUnmatchedExtractedItemsJson()
        {
            UnmatchedExtractedItemsJson = System.Text.Json.JsonSerializer.Serialize(UnmatchedExtractedItems);
        }

        public decimal InvoiceTotal => OrderLines?.Sum(l => l.LineTotal) ?? 0;

        public class OrderLine
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public decimal Cost { get; set; }
            public int Quantity { get; set; }
            public decimal LineTotal => Cost * Quantity;
        }

        public class UnmatchedExtractedItem
        {
            public string ProductName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        private sealed class ExtractionMappingResult
        {
            public List<OrderLine> MatchedLines { get; } = new();
            public List<UnmatchedExtractedItem> UnmatchedItems { get; } = new();
        }

        /// <summary>
        /// Handles initial GET requests by loading suppliers and current page state.
        /// </summary>
        /// <returns>A task representing asynchronous page data loading.</returns>
        /// <remarks>
        /// Expected output: page is initialized for purchase order creation.
        /// Possible errors: data access exceptions can propagate from loading operations.
        /// </remarks>
        public async Task OnGetAsync()
        {
            await LoadPageDataAsync();
        }


        /// <summary>
        /// Handles supplier selection changes and reloads supplier-specific product data.
        /// </summary>
        /// <returns>The current page with refreshed supplier and product options.</returns>
        /// <remarks>
        /// Expected output: state restored and page refreshed for the chosen supplier.
        /// Possible errors: data access and state deserialization issues may surface during load.
        /// </remarks>
        public async Task<IActionResult> OnPostSelectSupplierAsync()
        {
            RestoreOrderLinesFromJson();
            RestoreUnmatchedExtractedItemsFromJson();
            await LoadPageDataAsync();
            return Page();
        }

        /// <summary>
        /// Validates and saves the current purchase order lines as a persisted purchase order.
        /// </summary>
        /// <returns>A validation page when input is invalid; otherwise a redirect to purchase order list.</returns>
        /// <remarks>
        /// Expected output: a new purchase order and related line items persisted when valid.
        /// Possible errors: database update exceptions can propagate during save.
        /// </remarks>
        public async Task<IActionResult> OnPostSaveAsync()
        {
            RestoreOrderLinesFromJson();
            RestoreUnmatchedExtractedItemsFromJson();

            if (OrderLines == null || OrderLines.Count == 0 || SelectedSupplierId == 0)
            {
                await LoadPageDataAsync();
                ModelState.AddModelError(string.Empty, "Please select a supplier and add at least one product.");
                return Page();
            }

            var order = new InventoryManagement.Data.PurchaseOrder
            {
                OrderDate = DateTime.UtcNow,
                OrderedByUserId = User?.Identity?.Name,
                SupplierId = SelectedSupplierId,
                Status = PurchaseOrderStatus.InProcess,
                PurchaseOrderProducts = new List<InventoryManagement.Data.PurchaseOrderProduct>()
            };

            foreach (var line in OrderLines)
            {
                order.PurchaseOrderProducts.Add(new InventoryManagement.Data.PurchaseOrderProduct
                {
                    ProductId = line.ProductId,
                    QuantityAdded = line.Quantity
                });
            }

            _context.PurchaseOrders.Add(order);
            var rowsWritten = await _context.SaveChangesAsync();

            if (rowsWritten <= 0)
            {
                await LoadPageDataAsync();
                ModelState.AddModelError(string.Empty, "The purchase order could not be saved.");
                return Page();
            }

            StatusMessage = $"Purchase order #{order.Id} saved.";
            return RedirectToPage("/PurchaseOrder/Index");
        }

        /// <summary>
        /// Adds a selected product line to the in-progress order when product and quantity are valid.
        /// </summary>
        /// <returns>The current page with updated order lines.</returns>
        /// <remarks>
        /// Expected output: a new non-duplicate order line added for the selected product.
        /// Possible errors: data access exceptions can propagate while validating selected product.
        /// </remarks>
        public async Task<IActionResult> OnPostAddProductAsync()
        {
            RestoreOrderLinesFromJson();
            RestoreUnmatchedExtractedItemsFromJson();
            if (SelectedProductId > 0 && ProductQuantity > 0)
            {
                var product = await _context.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == SelectedProductId && !p.IsDiscontinued);
                if (product != null)
                {
                    // Prevent duplicate product lines
                    if (!OrderLines.Any(l => l.ProductId == product.Id))
                    {
                        OrderLines.Add(new OrderLine
                        {
                            ProductId = product.Id,
                            ProductName = product.Name,
                            Cost = product.Cost,
                            Quantity = ProductQuantity
                        });
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "The selected product is no longer available for purchase orders.");
                }
            }

            await LoadPageDataAsync();
            return Page();
        }

        /// <summary>
        /// Removes a product line from the in-progress purchase order.
        /// </summary>
        /// <param name="productId">Product identifier of the line to remove.</param>
        /// <returns>The current page with updated order lines.</returns>
        /// <remarks>
        /// Expected output: matching order line removed when present.
        /// Possible errors: data access exceptions can propagate during page reload.
        /// </remarks>
        public async Task<IActionResult> OnPostRemoveProductAsync(int productId)
        {
            RestoreOrderLinesFromJson();
            RestoreUnmatchedExtractedItemsFromJson();
            var line = OrderLines.FirstOrDefault(l => l.ProductId == productId);
            if (line != null)
            {
                OrderLines.Remove(line);
            }

            await LoadPageDataAsync();
            return Page();
        }

        /// <summary>
        /// Executes Gemini-based invoice extraction and maps extracted lines to catalog products.
        /// </summary>
        /// <returns>The current page with mapped lines or validation errors, depending on extraction results.</returns>
        /// <remarks>
        /// Expected output: order lines and optional unmatched items prefilled from invoice data.
        /// Possible errors: throws and handles <see cref="GeminiExtractionException"/> or generic extraction failures.
        /// </remarks>
        public async Task<IActionResult> OnPostExtractFromInvoiceAsync()
        {
            RestoreOrderLinesFromJson();
            RestoreUnmatchedExtractedItemsFromJson();

            var settings = await _context.GeminiSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Provider == "GoogleGemini");

            if (settings == null || string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.SelectedModel))
            {
                await LoadPageDataAsync();
                ModelState.AddModelError(string.Empty, "Gemini is not configured. Set API key and selected model in Gemini settings.");
                return Page();
            }

            var imagePayload = await GetImagePayloadAsync();
            if (imagePayload == null)
            {
                await LoadPageDataAsync();
                ModelState.AddModelError(string.Empty, "Upload an invoice image or capture one from webcam before extraction.");
                return Page();
            }

            GeminiExtractedInvoice extraction;
            try
            {
                extraction = await _geminiInvoiceExtractionService.ExtractInvoiceAsync(new GeminiInvoiceExtractionRequest
                {
                    ApiKey = settings.ApiKey,
                    Model = settings.SelectedModel,
                    ImageBytes = imagePayload.Value.ImageBytes,
                    ImageMimeType = imagePayload.Value.ImageMimeType,
                    ImageToTextPrompt = settings.InvoiceImageToTextPrompt ?? GeminiInvoicePromptDefaults.ImageToTextPrompt,
                    StructuredExtractionPrompt = settings.InvoiceStructuredExtractionPrompt ?? GeminiInvoicePromptDefaults.StructuredExtractionPrompt
                });
            }
            catch (GeminiExtractionException ex)
            {
                await LoadPageDataAsync();
                GeminiDebugMessage = $"Gemini step '{ex.Step}' failed. {ex.Message}";
                ModelState.AddModelError(string.Empty, GeminiDebugMessage);
                return Page();
            }
            catch (Exception ex)
            {
                await LoadPageDataAsync();
                GeminiDebugMessage = $"Unexpected extraction error: {ex.Message}";
                ModelState.AddModelError(string.Empty, GeminiDebugMessage);
                return Page();
            }

            var products = await _context.Products
                .Where(p => !p.IsDiscontinued)
                .AsNoTracking()
                .ToListAsync();

            var suppliers = await _context.Suppliers
                .AsNoTracking()
                .ToListAsync();

            var matchedSupplierId = FindSupplierId(extraction.SupplierName, suppliers);
            if (matchedSupplierId.HasValue)
            {
                SelectedSupplierId = matchedSupplierId.Value;
                ModelState.Remove(nameof(SelectedSupplierId));
            }

            var mappingResult = MapExtractedLinesToOrderLines(extraction.Items, products, SelectedSupplierId);
            var mappedLines = mappingResult.MatchedLines;
            UnmatchedExtractedItems = mappingResult.UnmatchedItems;

            if (SelectedSupplierId == 0 && mappedLines.Count > 0)
            {
                var oneSupplierId = products
                    .Where(p => mappedLines.Any(l => l.ProductId == p.Id))
                    .Select(p => p.SupplierId)
                    .Distinct()
                    .ToList();

                if (oneSupplierId.Count == 1)
                {
                    SelectedSupplierId = oneSupplierId[0];
                    ModelState.Remove(nameof(SelectedSupplierId));
                    mappedLines = RemapMatchedLinesForSupplier(mappedLines, products, SelectedSupplierId);
                    UnmatchedExtractedItems = RebuildUnmatchedItems(mappedLines, extraction.Items, products, SelectedSupplierId);
                }
            }

            if (SelectedSupplierId > 0)
            {
                mappedLines = RemapMatchedLinesForSupplier(mappedLines, products, SelectedSupplierId);
                UnmatchedExtractedItems = RebuildUnmatchedItems(mappedLines, extraction.Items, products, SelectedSupplierId);
            }

            GeminiDebugMessage = $"Gemini call succeeded using model '{settings.SelectedModel}'. OCR length: {extraction.RawOcrText?.Length ?? 0} chars. Extracted supplier: '{extraction.SupplierName ?? "(none)"}'. Extracted items: {extraction.Items.Count}.";

            if (mappedLines.Count == 0)
            {
                await LoadPageDataAsync();
                ModelState.AddModelError(string.Empty, "No invoice lines could be matched to existing products.");
                return Page();
            }

            OrderLines = mappedLines;
            StatusMessage = $"Invoice extracted. Prefilled {OrderLines.Count} product lines.";

            await LoadPageDataAsync();
            return Page();
        }

        /// <summary>
        /// Builds image bytes and MIME type from uploaded file or captured base64 payload.
        /// </summary>
        /// <returns>Image payload tuple when valid input is available; otherwise <see langword="null"/>.</returns>
        /// <remarks>
        /// Expected output: normalized binary payload for Gemini extraction.
        /// Possible errors: invalid base64 input is handled by returning null.
        /// </remarks>
        private async Task<(byte[] ImageBytes, string ImageMimeType)?> GetImagePayloadAsync()
        {
            if (InvoiceImageFile is { Length: > 0 })
            {
                await using var stream = InvoiceImageFile.OpenReadStream();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                return (memory.ToArray(), string.IsNullOrWhiteSpace(InvoiceImageFile.ContentType) ? "image/jpeg" : InvoiceImageFile.ContentType);
            }

            if (!string.IsNullOrWhiteSpace(InvoiceImageBase64))
            {
                try
                {
                    return (Convert.FromBase64String(InvoiceImageBase64), string.IsNullOrWhiteSpace(InvoiceImageMimeType) ? "image/png" : InvoiceImageMimeType);
                }
                catch (FormatException)
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to match an extracted supplier name to a known supplier identifier.
        /// </summary>
        /// <param name="extractedSupplierName">Supplier name extracted from invoice text.</param>
        /// <param name="suppliers">Available suppliers used for exact, partial, and tokenized matching.</param>
        /// <returns>Matched supplier identifier when confidence exists; otherwise <see langword="null"/>.</returns>
        /// <remarks>
        /// Expected output: best candidate supplier id based on similarity matching.
        /// Possible errors: no custom exceptions are thrown by this method.
        /// </remarks>
        private static int? FindSupplierId(string? extractedSupplierName, IEnumerable<Supplier> suppliers)
        {
            if (string.IsNullOrWhiteSpace(extractedSupplierName))
            {
                return null;
            }

            var exact = suppliers.FirstOrDefault(s => string.Equals(s.Name, extractedSupplierName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact.Id;
            }

            var partial = suppliers
                .FirstOrDefault(s => s.Name.Contains(extractedSupplierName, StringComparison.OrdinalIgnoreCase)
                    || extractedSupplierName.Contains(s.Name, StringComparison.OrdinalIgnoreCase));

            if (partial != null)
            {
                return partial.Id;
            }

            var extractedTokens = TokenizeName(extractedSupplierName);
            if (extractedTokens.Count == 0)
            {
                return null;
            }

            return suppliers
                .Select(s => new
                {
                    SupplierId = s.Id,
                    Score = TokenizeName(s.Name).Intersect(extractedTokens, StringComparer.OrdinalIgnoreCase).Count()
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => (int?)x.SupplierId)
                .FirstOrDefault();
        }

        /// <summary>
        /// Tokenizes a supplier or product name for similarity comparisons.
        /// </summary>
        /// <param name="value">Input string to tokenize.</param>
        /// <returns>A set of normalized tokens excluding common noise words.</returns>
        /// <remarks>
        /// Expected output: deterministic token set used by fuzzy matching helpers.
        /// Possible errors: no custom exceptions are thrown by this method.
        /// </remarks>
        private static HashSet<string> TokenizeName(string value)
        {
            var noiseWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "inc", "llc", "ltd", "limited", "co", "company", "corp", "corporation", "sa", "the"
            };

            return value
                .Split([' ', '.', ',', '-', '_', '/', '\\', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => token.Length > 2 && !noiseWords.Contains(token))
                .Select(token => token.ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Maps extracted invoice items to known catalog products and records unmatched items.
        /// </summary>
        /// <param name="items">Extracted invoice items from Gemini output.</param>
        /// <param name="products">Available products used for matching.</param>
        /// <param name="supplierId">Optional supplier constraint for product matching.</param>
        /// <returns>A mapping result containing matched order lines and unmatched extracted items.</returns>
        /// <remarks>
        /// Expected output: best-effort mapping with quantity normalization and unmatched reasons.
        /// Possible errors: no custom exceptions are thrown by this method.
        /// </remarks>
        private static ExtractionMappingResult MapExtractedLinesToOrderLines(IEnumerable<GeminiExtractedInvoiceItem> items, IReadOnlyList<ProductEntity> products, int supplierId)
        {
            var result = new ExtractionMappingResult();
            var sourceProducts = supplierId > 0
                ? products.Where(p => p.SupplierId == supplierId).ToList()
                : products;

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.ProductName))
                {
                    continue;
                }

                var match = sourceProducts
                    .FirstOrDefault(p => string.Equals(p.Name, item.ProductName, StringComparison.OrdinalIgnoreCase))
                    ?? sourceProducts.FirstOrDefault(p => p.Name.Contains(item.ProductName, StringComparison.OrdinalIgnoreCase)
                        || item.ProductName.Contains(p.Name, StringComparison.OrdinalIgnoreCase))
                    ?? sourceProducts
                        .Select(p => new
                        {
                            Product = p,
                            Score = TokenizeName(p.Name).Intersect(TokenizeName(item.ProductName), StringComparer.OrdinalIgnoreCase).Count()
                        })
                        .Where(x => x.Score > 0)
                        .OrderByDescending(x => x.Score)
                        .Select(x => x.Product)
                        .FirstOrDefault();

                if (match == null)
                {
                    result.UnmatchedItems.Add(new UnmatchedExtractedItem
                    {
                        ProductName = item.ProductName.Trim(),
                        Quantity = Math.Max(item.Quantity, 1),
                        Reason = supplierId > 0
                            ? "No catalog product matched this extracted item for the selected supplier."
                            : "No catalog product matched this extracted item."
                    });
                    continue;
                }

                var existing = result.MatchedLines.FirstOrDefault(r => r.ProductId == match.Id);
                if (existing != null)
                {
                    existing.Quantity += Math.Max(item.Quantity, 1);
                    continue;
                }

                result.MatchedLines.Add(new OrderLine
                {
                    ProductId = match.Id,
                    ProductName = match.Name,
                    Cost = match.Cost,
                    Quantity = Math.Max(item.Quantity, 1)
                });
            }

            return result;
        }

        /// <summary>
        /// Filters mapped order lines to keep only products that belong to the selected supplier.
        /// </summary>
        /// <param name="mappedLines">Mapped order lines from extraction matching.</param>
        /// <param name="products">Product catalog used for supplier ownership checks.</param>
        /// <param name="supplierId">Supplier identifier used for filtering.</param>
        /// <returns>A filtered list of order lines that belong to the selected supplier.</returns>
        /// <remarks>
        /// Expected output: order lines consistent with selected supplier scope.
        /// Possible errors: no custom exceptions are thrown by this method.
        /// </remarks>
        private static List<OrderLine> RemapMatchedLinesForSupplier(IEnumerable<OrderLine> mappedLines, IReadOnlyList<ProductEntity> products, int supplierId)
        {
            return mappedLines
                .Where(l => products.Any(p => p.Id == l.ProductId && p.SupplierId == supplierId))
                .ToList();
        }

        /// <summary>
        /// Recomputes unmatched extracted items after supplier filtering is applied.
        /// </summary>
        /// <param name="mappedLines">Currently matched order lines.</param>
        /// <param name="extractedItems">Original extracted invoice items.</param>
        /// <param name="products">Product catalog used for match checks.</param>
        /// <param name="supplierId">Supplier identifier used to constrain match scope.</param>
        /// <returns>List of extracted items that remain unmatched.</returns>
        /// <remarks>
        /// Expected output: unmatched item list synchronized with current mapped lines.
        /// Possible errors: no custom exceptions are thrown by this method.
        /// </remarks>
        private static List<UnmatchedExtractedItem> RebuildUnmatchedItems(
            IReadOnlyCollection<OrderLine> mappedLines,
            IEnumerable<GeminiExtractedInvoiceItem> extractedItems,
            IReadOnlyList<ProductEntity> products,
            int supplierId)
        {
            var matchedProductIds = mappedLines.Select(l => l.ProductId).ToHashSet();
            var sourceProducts = supplierId > 0
                ? products.Where(p => p.SupplierId == supplierId).ToList()
                : products;

            var unmatched = new List<UnmatchedExtractedItem>();

            foreach (var item in extractedItems)
            {
                if (string.IsNullOrWhiteSpace(item.ProductName))
                {
                    continue;
                }

                var match = sourceProducts.FirstOrDefault(p => matchedProductIds.Contains(p.Id)
                    && (string.Equals(p.Name, item.ProductName, StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains(item.ProductName, StringComparison.OrdinalIgnoreCase)
                        || item.ProductName.Contains(p.Name, StringComparison.OrdinalIgnoreCase)
                        || TokenizeName(p.Name).Intersect(TokenizeName(item.ProductName), StringComparer.OrdinalIgnoreCase).Any()));

                if (match != null)
                {
                    continue;
                }

                unmatched.Add(new UnmatchedExtractedItem
                {
                    ProductName = item.ProductName.Trim(),
                    Quantity = Math.Max(item.Quantity, 1),
                    Reason = supplierId > 0
                        ? "Not matched to an existing product for the selected supplier."
                        : "Not matched to an existing product."
                });
            }

            return unmatched;
        }
    }
}
