using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Pages.Category
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public Data.Category Category { get; set; } = new Data.Category();

        /// <summary>
        /// Initializes the category edit page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for category retrieval and updates.</param>
        /// <remarks>
        /// Expected output: a page model ready to process edit operations.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public EditModel(ApplicationDbContext context) => _context = context;

        /// <summary>
        /// Loads category data for editing.
        /// </summary>
        /// <param name="id">Category identifier to edit.</param>
        /// <returns>A not-found result when category is missing; otherwise the edit page.</returns>
        /// <remarks>
        /// Expected output: category values bound for editing.
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
        /// Validates and persists category updates.
        /// </summary>
        /// <returns>A page result when validation fails; otherwise a redirect to the category list.</returns>
        /// <remarks>
        /// Expected output: modified category values saved to the database.
        /// Possible errors: database concurrency and update exceptions can propagate.
        /// </remarks>
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(Category).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
