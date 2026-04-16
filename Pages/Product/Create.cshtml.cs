using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InventoryManagement.Pages.Product
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public Data.Product Product { get; set; } = new Data.Product();

        [BindProperty]
        public List<int> SelectedCategoryIds { get; set; } = new();

        public List<SelectListItem> ProviderOptions { get; set; } = new();
        public List<SelectListItem> CategoryOptions { get; set; } = new();

        /// <summary>
        /// Initializes the product creation page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for product, supplier, and category data.</param>
        /// <remarks>
        /// Expected output: a page model ready for product creation workflows.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public CreateModel(ApplicationDbContext context) => _context = context;

        /// <summary>
        /// Loads supplier and category options required for the create-product form.
        /// </summary>
        /// <returns>A task representing asynchronous form option loading.</returns>
        /// <remarks>
        /// Expected output: <see cref="ProviderOptions"/> and <see cref="CategoryOptions"/> populated for page rendering.
        /// Possible errors: query execution errors can propagate from the data provider.
        /// </remarks>
        public async Task OnGetAsync()
        {
            await LoadFormOptionsAsync();
        }

        /// <summary>
        /// Validates and saves a new product record with selected categories.
        /// </summary>
        /// <returns>A page result on validation failure; otherwise a redirect to the product list.</returns>
        /// <remarks>
        /// Expected output: product and category links persisted when model input is valid.
        /// Possible errors: database update exceptions can propagate during save.
        /// </remarks>
        public async Task<IActionResult> OnPostAsync()
        {
            if (SelectedCategoryIds.Count == 0)
            {
                ModelState.AddModelError(nameof(SelectedCategoryIds), "At least one category is required.");
            }

            if (!ModelState.IsValid)
            {
                await LoadFormOptionsAsync();
                return Page();
            }

            Product.Categories = await _context.Categories
                .Where(c => SelectedCategoryIds.Contains(c.Id))
                .ToListAsync();

            _context.Products.Add(Product);
            await _context.SaveChangesAsync();
            return RedirectToPage("Index");
        }

        /// <summary>
        /// Loads supplier and category select-list options used by product form pages.
        /// </summary>
        /// <returns>A task representing asynchronous option retrieval.</returns>
        /// <remarks>
        /// Expected output: both select lists populated and sorted for UI binding.
        /// Possible errors: data access exceptions can propagate from EF Core queries.
        /// </remarks>
        private async Task LoadFormOptionsAsync()
        {
            ProviderOptions = await _context.Suppliers
                .OrderBy(s => s.Name)
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name })
                .ToListAsync();

            CategoryOptions = await _context.Categories
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync();
        }
    }
}
