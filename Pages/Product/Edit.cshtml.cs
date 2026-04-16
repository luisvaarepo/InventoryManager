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
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public Data.Product Product { get; set; } = new Data.Product();

        [BindProperty]
        public List<int> SelectedCategoryIds { get; set; } = new();

        public List<SelectListItem> ProviderOptions { get; set; } = new();
        public List<SelectListItem> CategoryOptions { get; set; } = new();

        /// <summary>
        /// Initializes the product edit page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for loading and updating products.</param>
        /// <remarks>
        /// Expected output: a page model ready to process product edits.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public EditModel(ApplicationDbContext context) => _context = context;

        /// <summary>
        /// Loads product, supplier options, and category options for editing.
        /// </summary>
        /// <param name="id">Product identifier to edit.</param>
        /// <returns>A not-found result when product is missing; otherwise the edit page.</returns>
        /// <remarks>
        /// Expected output: selected product, category selections, and form options populated.
        /// Possible errors: data access exceptions can propagate during query execution.
        /// </remarks>
        public async Task<IActionResult> OnGetAsync(int id)
        {
            var product = await _context.Products
                .Include(p => p.Categories)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            Product = product;
            SelectedCategoryIds = product.Categories.Select(c => c.Id).ToList();
            await LoadFormOptionsAsync();
            return Page();
        }

        /// <summary>
        /// Validates and persists product updates including category assignments.
        /// </summary>
        /// <returns>A page result on validation failure; otherwise a redirect to the product list.</returns>
        /// <remarks>
        /// Expected output: updated product values and category links saved to persistence.
        /// Possible errors: database concurrency and update exceptions can propagate.
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

            var productToUpdate = await _context.Products
                .Include(p => p.Categories)
                .FirstOrDefaultAsync(p => p.Id == Product.Id);

            if (productToUpdate == null)
            {
                return NotFound();
            }

            productToUpdate.Name = Product.Name;
            productToUpdate.Description = Product.Description;
            productToUpdate.SupplierId = Product.SupplierId;
            productToUpdate.UPC = Product.UPC;
            productToUpdate.Cost = Product.Cost;
            productToUpdate.Quantity = Product.Quantity;
            productToUpdate.LowStockThreshold = Product.LowStockThreshold;
            productToUpdate.IsDiscontinued = Product.IsDiscontinued;
            productToUpdate.DateLastPurchased = Product.DateLastPurchased;
            productToUpdate.EstimatedTimeToReceiveWeeks = Product.EstimatedTimeToReceiveWeeks;

            var selectedCategories = await _context.Categories
                .Where(c => SelectedCategoryIds.Contains(c.Id))
                .ToListAsync();

            productToUpdate.Categories.Clear();
            foreach (var category in selectedCategories)
            {
                productToUpdate.Categories.Add(category);
            }

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
