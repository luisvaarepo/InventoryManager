using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Category
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public Data.Category Category { get; set; } = new Data.Category();

        /// <summary>
        /// Initializes the category creation page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for category persistence.</param>
        /// <remarks>
        /// Expected output: a page model ready to process create requests.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public CreateModel(ApplicationDbContext context) => _context = context;

        /// <summary>
        /// Handles the initial GET request for the category creation page.
        /// </summary>
        /// <remarks>
        /// Expected output: page state is prepared for rendering.
        /// Possible errors: no custom errors are produced by this handler.
        /// </remarks>
        public void OnGet() { }

        /// <summary>
        /// Validates and saves a new category record.
        /// </summary>
        /// <returns>A page result when validation fails; otherwise a redirect to the category list.</returns>
        /// <remarks>
        /// Expected output: a persisted category when model state is valid.
        /// Possible errors: database update exceptions can propagate during save.
        /// </remarks>
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Categories.Add(Category);
            await _context.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
