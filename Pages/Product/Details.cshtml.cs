using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace InventoryManagement.Pages.Product
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Purpose: Initializes the product details page model with the application database context.
        /// Explanation: Stores the context instance used to query product and supplier data for display.
        /// Parameters: <paramref name="context"/> - database context used for product retrieval.
        /// Expected output: A ready-to-use page model instance for details loading.
        /// Possible errors: Dependency resolution errors can occur if the context is unavailable.
        /// </summary>
        public DetailsModel(ApplicationDbContext context) => _context = context;

        public Data.Product? Product { get; private set; }

        /// <summary>
        /// Purpose: Loads one product with provider and categories for read-only display.
        /// Explanation: Queries the requested product by identifier and includes related supplier and category links.
        /// Parameters: <paramref name="id"/> - product identifier to load.
        /// Expected output: Returns the details page with <see cref="Product"/> populated.
        /// Possible errors: Returns not found when the product does not exist; data access exceptions can propagate from EF Core.
        /// </summary>
        public async Task<IActionResult> OnGetAsync(int id)
        {
            Product = await _context.Products
                .Include(p => p.Supplier)
                .Include(p => p.Categories)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (Product == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}
