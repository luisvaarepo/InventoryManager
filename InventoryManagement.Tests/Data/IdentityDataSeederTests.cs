using InventoryManagement.Data;
using InventoryManagement.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InventoryManagement.Tests.Data;

public class IdentityDataSeederTests
{
    /// <summary>
    /// Verifies that seeding creates required roles, seeded users, and baseline catalog data in an empty database.
    /// </summary>
    /// <remarks>
    /// Purpose: validate the primary end-to-end seeding path for identity and catalog entities.
    /// Parameters: none.
    /// Expected output: roles and seed users exist with expected role assignments, and baseline entity counts match seed values.
    /// Possible errors: propagates seeding or identity failures when service registration is invalid.
    /// </remarks>
    [Fact]
    public async Task SeedAsync_CreatesRolesUsersAndBaselineCatalog()
    {
        await using var scopeContext = await CreateScopeContextAsync();
        var services = scopeContext.Scope.ServiceProvider;

        await IdentityDataSeeder.SeedAsync(services);

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            Assert.True(await roleManager.RoleExistsAsync(role));
        }

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var adminUser = await userManager.FindByEmailAsync("admin@example.com");
        Assert.NotNull(adminUser);
        Assert.True(await userManager.IsInRoleAsync(adminUser!, Roles.Manager));
        Assert.False(await userManager.IsInRoleAsync(adminUser!, Roles.Staff));

        var staffUser = await userManager.FindByEmailAsync("staff@example.com");
        Assert.NotNull(staffUser);
        Assert.True(await userManager.IsInRoleAsync(staffUser!, Roles.Staff));
        Assert.False(await userManager.IsInRoleAsync(staffUser!, Roles.Manager));

        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(4, await dbContext.Suppliers.CountAsync());
        Assert.Equal(5, await dbContext.Categories.CountAsync());
        Assert.Equal(12, await dbContext.Products.CountAsync());
    }

    /// <summary>
    /// Verifies that an existing local user without any role is assigned the default Staff role during seeding.
    /// </summary>
    /// <remarks>
    /// Purpose: validate the fallback role-assignment branch for roleless accounts.
    /// Parameters: none.
    /// Expected output: the pre-existing user receives the Staff role after seeding.
    /// Possible errors: propagates identity creation errors if test user setup fails.
    /// </remarks>
    [Fact]
    public async Task SeedAsync_AssignsStaffRoleToExistingRolelessUser()
    {
        await using var scopeContext = await CreateScopeContextAsync();
        var services = scopeContext.Scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        var createResult = await userManager.CreateAsync(new IdentityUser
        {
            UserName = "roleless@example.com",
            Email = "roleless@example.com",
            EmailConfirmed = true
        }, "Qwerty123#");

        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        await IdentityDataSeeder.SeedAsync(services);

        var rolelessUser = await userManager.FindByEmailAsync("roleless@example.com");
        Assert.NotNull(rolelessUser);
        var roles = await userManager.GetRolesAsync(rolelessUser!);
        Assert.Single(roles);
        Assert.Equal(Roles.Staff, roles[0]);
    }

    /// <summary>
    /// Verifies that rerunning seeding does not duplicate baseline catalog records and repairs seeded account roles.
    /// </summary>
    /// <remarks>
    /// Purpose: validate idempotency and seeded-user role normalization across repeated runs.
    /// Parameters: none.
    /// Expected output: baseline counts remain unchanged and seeded accounts are restored to intended single roles.
    /// Possible errors: propagates identity role mutation errors when constraints fail.
    /// </remarks>
    [Fact]
    public async Task SeedAsync_IsIdempotentAndNormalizesSeedUserRoles()
    {
        await using var scopeContext = await CreateScopeContextAsync();
        var services = scopeContext.Scope.ServiceProvider;

        await IdentityDataSeeder.SeedAsync(services);

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var adminUser = await userManager.FindByEmailAsync("admin@example.com");
        Assert.NotNull(adminUser);

        var addStaffResult = await userManager.AddToRoleAsync(adminUser!, Roles.Staff);
        Assert.True(addStaffResult.Succeeded, string.Join("; ", addStaffResult.Errors.Select(e => e.Description)));

        await IdentityDataSeeder.SeedAsync(services);

        var adminRoles = await userManager.GetRolesAsync(adminUser!);
        Assert.Single(adminRoles);
        Assert.Equal(Roles.Manager, adminRoles[0]);

        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(4, await dbContext.Suppliers.CountAsync());
        Assert.Equal(5, await dbContext.Categories.CountAsync());
        Assert.Equal(12, await dbContext.Products.CountAsync());
    }

    /// <summary>
    /// Creates an isolated service scope backed by an in-memory SQLite database configured with Identity and application services.
    /// </summary>
    /// <returns>
    /// A disposable context containing the root service provider scope and open SQLite connection.
    /// </returns>
    /// <remarks>
    /// Purpose: provide deterministic integration-style unit test setup for seeding.
    /// Parameters: none.
    /// Expected output: a ready-to-use scoped provider with created schema and registered managers.
    /// Possible errors: throws if EF Core cannot create schema or if required identity services are missing.
    /// </remarks>
    private static async Task<TestScopeContext> CreateScopeContextAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        services
            .AddIdentityCore<IdentityUser>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        return new TestScopeContext(provider, scope, connection);
    }

    /// <summary>
    /// Encapsulates disposable resources used by seeder tests.
    /// </summary>
    /// <remarks>
    /// Purpose: ensure test provider and SQLite connection are disposed after each test run.
    /// Parameters: provider, scope, and connection initialized by test setup.
    /// Expected output: deterministic cleanup through asynchronous disposal.
    /// Possible errors: no custom exceptions; disposal may propagate provider or connection disposal errors.
    /// </remarks>
    private sealed class TestScopeContext : IAsyncDisposable
    {
        /// <summary>
        /// Initializes a new disposable scope context instance for test execution.
        /// </summary>
        /// <param name="provider">Root service provider that owns DI registrations for the test.</param>
        /// <param name="scope">Scoped service provider used for resolving seeding dependencies.</param>
        /// <param name="connection">Open SQLite connection backing the in-memory test database.</param>
        /// <remarks>
        /// Purpose: bind together all disposable test resources in one object.
        /// Parameters: provider, scope, and connection from test bootstrap.
        /// Expected output: initialized context with accessible `Scope` for service resolution.
        /// Possible errors: no custom exceptions are thrown by this constructor.
        /// </remarks>
        public TestScopeContext(ServiceProvider provider, IServiceScope scope, SqliteConnection connection)
        {
            _provider = provider;
            Scope = scope;
            _connection = connection;
        }

        private readonly ServiceProvider _provider;
        private readonly SqliteConnection _connection;

        public IServiceScope Scope { get; }

        /// <summary>
        /// Asynchronously disposes test scope resources in dependency-safe order.
        /// </summary>
        /// <returns>A task that completes when all resources are disposed.</returns>
        /// <remarks>
        /// Purpose: release scoped services, root provider, and SQLite connection after each test.
        /// Parameters: none.
        /// Expected output: all resources are disposed without leaks.
        /// Possible errors: disposal exceptions from the provider or connection can propagate.
        /// </remarks>
        public async ValueTask DisposeAsync()
        {
            Scope.Dispose();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
