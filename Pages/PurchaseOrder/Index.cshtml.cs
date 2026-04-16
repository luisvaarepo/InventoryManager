using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagement.Pages.PurchaseOrder
{
    /// <summary>
    /// Page model for displaying and managing purchase orders.
    /// </summary>
    public class IndexModel : PageModel
    {
        private const int PageSize = 10;
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Initializes the purchase order listing page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for order list queries.</param>
        /// <remarks>
        /// Expected output: a page model ready to load paginated purchase orders.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public IndexModel(ApplicationDbContext context) => _context = context;

        public IList<InventoryManagement.Data.PurchaseOrder> PurchaseOrders { get; set; } = new List<InventoryManagement.Data.PurchaseOrder>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string CurrentSort { get; set; } = "orderdate_desc";
        public string OrderNumberSort { get; set; } = "id_asc";
        public string OrderDateSort { get; set; } = "orderdate_desc";
        public string SupplierSort { get; set; } = "supplier_asc";
        public string StatusSort { get; set; } = "status_asc";
        public string ProductsCountSort { get; set; } = "productcount_asc";
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        /// <summary>
        /// Loads a paginated purchase order list with supplier and line item data.
        /// </summary>
        /// <param name="pageNumber">Requested page index; values below 1 are normalized to 1.</param>
        /// <param name="sortOrder">Sort key controlling purchase order listing order.</param>
        /// <returns>A task representing asynchronous page data loading.</returns>
        /// <remarks>
        /// Expected output: <see cref="PurchaseOrders"/> and pagination metadata populated.
        /// Possible errors: data access exceptions can propagate during count and query operations.
        /// </remarks>
        public async Task OnGetAsync(int pageNumber = 1, string? sortOrder = null)
        {
            CurrentPage = pageNumber < 1 ? 1 : pageNumber;
            CurrentSort = string.IsNullOrWhiteSpace(sortOrder) ? "orderdate_desc" : sortOrder.ToLowerInvariant();

            OrderNumberSort = CurrentSort == "id_asc" ? "id_desc" : "id_asc";
            OrderDateSort = CurrentSort == "orderdate_asc" ? "orderdate_desc" : "orderdate_asc";
            SupplierSort = CurrentSort == "supplier_asc" ? "supplier_desc" : "supplier_asc";
            StatusSort = CurrentSort == "status_asc" ? "status_desc" : "status_asc";
            ProductsCountSort = CurrentSort == "productcount_asc" ? "productcount_desc" : "productcount_asc";

            var totalCount = await _context.PurchaseOrders.CountAsync();
            TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)PageSize);

            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }

            var purchaseOrdersQuery = _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.PurchaseOrderProducts)
                .AsNoTracking();

            purchaseOrdersQuery = CurrentSort switch
            {
                "id_asc" => purchaseOrdersQuery.OrderBy(po => po.Id),
                "id_desc" => purchaseOrdersQuery.OrderByDescending(po => po.Id),
                "orderdate_asc" => purchaseOrdersQuery.OrderBy(po => po.OrderDate).ThenBy(po => po.Id),
                "supplier_asc" => purchaseOrdersQuery.OrderBy(po => po.Supplier != null ? po.Supplier.Name : string.Empty).ThenByDescending(po => po.OrderDate),
                "supplier_desc" => purchaseOrdersQuery.OrderByDescending(po => po.Supplier != null ? po.Supplier.Name : string.Empty).ThenByDescending(po => po.OrderDate),
                "status_asc" => purchaseOrdersQuery.OrderBy(po => po.Status).ThenByDescending(po => po.OrderDate),
                "status_desc" => purchaseOrdersQuery.OrderByDescending(po => po.Status).ThenByDescending(po => po.OrderDate),
                "productcount_asc" => purchaseOrdersQuery.OrderBy(po => po.PurchaseOrderProducts.Sum(p => p.QuantityAdded)).ThenByDescending(po => po.OrderDate),
                "productcount_desc" => purchaseOrdersQuery.OrderByDescending(po => po.PurchaseOrderProducts.Sum(p => p.QuantityAdded)).ThenByDescending(po => po.OrderDate),
                _ => purchaseOrdersQuery.OrderByDescending(po => po.OrderDate).ThenByDescending(po => po.Id)
            };

            PurchaseOrders = await purchaseOrdersQuery
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
    }
}
