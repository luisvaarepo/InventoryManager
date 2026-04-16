using InventoryManagement.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.UserManagement;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;

    /// <summary>
    /// Initializes the user management page model with an ASP.NET Identity user manager.
    /// </summary>
    /// <param name="userManager">Identity user manager used for user and role operations.</param>
    /// <remarks>
    /// Expected output: a page model ready to query and update users.
    /// Possible errors: dependency resolution errors may occur when user manager is unavailable.
    /// </remarks>
    public IndexModel(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public IList<UserListItem> Users { get; private set; } = new List<UserListItem>();
    public IReadOnlyList<string> AvailableRoles { get; } = Roles.All;

    [TempData]
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Loads all users with their current roles for display.
    /// </summary>
    /// <returns>A task representing asynchronous page data loading.</returns>
    /// <remarks>
    /// Expected output: <see cref="Users"/> populated with identity users and primary roles.
    /// Possible errors: identity store query exceptions can propagate.
    /// </remarks>
    public async Task OnGetAsync()
    {
        await LoadUsersAsync();
    }

    /// <summary>
    /// Updates a user's email and role assignment.
    /// </summary>
    /// <param name="userId">Target user identifier.</param>
    /// <param name="email">Email address to assign to the target user.</param>
    /// <param name="role">Role name to assign after removing current roles.</param>
    /// <returns>A not-found result when user is missing, a page result on validation errors, or redirect on success.</returns>
    /// <remarks>
    /// Expected output: user email/username and role membership updated when inputs are valid.
    /// Possible errors: identity operation failures are reported to model state; store exceptions can propagate.
    /// </remarks>
    public async Task<IActionResult> OnPostUpdateAsync(string userId, string email, string role)
    {
        if (!AvailableRoles.Contains(role))
        {
            ModelState.AddModelError(string.Empty, "Invalid role selected.");
            await LoadUsersAsync();
            return Page();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmailResult = await _userManager.SetEmailAsync(user, email);
            if (!setEmailResult.Succeeded)
            {
                AddErrors(setEmailResult);
                await LoadUsersAsync();
                return Page();
            }

            var setUserNameResult = await _userManager.SetUserNameAsync(user, email);
            if (!setUserNameResult.Succeeded)
            {
                AddErrors(setUserNameResult);
                await LoadUsersAsync();
                return Page();
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var roleChanged = !currentRoles.Contains(role, StringComparer.OrdinalIgnoreCase);

        var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
        {
            AddErrors(removeResult);
            await LoadUsersAsync();
            return Page();
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, role);
        if (!addRoleResult.Succeeded)
        {
            AddErrors(addRoleResult);
            await LoadUsersAsync();
            return Page();
        }

        StatusMessage = roleChanged
            ? $"Updated {email}. Role changes require logout and login again to refresh permissions."
            : $"Updated {email}.";

        return RedirectToPage();
    }

    /// <summary>
    /// Loads users and their roles into the list used by the page UI.
    /// </summary>
    /// <returns>A task representing asynchronous user loading.</returns>
    /// <remarks>
    /// Expected output: <see cref="Users"/> contains sorted user records with role labels.
    /// Possible errors: identity query exceptions can propagate.
    /// </remarks>
    private async Task LoadUsersAsync()
    {
        Users = new List<UserListItem>();

        foreach (var user in _userManager.Users.OrderBy(u => u.Email))
        {
            var roles = await _userManager.GetRolesAsync(user);
            Users.Add(new UserListItem
            {
                Id = user.Id,
                Email = user.Email ?? user.UserName ?? string.Empty,
                Role = roles.FirstOrDefault() ?? Roles.Staff
            });
        }
    }

    /// <summary>
    /// Appends identity operation errors to the current model state.
    /// </summary>
    /// <param name="result">Identity operation result containing error details.</param>
    /// <remarks>
    /// Expected output: model state contains user-friendly identity validation errors.
    /// Possible errors: no custom exceptions are thrown by this method.
    /// </remarks>
    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    public sealed class UserListItem
    {
        public string Id { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Role { get; init; } = Roles.Staff;
    }
}
