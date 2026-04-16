using System.Security.Claims;
using InventoryManagement.Pages.UserManagement;
using InventoryManagement.Security;
using InventoryManagement.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InventoryManagement.Tests.Pages.UserManagement;

public class IndexModelTests
{
    /// <summary>
    /// Verifies that updating a user email and role persists identity changes and redirects.
    /// </summary>
    /// <remarks>
    /// Purpose: validate successful user-management update workflow.
    /// Explanation: creates a staff user, updates email and role to manager, and asserts persisted role change.
    /// Parameters: none.
    /// Expected output: redirect result with updated email/username and manager role assignment.
    /// Possible errors: identity operation failures may cause page results with model-state errors.
    /// </remarks>
    [Fact]
    public async Task OnPostUpdateAsync_WithValidRole_UpdatesUserAndRedirects()
    {
        await using var scopeContext = await TestServiceScopeContext.CreateAsync();
        var services = scopeContext.Scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await EnsureRoleExistsAsync(roleManager, Roles.Staff);
        await EnsureRoleExistsAsync(roleManager, Roles.Manager);

        var user = new IdentityUser { UserName = "staff1@example.com", Email = "staff1@example.com", EmailConfirmed = true };
        var createResult = await userManager.CreateAsync(user, "Qwerty123#");
        Assert.True(createResult.Succeeded);
        await userManager.AddToRoleAsync(user, Roles.Staff);

        var model = new IndexModel(userManager);
        SetPageUser(model, user.Id, Roles.Manager);

        var result = await model.OnPostUpdateAsync(user.Id, "manager1@example.com", Roles.Manager);

        Assert.IsType<RedirectToPageResult>(result);

        var updatedUser = await userManager.FindByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("manager1@example.com", updatedUser!.Email);
        Assert.Equal("manager1@example.com", updatedUser.UserName);

        var roles = await userManager.GetRolesAsync(updatedUser);
        Assert.Single(roles);
        Assert.Equal(Roles.Manager, roles[0]);
    }

    /// <summary>
    /// Verifies that deleting the last manager is blocked with a page result and a protection message.
    /// </summary>
    /// <remarks>
    /// Purpose: validate manager-delete safety rule.
    /// Explanation: attempts deleting the only manager while acting as a manager principal with different user id.
    /// Parameters: none.
    /// Expected output: page result with protection error and target manager remains in store.
    /// Possible errors: identity query failures can propagate from user manager operations.
    /// </remarks>
    [Fact]
    public async Task OnPostDeleteAsync_ForLastManager_ReturnsPageAndBlocksDeletion()
    {
        await using var scopeContext = await TestServiceScopeContext.CreateAsync();
        var services = scopeContext.Scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await EnsureRoleExistsAsync(roleManager, Roles.Manager);

        var targetManager = new IdentityUser { UserName = "manager@example.com", Email = "manager@example.com", EmailConfirmed = true };
        var createResult = await userManager.CreateAsync(targetManager, "Qwerty123#");
        Assert.True(createResult.Succeeded);
        await userManager.AddToRoleAsync(targetManager, Roles.Manager);

        var model = new IndexModel(userManager);
        SetPageUser(model, "other-manager-id", Roles.Manager);

        var result = await model.OnPostDeleteAsync(targetManager.Id);

        Assert.IsType<PageResult>(result);
        Assert.Contains(model.ModelState[string.Empty]!.Errors, e => e.ErrorMessage.Contains("last manager"));
        Assert.NotNull(await userManager.FindByIdAsync(targetManager.Id));
    }

    /// <summary>
    /// Verifies that a manager can delete a non-manager user and receives a success redirect.
    /// </summary>
    /// <remarks>
    /// Purpose: validate successful user deletion path.
    /// Explanation: creates manager and staff users, then deletes staff user with manager principal.
    /// Parameters: none.
    /// Expected output: redirect result and removed target user from identity store.
    /// Possible errors: identity delete failures may surface as page results with model-state errors.
    /// </remarks>
    [Fact]
    public async Task OnPostDeleteAsync_ForNonManagerUser_DeletesAndRedirects()
    {
        await using var scopeContext = await TestServiceScopeContext.CreateAsync();
        var services = scopeContext.Scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await EnsureRoleExistsAsync(roleManager, Roles.Manager);
        await EnsureRoleExistsAsync(roleManager, Roles.Staff);

        var manager = new IdentityUser { UserName = "admin@example.com", Email = "admin@example.com", EmailConfirmed = true };
        var staff = new IdentityUser { UserName = "staff@example.com", Email = "staff@example.com", EmailConfirmed = true };

        Assert.True((await userManager.CreateAsync(manager, "Qwerty123#")).Succeeded);
        Assert.True((await userManager.CreateAsync(staff, "Qwerty123#")).Succeeded);
        await userManager.AddToRoleAsync(manager, Roles.Manager);
        await userManager.AddToRoleAsync(staff, Roles.Staff);

        var model = new IndexModel(userManager);
        SetPageUser(model, manager.Id, Roles.Manager);

        var result = await model.OnPostDeleteAsync(staff.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(await userManager.FindByIdAsync(staff.Id));
    }

    /// <summary>
    /// Ensures a role exists for identity operations used in tests.
    /// </summary>
    /// <param name="roleManager">Role manager used to create roles.</param>
    /// <param name="role">Role name to ensure.</param>
    /// <returns>A task representing asynchronous role validation/creation.</returns>
    /// <remarks>
    /// Purpose: guarantee role prerequisites for user-management page handlers.
    /// Explanation: checks existence first and creates role when missing.
    /// Parameters: role manager dependency and target role string.
    /// Expected output: requested role exists in identity store.
    /// Possible errors: identity store failures can propagate from role creation.
    /// </remarks>
    private static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole> roleManager, string role)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(role));
            Assert.True(result.Succeeded);
        }
    }

    /// <summary>
    /// Assigns an authenticated principal to a page model for role-based handler testing.
    /// </summary>
    /// <param name="model">Page model receiving the test user context.</param>
    /// <param name="userId">Name identifier claim for the simulated current user.</param>
    /// <param name="role">Role claim used for authorization checks.</param>
    /// <remarks>
    /// Purpose: provide deterministic request-user context for page handler tests.
    /// Explanation: creates a default HTTP context with claims and assigns it to page context.
    /// Parameters: target model, user id, and role string.
    /// Expected output: model handlers evaluate authorization and current user id consistently.
    /// Possible errors: no custom exceptions are thrown by this helper.
    /// </remarks>
    private static void SetPageUser(PageModel model, string userId, string role)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        ],
        authenticationType: "TestAuth");

        model.PageContext = new PageContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
