using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace InventoryManagement.Pages.Provider
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        [BindProperty]
        public Supplier Supplier { get; set; } = new Supplier();

        /// <summary>
        /// Initializes the provider edit page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for supplier retrieval and updates.</param>
        /// <remarks>
        /// Expected output: a page model ready to process edit operations.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public EditModel(ApplicationDbContext context) => _context = context;

        /// <summary>
        /// Loads supplier data for editing.
        /// </summary>
        /// <param name="id">Supplier identifier to edit.</param>
        /// <returns>A not-found result when supplier is missing; otherwise the edit page.</returns>
        /// <remarks>
        /// Expected output: supplier values bound for editing.
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
        /// Validates and persists supplier updates.
        /// </summary>
        /// <returns>A page result when validation fails; otherwise a redirect to the provider list.</returns>
        /// <remarks>
        /// Expected output: modified supplier values saved to the database.
        /// Possible errors: database concurrency and update exceptions can propagate.
        /// </remarks>
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();
            _context.Attach(Supplier).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
