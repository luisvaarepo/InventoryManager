using InventoryManagement.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Pages.Audit
{
    public class IndexModel : PageModel
    {
        private const int PageSize = 20;
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Initializes the audit listing page model with the application database context.
        /// </summary>
        /// <param name="context">Database context used to query, filter, and sort audit records.</param>
        /// <remarks>
        /// Expected output: a page model instance ready to load audit history data.
        /// Possible errors: dependency resolution exceptions can occur when the database context is unavailable.
        /// </remarks>
        public IndexModel(ApplicationDbContext context) => _context = context;

        public IList<Data.Audit> Audits { get; set; } = new List<Data.Audit>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string CurrentSort { get; set; } = "timestamp_desc";
        public string CurrentSearch { get; set; } = string.Empty;
        public string UserSort { get; set; } = "user_asc";
        public string ActionSort { get; set; } = "action_asc";
        public string TableSort { get; set; } = "table_asc";
        public string RecordSort { get; set; } = "record_asc";
        public string TimestampSort { get; set; } = "timestamp_desc";
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        /// <summary>
        /// Loads a paginated audit list with optional search filtering and column sorting.
        /// </summary>
        /// <param name="pageNumber">Requested page index; values below 1 are normalized to 1.</param>
        /// <param name="sortOrder">Sort key used to control audit list ordering.</param>
        /// <param name="searchTerm">Optional free-text filter applied to audit fields.</param>
        /// <returns>A task that represents asynchronous loading of audit records and paging metadata.</returns>
        /// <remarks>
        /// Expected output: <see cref="Audits"/> populated with matching records plus pagination and sorting state.
        /// Possible errors: query execution exceptions can propagate from Entity Framework and the database provider.
        /// </remarks>
        public async Task OnGetAsync(int pageNumber = 1, string? sortOrder = null, string? searchTerm = null)
        {
            CurrentPage = pageNumber < 1 ? 1 : pageNumber;
            CurrentSort = string.IsNullOrWhiteSpace(sortOrder) ? "timestamp_desc" : sortOrder.ToLowerInvariant();
            CurrentSearch = searchTerm?.Trim() ?? string.Empty;

            UserSort = CurrentSort == "user_asc" ? "user_desc" : "user_asc";
            ActionSort = CurrentSort == "action_asc" ? "action_desc" : "action_asc";
            TableSort = CurrentSort == "table_asc" ? "table_desc" : "table_asc";
            RecordSort = CurrentSort == "record_asc" ? "record_desc" : "record_asc";
            TimestampSort = CurrentSort == "timestamp_asc" ? "timestamp_desc" : "timestamp_asc";

            var auditsQuery = _context.Audits.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(CurrentSearch))
            {
                var normalizedSearch = CurrentSearch.ToLowerInvariant();
                var hasRecordId = int.TryParse(CurrentSearch, out var recordId);
                var hasDate = DateTime.TryParse(CurrentSearch, out var parsedDate);
                var dateStart = parsedDate.Date;
                var dateEnd = dateStart.AddDays(1);

                auditsQuery = auditsQuery.Where(a =>
                    (a.UserId ?? string.Empty).ToLower().Contains(normalizedSearch) ||
                    (a.Action ?? string.Empty).ToLower().Contains(normalizedSearch) ||
                    (a.TableName ?? string.Empty).ToLower().Contains(normalizedSearch) ||
                    (a.Details ?? string.Empty).ToLower().Contains(normalizedSearch) ||
                    (hasRecordId && a.RecordId == recordId) ||
                    (hasDate && a.Timestamp >= dateStart && a.Timestamp < dateEnd));
            }

            auditsQuery = CurrentSort switch
            {
                "user_asc" => auditsQuery.OrderBy(a => a.UserId ?? string.Empty).ThenByDescending(a => a.Timestamp),
                "user_desc" => auditsQuery.OrderByDescending(a => a.UserId ?? string.Empty).ThenByDescending(a => a.Timestamp),
                "action_asc" => auditsQuery.OrderBy(a => a.Action ?? string.Empty).ThenByDescending(a => a.Timestamp),
                "action_desc" => auditsQuery.OrderByDescending(a => a.Action ?? string.Empty).ThenByDescending(a => a.Timestamp),
                "table_asc" => auditsQuery.OrderBy(a => a.TableName ?? string.Empty).ThenByDescending(a => a.Timestamp),
                "table_desc" => auditsQuery.OrderByDescending(a => a.TableName ?? string.Empty).ThenByDescending(a => a.Timestamp),
                "record_asc" => auditsQuery.OrderBy(a => a.RecordId ?? int.MinValue).ThenByDescending(a => a.Timestamp),
                "record_desc" => auditsQuery.OrderByDescending(a => a.RecordId ?? int.MinValue).ThenByDescending(a => a.Timestamp),
                "timestamp_asc" => auditsQuery.OrderBy(a => a.Timestamp).ThenBy(a => a.Id),
                _ => auditsQuery.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
            };

            var totalCount = await auditsQuery.CountAsync();
            TotalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)PageSize);

            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }

            Audits = await auditsQuery
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
    }
}
