using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

namespace InventoryManagement.Pages.Provider
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        [BindProperty]
        public Supplier Provider { get; set; } = new Supplier();

        /// <summary>
        /// Initializes the provider creation page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for supplier persistence.</param>
        /// <remarks>
        /// Expected output: a page model ready to process create requests.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public CreateModel(ApplicationDbContext context) => _context = context;

        /// <summary>
        /// Handles the initial GET request for the provider creation page.
        /// </summary>
        /// <remarks>
        /// Expected output: page state is prepared for rendering.
        /// Possible errors: no custom errors are produced by this handler.
        /// </remarks>
        public void OnGet() { }

        /// <summary>
        /// Validates and saves a new supplier record.
        /// </summary>
        /// <returns>A page result when validation fails; otherwise a redirect to the provider list.</returns>
        /// <remarks>
        /// Expected output: a persisted supplier when model state is valid.
        /// Possible errors: database update exceptions can propagate during save.
        /// </remarks>
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();
            _context.Suppliers.Add(Provider);
            await _context.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
