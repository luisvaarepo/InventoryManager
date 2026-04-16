using InventoryManagement.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Data;

public static class IdentityDataSeeder
{
    private const string SeedUserPassword = "Qwerty123#";
    private const string AdminEmail = "admin@example.com";
    private const string StaffEmail = "staff@example.com";

    /// <summary>
    /// Seeds identity roles, assigns default staff roles, creates test accounts, and inserts baseline supplier, category, and product records when missing.
    /// </summary>
    /// <param name="serviceProvider">Service provider used to resolve identity managers and application data context.</param>
    /// <returns>A task representing the asynchronous seed operation.</returns>
    /// <remarks>
    /// Expected output: required roles exist, test accounts are available, and initial catalog data is present in an empty database.
    /// Possible errors: propagates data access and identity management exceptions from EF Core and ASP.NET Identity.
    /// </remarks>
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureSeedUserAsync(userManager, AdminEmail, Roles.Manager);
        await EnsureSeedUserAsync(userManager, StaffEmail, Roles.Staff);

        foreach (var user in userManager.Users)
        {
            var roles = await userManager.GetRolesAsync(user);
            if (roles.Count == 0)
            {
                await userManager.AddToRoleAsync(user, Roles.Staff);
            }
        }

        if (!await dbContext.Suppliers.AnyAsync())
        {
            dbContext.Suppliers.AddRange(
                new Supplier { Name = "Northwind Supply", ContactInfo = "northwind@example.com" },
                new Supplier { Name = "Blue Ocean Traders", ContactInfo = "blueocean@example.com" },
                new Supplier { Name = "Summit Wholesale", ContactInfo = "summit@example.com" },
                new Supplier { Name = "Urban Goods Co.", ContactInfo = "urban@example.com" }
            );

            await dbContext.SaveChangesAsync();
        }

        if (!await dbContext.Categories.AnyAsync())
        {
            dbContext.Categories.AddRange(
                new Category { Name = "Office", Description = "Office supplies and stationery" },
                new Category { Name = "Electronics", Description = "Electronic devices and accessories" },
                new Category { Name = "Warehouse", Description = "Warehouse consumables and logistics items" },
                new Category { Name = "Safety", Description = "Safety and hygiene products" },
                new Category { Name = "Accessories", Description = "General accessories and peripherals" }
            );

            await dbContext.SaveChangesAsync();
        }

