using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace InventoryManagement.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        /// <summary>
        /// Handles error page GET requests by setting the current request identifier.
        /// </summary>
        /// <remarks>
        /// Expected output: <see cref="RequestId"/> populated from current activity or HTTP trace identifier.
        /// Possible errors: no custom exceptions are thrown by this handler.
        /// </remarks>
        public void OnGet()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        }
    }

}
