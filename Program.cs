using InventoryManagement.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using InventoryManagement.Security;
using InventoryManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=inventory.db";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IGeminiModelCatalogService, GeminiModelCatalogService>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
});
builder.Services.AddHttpClient<IGeminiInvoiceExtractionService, GeminiInvoiceExtractionService>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnValidatePrincipal = async context =>
    {
        if (context.Principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
        var signInManager = context.HttpContext.RequestServices.GetRequiredService<SignInManager<IdentityUser>>();
        var user = await userManager.GetUserAsync(context.Principal);

        if (user is null)
        {
            return;
        }

        var externalLogins = await userManager.GetLoginsAsync(user);
        var isGoogleUser = externalLogins.Any(login => string.Equals(login.LoginProvider, "Google", StringComparison.OrdinalIgnoreCase));

        if (!isGoogleUser)
        {
            return;
        }

        var roles = await userManager.GetRolesAsync(user);
        var changed = false;

        if (!roles.Contains(Roles.Manager))
        {
            var addManagerResult = await userManager.AddToRoleAsync(user, Roles.Manager);
            changed |= addManagerResult.Succeeded;
        }

        if (roles.Contains(Roles.Staff))
        {
            var removeStaffResult = await userManager.RemoveFromRoleAsync(user, Roles.Staff);
            changed |= removeStaffResult.Succeeded;
        }

        if (changed)
        {
            context.ReplacePrincipal(await signInManager.CreateUserPrincipalAsync(user));
            context.ShouldRenew = true;
        }
    };
});

var googleAuthSection = builder.Configuration.GetSection("Authentication:Google");
var googleClientId = googleAuthSection["ClientId"];
var googleClientSecret = googleAuthSection["ClientSecret"];

if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            options.CallbackPath = googleAuthSection["CallbackPath"] ?? "/signin-google";
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.Manager, policy => policy.RequireRole(Roles.Manager));
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Product");
    options.Conventions.AuthorizeFolder("/Provider");
    options.Conventions.AuthorizeFolder("/Category");
    options.Conventions.AuthorizeFolder("/PurchaseOrder");
    options.Conventions.AuthorizeFolder("/UserManagement");
    options.Conventions.AuthorizeFolder("/Gemini");
    options.Conventions.AuthorizeFolder("/Audit", Roles.Manager);

    options.Conventions.AuthorizePage("/Product/Create", Roles.Manager);
    options.Conventions.AuthorizePage("/Product/Edit", Roles.Manager);
    options.Conventions.AuthorizePage("/Product/Delete", Roles.Manager);

    options.Conventions.AuthorizePage("/Provider/Create", Roles.Manager);
    options.Conventions.AuthorizePage("/Provider/Edit", Roles.Manager);
    options.Conventions.AuthorizePage("/Provider/Delete", Roles.Manager);

    options.Conventions.AuthorizePage("/Category/Create", Roles.Manager);
    options.Conventions.AuthorizePage("/Category/Edit", Roles.Manager);
    options.Conventions.AuthorizePage("/Category/Delete", Roles.Manager);

    options.Conventions.AuthorizePage("/PurchaseOrder/Create", Roles.Manager);
    options.Conventions.AuthorizePage("/Gemini/Settings", Roles.Manager);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
    await IdentityDataSeeder.SeedAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
