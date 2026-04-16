using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagement.Pages.PurchaseOrder
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Initializes the purchase order details page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for details retrieval.</param>
        /// <remarks>
        /// Expected output: a page model ready to load a purchase order and related entities.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public DetailsModel(ApplicationDbContext context) => _context = context;

        [TempData]
        public string? StatusMessage { get; set; }

        public InventoryManagement.Data.PurchaseOrder? PurchaseOrder { get; private set; }

        public int TotalQuantity => PurchaseOrder?.PurchaseOrderProducts?.Sum(p => p.QuantityAdded) ?? 0;

        /// <summary>
        /// Loads a single purchase order and related supplier and product details.
        /// </summary>
        /// <param name="id">Purchase order identifier to retrieve.</param>
        /// <returns>A not-found result when missing; otherwise the details page.</returns>
        /// <remarks>
        /// Expected output: <see cref="PurchaseOrder"/> populated with related data for display.
        /// Possible errors: data access exceptions can propagate during query execution.
        /// </remarks>
        public async Task<IActionResult> OnGetAsync(int id)
        {
            PurchaseOrder = await _context.PurchaseOrders
                .Include(po => po.Supplier)
                .Include(po => po.PurchaseOrderProducts)
                .ThenInclude(pop => pop.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(po => po.Id == id);

            if (PurchaseOrder == null)
            {
                return NotFound();
            }

            return Page();
        }

        /// <summary>
        /// Completes a purchase order and optionally applies ordered quantities to product inventory.
        /// </summary>
        /// <param name="id">Purchase order identifier to complete.</param>
        /// <param name="addItemsToInventory">Indicates whether line quantities should be added to product inventory.</param>
        /// <returns>A redirect to the same details page after processing.</returns>
        /// <remarks>
        /// Expected output: purchase order status updated to completed and optional inventory updates persisted.
        /// Possible errors: data access exceptions can propagate during retrieval and save operations.
        /// </remarks>
        public async Task<IActionResult> OnPostCompleteAsync(int id, bool addItemsToInventory)
        {
            var order = await _context.PurchaseOrders
                .Include(po => po.PurchaseOrderProducts)
                .FirstOrDefaultAsync(po => po.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            if (order.Status == PurchaseOrderStatus.Completed)
            {
                StatusMessage = "Purchase order is already completed.";
                return RedirectToPage(new { id });
            }

            if (addItemsToInventory && order.PurchaseOrderProducts.Count > 0)
            {
                var productIds = order.PurchaseOrderProducts.Select(pop => pop.ProductId).ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                foreach (var line in order.PurchaseOrderProducts)
                {
                    if (products.TryGetValue(line.ProductId, out var product))
                    {
                        product.Quantity += line.QuantityAdded;
                    }
                }
            }

            order.Status = PurchaseOrderStatus.Completed;
            await _context.SaveChangesAsync();

            StatusMessage = addItemsToInventory
                ? "Purchase order completed and inventory quantities were updated."
                : "Purchase order completed without updating inventory quantities.";

            return RedirectToPage(new { id });
        }
    }
}
