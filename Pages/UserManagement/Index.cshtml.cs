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
    /// Deletes a target user when the current signed-in user is a manager and the delete request is safe.
    /// </summary>
    /// <param name="userId">Identifier of the user selected for deletion.</param>
    /// <returns>A redirect on success, a page result when the delete is blocked, or not found when the user does not exist.</returns>
    /// <remarks>
    /// Expected output: the selected user is removed unless the request targets the current user or the last manager account.
    /// Possible errors: unauthorized access returns a forbid result; identity delete failures are reported to model state; store exceptions can propagate.
    /// </remarks>
    public async Task<IActionResult> OnPostDeleteAsync(string userId)
    {
        if (!User.IsInRole(Roles.Manager))
        {
            return Forbid();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User);
        if (string.Equals(user.Id, currentUserId, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Managers can delete other users, but they cannot delete their own account here.");
            await LoadUsersAsync();
            return Page();
        }

        var deleteProtection = await GetDeleteProtectionReasonAsync(user, currentUserId);
        if (deleteProtection is not null)
        {
            ModelState.AddModelError(string.Empty, deleteProtection);
            await LoadUsersAsync();
            return Page();
        }

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            AddErrors(deleteResult);
            await LoadUsersAsync();
            return Page();
        }

        StatusMessage = $"Deleted {user.Email ?? user.UserName ?? "the selected user"}.";
        return RedirectToPage();
    }

    /// <summary>
    /// Loads users and their roles into the list used by the page UI.
    /// </summary>
    /// <returns>A task representing asynchronous user loading.</returns>
    /// <remarks>
    /// Expected output: <see cref="Users"/> contains sorted user records with role labels and delete-state metadata.
    /// Possible errors: identity query exceptions can propagate.
    /// </remarks>
    private async Task LoadUsersAsync()
    {
        Users = new List<UserListItem>();

        var currentUserId = _userManager.GetUserId(User);
        var managerCount = await _userManager.GetUsersInRoleAsync(Roles.Manager);
        var remainingManagerCount = managerCount.Count;

        foreach (var user in _userManager.Users.OrderBy(u => u.Email))
        {
            var roles = await _userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? Roles.Staff;
            var isCurrentUser = string.Equals(user.Id, currentUserId, StringComparison.Ordinal);
            var isManager = roles.Contains(Roles.Manager, StringComparer.OrdinalIgnoreCase);
            var deleteBlockedReason = GetDeleteBlockedReason(isCurrentUser, isManager, remainingManagerCount);

            Users.Add(new UserListItem
            {
                Id = user.Id,
                Email = user.Email ?? user.UserName ?? string.Empty,
                Role = primaryRole,
                CanDelete = User.IsInRole(Roles.Manager) && deleteBlockedReason is null,
                DeleteBlockedReason = deleteBlockedReason
            });
        }
    }

    /// <summary>
    /// Computes whether a target user is protected from deletion in the current request context.
    /// </summary>
    /// <param name="user">User being evaluated for deletion.</param>
    /// <param name="currentUserId">Current signed-in user identifier.</param>
    /// <returns>The reason the user cannot be deleted, or <see langword="null"/> when deletion is allowed.</returns>
    /// <remarks>
    /// Expected output: a user-friendly protection message for self-delete or last-manager delete attempts.
    /// Possible errors: identity role queries can propagate store exceptions.
    /// </remarks>
    private async Task<string?> GetDeleteProtectionReasonAsync(IdentityUser user, string? currentUserId)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var managerCount = (await _userManager.GetUsersInRoleAsync(Roles.Manager)).Count;
        var isCurrentUser = string.Equals(user.Id, currentUserId, StringComparison.Ordinal);
        var isManager = roles.Contains(Roles.Manager, StringComparer.OrdinalIgnoreCase);

        return GetDeleteBlockedReason(isCurrentUser, isManager, managerCount);
    }

    /// <summary>
    /// Returns the delete restriction message for the supplied user state.
    /// </summary>
    /// <param name="isCurrentUser">Indicates whether the candidate user is the current signed-in user.</param>
    /// <param name="isManager">Indicates whether the candidate user currently belongs to the manager role.</param>
    /// <param name="managerCount">Current number of manager-role users in the system.</param>
    /// <returns>A restriction message when deletion must be blocked, or <see langword="null"/> when deletion is safe.</returns>
    /// <remarks>
    /// Expected output: a consistent rule evaluation for delete availability in both UI and postback flows.
    /// Possible errors: no custom exceptions are thrown by this method.
    /// </remarks>
    private static string? GetDeleteBlockedReason(bool isCurrentUser, bool isManager, int managerCount)
    {
        if (isCurrentUser)
        {
            return "You cannot delete your own account from this page.";
        }

        if (isManager && managerCount <= 1)
        {
            return "You cannot delete the last manager user.";
        }

        return null;
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
        public bool CanDelete { get; init; }
        public string? DeleteBlockedReason { get; init; }
    }
}
