using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Pages.Category
{
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public Data.Category Category { get; set; } = new Data.Category();

        /// <summary>
        /// Initializes the category deletion page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for category queries and deletion.</param>
        /// <remarks>
        /// Expected output: a page model ready to load and delete categories.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public DeleteModel(ApplicationDbContext context) => _context = context;

        /// <summary>
        /// Loads category data for deletion confirmation.
        /// </summary>
        /// <param name="id">Category identifier requested for deletion.</param>
        /// <returns>A not-found result when missing; otherwise the confirmation page.</returns>
        /// <remarks>
        /// Expected output: bound category details for confirmation UI.
        /// Possible errors: data access exceptions can propagate during query execution.
        /// </remarks>
        public async Task<IActionResult> OnGetAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            Category = category;
            return Page();
        }

        /// <summary>
        /// Deletes the selected category from the catalog.
        /// </summary>
        /// <returns>A not-found result when category is missing; otherwise a redirect to the category list.</returns>
        /// <remarks>
        /// Expected output: category removed from persistence when found.
        /// Possible errors: database update exceptions can propagate during save.
        /// </remarks>
        public async Task<IActionResult> OnPostAsync()
        {
            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == Category.Id);

            if (category == null)
            {
                return NotFound();
            }

            category.Products.Clear();
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
