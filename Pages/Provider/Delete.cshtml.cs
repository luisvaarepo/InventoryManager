using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagement.Pages.Provider
{
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        [BindProperty]
        public Supplier Supplier { get; set; } = new Supplier();

        /// <summary>
        /// Initializes the provider deletion page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for supplier queries and deletion.</param>
        /// <remarks>
        /// Expected output: a page model ready to load and delete suppliers.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public DeleteModel(ApplicationDbContext context) => _context = context;

        /// <summary>
        /// Loads supplier data for deletion confirmation.
        /// </summary>
        /// <param name="id">Supplier identifier requested for deletion.</param>
        /// <returns>A not-found result when missing; otherwise the confirmation page.</returns>
        /// <remarks>
        /// Expected output: bound supplier details for confirmation UI.
        /// Possible errors: data access exceptions can propagate during query execution.
        /// </remarks>
        public async Task<IActionResult> OnGetAsync(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();
            Supplier = supplier;
            return Page();
        }

        /// <summary>
        /// Deletes the selected supplier and clears supplier links from related purchase orders.
        /// </summary>
        /// <returns>A not-found result when supplier is missing; otherwise a redirect to the provider list.</returns>
        /// <remarks>
        /// Expected output: supplier removed and related purchase orders detached from that supplier.
        /// Possible errors: database update exceptions can propagate during save.
        /// </remarks>
        public async Task<IActionResult> OnPostAsync()
        {
            var supplier = await _context.Suppliers.FindAsync(Supplier.Id);
            if (supplier == null) return NotFound();

            var purchaseOrders = await _context.PurchaseOrders
                .Where(po => po.SupplierId == supplier.Id)
                .ToListAsync();

            foreach (var purchaseOrder in purchaseOrders)
            {
                purchaseOrder.SupplierId = null;
            }

            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
