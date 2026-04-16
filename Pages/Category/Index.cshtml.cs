using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Pages.Category
{
    public class IndexModel : PageModel
    {
        private const int PageSize = 10;
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Initializes the category listing page model with a database context.
        /// </summary>
        /// <param name="context">Application database context used for category paging queries.</param>
        /// <remarks>
        /// Expected output: a page model ready to load paginated categories.
        /// Possible errors: dependency resolution errors may occur if context is unavailable.
        /// </remarks>
        public IndexModel(ApplicationDbContext context) => _context = context;

        public IList<Data.Category> Categories { get; set; } = new List<Data.Category>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        /// <summary>
        /// Loads a paginated category list for the requested page number.
        /// </summary>
        /// <param name="pageNumber">Requested page index; values below 1 are normalized to 1.</param>
        /// <returns>A task representing asynchronous page data loading.</returns>
        /// <remarks>
        /// Expected output: <see cref="Categories"/>, pagination metadata, and navigation flags populated.
        /// Possible errors: data access exceptions can propagate during count or list queries.
        /// </remarks>
        public async Task OnGetAsync(int pageNumber = 1)
        {
            CurrentPage = pageNumber < 1 ? 1 : pageNumber;

            var totalCount = await _context.Categories.CountAsync();
            TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)PageSize);

            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }

            Categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
    }
}