        if (!await dbContext.Products.AnyAsync())
        {
            var supplierIds = await dbContext.Suppliers
                .ToDictionaryAsync(s => s.Name, s => s.Id);

            var categories = await dbContext.Categories
                .ToDictionaryAsync(c => c.Name, c => c);

            dbContext.Products.AddRange(
                new Product
                {
                    Name = "Office Paper A4",
                    Description = "500-sheet ream",
                    UPC = "100000000001",
                    SupplierId = supplierIds["Northwind Supply"],
                    Cost = 6.49m,
                    Quantity = 120,
                    LowStockThreshold = 25,
                    DateLastPurchased = DateTime.UtcNow.AddDays(-8),
                    EstimatedTimeToReceiveWeeks = 1,
                    IsDiscontinued = false,
                    Categories = new List<Category> { categories["Office"] }
                },
                new Product
                {
                    Name = "Ballpoint Pens - Blue (Pack of 20)",
                    Description = "Smooth writing pens",
                    UPC = "100000000002",
                    SupplierId = supplierIds["Northwind Supply"],
                    Cost = 4.99m,
                    Quantity = 8,
                    LowStockThreshold = 20,
                    DateLastPurchased = DateTime.UtcNow.AddDays(-21),
                    EstimatedTimeToReceiveWeeks = 2,
                    IsDiscontinued = false,
                    Categories = new List<Category> { categories["Office"] }
                },
                new Product
                {
                    Name = "Wireless Keyboard",
                    Description = "2.4GHz compact keyboard",
                    UPC = "100000000003",
                    SupplierId = supplierIds["Blue Ocean Traders"],
                    Cost = 18.75m,
                    Quantity = 45,
                    LowStockThreshold = 10,
                    DateLastPurchased = DateTime.UtcNow.AddDays(-14),
                    EstimatedTimeToReceiveWeeks = 2,
                    IsDiscontinued = false,
                    Categories = new List<Category> { categories["Electronics"], categories["Accessories"] }
                },
                new Product
                {
                    Name = "USB-C Cable 1m",
                    Description = "Fast charging cable",
                    UPC = "100000000004",
                    SupplierId = supplierIds["Blue Ocean Traders"],
                    Cost = 3.10m,
                    Quantity = 5,
                    LowStockThreshold = 15,
                    DateLastPurchased = DateTime.UtcNow.AddDays(-30),
                    EstimatedTimeToReceiveWeeks = 1,
                    IsDiscontinued = false,
                    Categories = new List<Category> { categories["Electronics"], categories["Accessories"] }
                },
                new Product
                {
                    Name = "Packing Tape",
                    Description = "Heavy-duty clear tape",
                    UPC = "100000000005",
                    SupplierId = supplierIds["Summit Wholesale"],
                    Cost = 2.25m,
                    Quantity = 90,
                    LowStockThreshold = 18,
                    DateLastPurchased = DateTime.UtcNow.AddDays(-6),
                    EstimatedTimeToReceiveWeeks = 1,
                    IsDiscontinued = false,
                    Categories = new List<Category> { categories["Warehouse"] }
                },
                new Product
                {
                    Name = "Shipping Labels",
                    Description = "Thermal labels, 4x6",
                    UPC = "100000000006",
                    SupplierId = supplierIds["Summit Wholesale"],
                    Cost = 11.40m,
                    Quantity = 12,
                    LowStockThreshold = 30,
                    DateLastPurchased = DateTime.UtcNow.AddDays(-27),
                    EstimatedTimeToReceiveWeeks = 2,
                    IsDiscontinued = false,
                    Categories = new List<Category> { categories["Warehouse"], categories["Office"] }
                },
                new Product
                {
                    Name = "Stapler - Classic",
                    Description = "Metal body stapler",
                    UPC = "100000000007",
                    SupplierId = supplierIds["Urban Goods Co."],
                    Cost = 7.99m,
                    Quantity = 0,
                    LowStockThreshold = 10,
                    DateLastPurchased = DateTime.UtcNow.AddMonths(-4),
                    EstimatedTimeToReceiveWeeks = 0,
                    IsDiscontinued = true,
                    Categories = new List<Category> { categories["Office"] }
                },
                new Product
                {
                    Name = "Desk Organizer",
                    Description = "Multi-compartment organizer",
                    UPC = "100000000008",
                    SupplierId = supplierIds["Urban Goods Co."],
                    Cost = 9.55m,
                    Quantity = 3,
                    LowStockThreshold = 12,
                    DateLastPurchased = DateTime.UtcNow.AddDays(-19),
                    EstimatedTimeToReceiveWeeks = 3,
                    IsDiscontinued = false,
                    Categories = new List<Category> { categories["Office"], categories["Accessories"] }
                },
                new Product
                {
                    Name = "Thermal Receipt Printer",
                    Description = "Compact POS printer",
                    UPC = "100000000009",
                    SupplierId = supplierIds["Blue Ocean Traders"],
                    Cost = 79.00m,
                    Quantity = 16,
                    LowStockThreshold = 4,
                    DateLastPurchased = DateTime.UtcNow.AddDays(-12),
                    EstimatedTimeToReceiveWeeks = 2,
                    IsDiscontinued = false,
                    Categories = new List<Category> { categories["Electronics"], categories["Warehouse"] }
                },
                new Product
                {
                    Name = "Legacy Barcode Scanner",
                    Description = "1D scanner (legacy model)",
                    UPC = "100000000010",
                    SupplierId = supplierIds["Northwind Supply"],
                    Cost = 24.00m,
                    Quantity = 0,
                    LowStockThreshold = 5,
                    DateLastPurchased = DateTime.UtcNow.AddMonths(-8),
                    EstimatedTimeToReceiveWeeks = 0,
                    IsDiscontinued = true,
                    Categories = new List<Category> { categories["Electronics"] }
                },
                new Product
                {
                    Name = "Warehouse Gloves",
                    Description = "Anti-slip gloves, pair",
                    UPC = "100000000011",
                    SupplierId = supplierIds["Summit Wholesale"],
                    Cost = 1.95m,
                    Quantity = 68,
                    LowStockThreshold = 20,
                    DateLastPurchased = DateTime.UtcNow.AddDays(-5),
                    EstimatedTimeToReceiveWeeks = 1,
                    IsDiscontinued = false,
                    Categories = new List<Category> { categories["Safety"], categories["Warehouse"] }
                },
                new Product
                {
                    Name = "Hand Sanitizer 250ml",
                    Description = "Alcohol-based sanitizer",
                    UPC = "100000000012",
                    SupplierId = supplierIds["Urban Goods Co."],
                    Cost = 2.80m,
                    Quantity = 9,
                    LowStockThreshold = 24,
                    DateLastPurchased = DateTime.UtcNow.AddDays(-16),
                    EstimatedTimeToReceiveWeeks = 2,
                    IsDiscontinued = false,
                    Categories = new List<Category> { categories["Safety"] }
                }
            );

            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Ensures that a seeded local account exists and is assigned only to the requested application role.
    /// </summary>
    /// <param name="userManager">Identity user manager used to create users and manage their roles.</param>
    /// <param name="email">Email address and username of the seeded local account.</param>
    /// <param name="requiredRole">Application role that the seeded account must have.</param>
    /// <returns>A task representing the asynchronous identity seed operation for the requested user.</returns>
    /// <remarks>
    /// Expected output: the requested local user exists and belongs to the intended role.
    /// Possible errors: throws <see cref="InvalidOperationException"/> when user creation or role assignment fails.
    /// </remarks>
    private static async Task EnsureSeedUserAsync(UserManager<IdentityUser> userManager, string email, string requiredRole)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, SeedUserPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create seeded user '{email}': {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        foreach (var role in Roles.All.Where(role => role != requiredRole))
        {
            if (await userManager.IsInRoleAsync(user, role))
            {
                var removeRoleResult = await userManager.RemoveFromRoleAsync(user, role);
                if (!removeRoleResult.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to remove role '{role}' from seeded user '{email}': {string.Join("; ", removeRoleResult.Errors.Select(e => e.Description))}");
                }
            }
        }

        if (!await userManager.IsInRoleAsync(user, requiredRole))
        {
            var addRoleResult = await userManager.AddToRoleAsync(user, requiredRole);
            if (!addRoleResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to add role '{requiredRole}' to seeded user '{email}': {string.Join("; ", addRoleResult.Errors.Select(e => e.Description))}");
            }
        }
    }
}
