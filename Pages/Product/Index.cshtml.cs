using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagement.Pages.Product
{
    public class IndexModel : PageModel
    {
        private const int PageSize = 10;
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Initializes the product listing page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for paginated product queries.</param>
        /// <remarks>
        /// Expected output: a page model ready to load product listings.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public IndexModel(ApplicationDbContext context) => _context = context;

        public IList<Data.Product> Products { get; set; } = new List<Data.Product>();
        public HashSet<int> OrderedProductIds { get; set; } = new();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string CurrentSort { get; set; } = "name_asc";
        public string NameSort { get; set; } = "name_asc";
        public string DescriptionSort { get; set; } = "description_asc";
        public string CategoriesSort { get; set; } = "categories_asc";
        public string ProviderSort { get; set; } = "provider_asc";
        public string UpcSort { get; set; } = "upc_asc";
        public string CostSort { get; set; } = "cost_asc";
        public string QuantitySort { get; set; } = "quantity_asc";
        public string LowStockThresholdSort { get; set; } = "lowstockthreshold_asc";
        public string StatusSort { get; set; } = "status_asc";
        public string DateLastPurchasedSort { get; set; } = "datelastpurchased_asc";
        public string EstimatedTimeSort { get; set; } = "estimatedtime_asc";
        public string? SearchText { get; set; }
        public string StockStatus { get; set; } = "all";
        public Dictionary<string, string?> CurrentFilterRouteValues { get; set; } = new();
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        /// <summary>
        /// Loads a paginated product list with search filters, stock-status filtering, and related supplier/category information.
        /// </summary>
        /// <param name="pageNumber">Requested page index; values below 1 are normalized to 1.</param>
        /// <param name="sortOrder">Sort key controlling the product listing order.</param>
        /// <param name="searchText">Optional smart search text filter applied to product name, description, supplier, category, and UPC.</param>
        /// <param name="stockStatus">Optional stock-status filter: all, lowstock, ordered, or discontinued.</param>
        /// <returns>A task representing asynchronous page data loading.</returns>
        /// <remarks>
        /// Expected output: <see cref="Products"/>, pagination metadata, and active filter state populated for rendering.
        /// Possible errors: data access exceptions can propagate during filtered count and list queries.
        /// </remarks>
        public async Task OnGetAsync(
            int pageNumber = 1,
            string? sortOrder = null,
            string? searchText = null,
            string? stockStatus = null)
        {
            CurrentPage = pageNumber < 1 ? 1 : pageNumber;
            CurrentSort = string.IsNullOrWhiteSpace(sortOrder) ? "name_asc" : sortOrder.ToLowerInvariant();
            SearchText = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim();
            StockStatus = string.IsNullOrWhiteSpace(stockStatus) ? "all" : stockStatus.Trim().ToLowerInvariant();
            var normalizedSearchText = SearchText?.ToLowerInvariant();

            if (StockStatus is not ("all" or "lowstock" or "ordered" or "discontinued"))
            {
                StockStatus = "all";
            }

            CurrentFilterRouteValues = new Dictionary<string, string?>
            {
                ["searchText"] = SearchText,
                ["stockStatus"] = StockStatus == "all" ? null : StockStatus
            };

            NameSort = CurrentSort == "name_asc" ? "name_desc" : "name_asc";
            DescriptionSort = CurrentSort == "description_asc" ? "description_desc" : "description_asc";
            CategoriesSort = CurrentSort == "categories_asc" ? "categories_desc" : "categories_asc";
            ProviderSort = CurrentSort == "provider_asc" ? "provider_desc" : "provider_asc";
            UpcSort = CurrentSort == "upc_asc" ? "upc_desc" : "upc_asc";
            CostSort = CurrentSort == "cost_asc" ? "cost_desc" : "cost_asc";
            QuantitySort = CurrentSort == "quantity_asc" ? "quantity_desc" : "quantity_asc";
            LowStockThresholdSort = CurrentSort == "lowstockthreshold_asc" ? "lowstockthreshold_desc" : "lowstockthreshold_asc";
            StatusSort = CurrentSort == "status_asc" ? "status_desc" : "status_asc";
            DateLastPurchasedSort = CurrentSort == "datelastpurchased_asc" ? "datelastpurchased_desc" : "datelastpurchased_asc";
            EstimatedTimeSort = CurrentSort == "estimatedtime_asc" ? "estimatedtime_desc" : "estimatedtime_asc";

            var orderedProductIdsQuery = _context.PurchaseOrderProducts
                .Where(pop => pop.PurchaseOrder.Status == PurchaseOrderStatus.InProcess)
                .Select(pop => pop.ProductId)
                .Distinct();

            var productsQuery = _context.Products
                .Include(p => p.Supplier)
                .Include(p => p.Categories)
                .AsNoTracking();

            if (normalizedSearchText is not null)
            {
                productsQuery = productsQuery.Where(p =>
                    p.Name.ToLower().Contains(normalizedSearchText) ||
                    (p.Description ?? string.Empty).ToLower().Contains(normalizedSearchText) ||
                    (p.Supplier != null && p.Supplier.Name.ToLower().Contains(normalizedSearchText)) ||
                    p.Categories.Any(c => c.Name.ToLower().Contains(normalizedSearchText)) ||
                    p.UPC.ToLower().Contains(normalizedSearchText));
            }

            productsQuery = StockStatus switch
            {
                "lowstock" => productsQuery.Where(p => !p.IsDiscontinued && p.Quantity < p.LowStockThreshold),
                "ordered" => productsQuery.Where(p => !p.IsDiscontinued && orderedProductIdsQuery.Contains(p.Id)),
                "discontinued" => productsQuery.Where(p => p.IsDiscontinued),
                _ => productsQuery
            };

            var totalCount = await productsQuery.CountAsync();
            TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)PageSize);

            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }

            productsQuery = CurrentSort switch
            {
                "name_desc" => productsQuery.OrderByDescending(p => p.Name),
                "description_asc" => productsQuery.OrderBy(p => p.Description ?? string.Empty).ThenBy(p => p.Name),
                "description_desc" => productsQuery.OrderByDescending(p => p.Description ?? string.Empty).ThenBy(p => p.Name),
                "categories_asc" => productsQuery
                    .OrderBy(p => p.Categories.OrderBy(c => c.Name).Select(c => c.Name).FirstOrDefault() ?? string.Empty)
                    .ThenBy(p => p.Name),
                "categories_desc" => productsQuery
                    .OrderByDescending(p => p.Categories.OrderBy(c => c.Name).Select(c => c.Name).FirstOrDefault() ?? string.Empty)
                    .ThenBy(p => p.Name),
                "provider_asc" => productsQuery.OrderBy(p => p.Supplier != null ? p.Supplier.Name : string.Empty).ThenBy(p => p.Name),
                "provider_desc" => productsQuery.OrderByDescending(p => p.Supplier != null ? p.Supplier.Name : string.Empty).ThenBy(p => p.Name),
                "upc_asc" => productsQuery.OrderBy(p => p.UPC).ThenBy(p => p.Name),
                "upc_desc" => productsQuery.OrderByDescending(p => p.UPC).ThenBy(p => p.Name),
                "cost_asc" => productsQuery.OrderBy(p => p.Cost).ThenBy(p => p.Name),
                "cost_desc" => productsQuery.OrderByDescending(p => p.Cost).ThenBy(p => p.Name),
                "quantity_asc" => productsQuery.OrderBy(p => p.Quantity).ThenBy(p => p.Name),
                "quantity_desc" => productsQuery.OrderByDescending(p => p.Quantity).ThenBy(p => p.Name),
                "lowstockthreshold_asc" => productsQuery.OrderBy(p => p.LowStockThreshold).ThenBy(p => p.Name),
                "lowstockthreshold_desc" => productsQuery.OrderByDescending(p => p.LowStockThreshold).ThenBy(p => p.Name),
                "status_asc" => productsQuery.OrderBy(p => p.IsDiscontinued).ThenBy(p => p.Name),
                "status_desc" => productsQuery.OrderByDescending(p => p.IsDiscontinued).ThenBy(p => p.Name),
                "datelastpurchased_asc" => productsQuery.OrderBy(p => p.DateLastPurchased ?? DateTime.MinValue).ThenBy(p => p.Name),
                "datelastpurchased_desc" => productsQuery.OrderByDescending(p => p.DateLastPurchased ?? DateTime.MinValue).ThenBy(p => p.Name),
                "estimatedtime_asc" => productsQuery.OrderBy(p => p.EstimatedTimeToReceiveWeeks ?? int.MaxValue).ThenBy(p => p.Name),
                "estimatedtime_desc" => productsQuery.OrderByDescending(p => p.EstimatedTimeToReceiveWeeks ?? int.MinValue).ThenBy(p => p.Name),
                _ => productsQuery.OrderBy(p => p.Name)
            };

            Products = await productsQuery
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var pageProductIds = Products.Select(p => p.Id).ToList();
            OrderedProductIds = await orderedProductIdsQuery
                .Where(productId => pageProductIds.Contains(productId))
                .ToHashSetAsync();
        }
    }
}
