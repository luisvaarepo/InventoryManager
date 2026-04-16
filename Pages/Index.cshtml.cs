using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagement.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Initializes the dashboard page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for dashboard aggregates and low-stock queries.</param>
        /// <remarks>
        /// Expected output: a page model ready to load dashboard metrics.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public int ProductCount { get; set; }
        public int ProviderCount { get; set; }
        public int PurchaseOrderCount { get; set; }
        public int InProgressPurchaseOrderCount { get; set; }
        public int LowStockCount { get; set; }
        public string CurrentSort { get; set; } = "quantity_asc";
        public string NameSort { get; set; } = "name_asc";
        public string ProviderSort { get; set; } = "provider_asc";
        public string QuantitySort { get; set; } = "quantity_asc";
        public string ThresholdSort { get; set; } = "threshold_asc";
        public HashSet<int> OrderedLowStockProductIds { get; set; } = new();
        public IList<InventoryManagement.Data.Product> LowStockProducts { get; set; } = new List<InventoryManagement.Data.Product>();

        /// <summary>
        /// Loads dashboard totals and low-stock product information.
        /// </summary>
        /// <param name="sortOrder">Sort key controlling low-stock table ordering.</param>
        /// <returns>A task representing asynchronous dashboard loading.</returns>
        /// <remarks>
        /// Expected output: count metrics and low-stock lists populated for the home page.
        /// Possible errors: data access exceptions can propagate during aggregate queries.
        /// </remarks>
        public async Task OnGetAsync(string? sortOrder)
        {
            ProductCount = await _context.Products.CountAsync();
            ProviderCount = await _context.Suppliers.CountAsync();
            PurchaseOrderCount = await _context.PurchaseOrders.CountAsync();
            InProgressPurchaseOrderCount = await _context.PurchaseOrders.CountAsync(po => po.Status == PurchaseOrderStatus.InProcess);

            CurrentSort = string.IsNullOrWhiteSpace(sortOrder) ? "quantity_asc" : sortOrder.ToLowerInvariant();
            NameSort = CurrentSort == "name_asc" ? "name_desc" : "name_asc";
            ProviderSort = CurrentSort == "provider_asc" ? "provider_desc" : "provider_asc";
            QuantitySort = CurrentSort == "quantity_asc" ? "quantity_desc" : "quantity_asc";
            ThresholdSort = CurrentSort == "threshold_asc" ? "threshold_desc" : "threshold_asc";

            var lowStockQuery = _context.Products
                .Include(p => p.Supplier)
                .Where(p => !p.IsDiscontinued && p.Quantity < p.LowStockThreshold)
                .AsNoTracking();

            lowStockQuery = CurrentSort switch
            {
                "name_asc" => lowStockQuery.OrderBy(p => p.Name).ThenBy(p => p.Quantity),
                "name_desc" => lowStockQuery.OrderByDescending(p => p.Name).ThenBy(p => p.Quantity),
                "provider_asc" => lowStockQuery.OrderBy(p => p.Supplier != null ? p.Supplier.Name : string.Empty).ThenBy(p => p.Name),
                "provider_desc" => lowStockQuery.OrderByDescending(p => p.Supplier != null ? p.Supplier.Name : string.Empty).ThenBy(p => p.Name),
                "quantity_desc" => lowStockQuery.OrderByDescending(p => p.Quantity).ThenBy(p => p.Name),
                "threshold_asc" => lowStockQuery.OrderBy(p => p.LowStockThreshold).ThenBy(p => p.Name),
                "threshold_desc" => lowStockQuery.OrderByDescending(p => p.LowStockThreshold).ThenBy(p => p.Name),
                _ => lowStockQuery.OrderBy(p => p.Quantity).ThenBy(p => p.Name)
            };

            LowStockProducts = await lowStockQuery.ToListAsync();

            var lowStockProductIds = LowStockProducts.Select(p => p.Id).ToList();
            OrderedLowStockProductIds = await _context.PurchaseOrderProducts
                .Where(pop => lowStockProductIds.Contains(pop.ProductId)
                    && pop.PurchaseOrder.Status == PurchaseOrderStatus.InProcess)
                .Select(pop => pop.ProductId)
                .Distinct()
                .ToHashSetAsync();

            LowStockCount = LowStockProducts.Count;
        }
    }
}
