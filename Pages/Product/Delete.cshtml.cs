using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace InventoryManagement.Pages.Product
{
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        [BindProperty]
        public Data.Product Product { get; set; } = new Data.Product();

        /// <summary>
        /// Initializes the product deletion page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for product lookups and deletion.</param>
        /// <remarks>
        /// Expected output: a page model ready to confirm and process deletions.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public DeleteModel(ApplicationDbContext context) => _context = context;

        /// <summary>
        /// Loads product data for deletion confirmation.
        /// </summary>
        /// <param name="id">Product identifier requested for deletion.</param>
        /// <returns>A not-found result when missing; otherwise the confirmation page.</returns>
        /// <remarks>
        /// Expected output: bound product values for delete confirmation.
        /// Possible errors: data access exceptions can propagate during query execution.
        /// </remarks>
        public async Task<IActionResult> OnGetAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            Product = product;
            return Page();
        }

        /// <summary>
        /// Deletes the selected product from the catalog.
        /// </summary>
        /// <returns>A not-found result when product is missing; otherwise a redirect to the product list.</returns>
        /// <remarks>
        /// Expected output: product removed from persistence when found.
        /// Possible errors: database update exceptions can propagate during save.
        /// </remarks>
        public async Task<IActionResult> OnPostAsync()
        {
            var product = await _context.Products.FindAsync(Product.Id);
            if (product == null) return NotFound();
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
