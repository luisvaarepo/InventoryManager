using InventoryManagement.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManagement.Tests.TestInfrastructure;

/// <summary>
/// Provides an isolated service scope backed by an in-memory SQLite database for test execution.
/// </summary>
/// <remarks>
/// Purpose: centralize reusable test bootstrapping for EF Core and ASP.NET Identity dependencies.
/// Explanation: creates a root service provider, scoped services, and an open SQLite in-memory connection.
/// Parameters: none.
/// Expected output: disposable context with initialized schema and resolvable test services.
/// Possible errors: throws when EF Core schema creation or service resolution fails.
/// </remarks>
public sealed class TestServiceScopeContext : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly SqliteConnection _connection;

    private TestServiceScopeContext(ServiceProvider provider, IServiceScope scope, SqliteConnection connection)
    {
        _provider = provider;
        Scope = scope;
        _connection = connection;
    }

    public IServiceScope Scope { get; }

    /// <summary>
    /// Creates an initialized service scope configured with ApplicationDbContext and ASP.NET Identity stores.
    /// </summary>
    /// <returns>A task that resolves to an initialized disposable test scope context.</returns>
    /// <remarks>
    /// Purpose: build deterministic test infrastructure for page model and seeding unit tests.
    /// Explanation: configures DI services, opens SQLite in-memory connection, and ensures database schema exists.
    /// Parameters: none.
    /// Expected output: ready-to-use service scope for resolving DbContext, UserManager, and related services.
    /// Possible errors: throws on database initialization failures or invalid DI registrations.
    /// </remarks>
    public static async Task<TestServiceScopeContext> CreateAsync()
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

        return new TestServiceScopeContext(provider, scope, connection);
    }

    /// <summary>
    /// Disposes scoped services, provider, and connection resources allocated for tests.
    /// </summary>
    /// <returns>A task that completes when all resources are released.</returns>
    /// <remarks>
    /// Purpose: guarantee cleanup of in-memory database and dependency injection resources.
    /// Explanation: disposes test scope first, then root provider and SQLite connection.
    /// Parameters: none.
    /// Expected output: no leaked resources after each test.
    /// Possible errors: disposal exceptions may propagate from provider or connection disposal.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        Scope.Dispose();
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
