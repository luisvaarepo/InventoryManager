using InventoryManagement.Data;
using InventoryManagement.Pages.Product;
using InventoryManagement.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InventoryManagement.Tests.Pages.Product;

public class CreateModelTests
{
    /// <summary>
    /// Verifies that posting a valid product with selected categories persists the product and redirects to index.
    /// </summary>
    /// <remarks>
    /// Purpose: validate successful product creation workflow.
    /// Explanation: seeds supplier/category records, submits valid model input, and asserts saved category links.
    /// Parameters: none.
    /// Expected output: redirect result and one persisted product with expected category assignment.
    /// Possible errors: propagates data access failures if test setup cannot persist seed data.
    /// </remarks>
    [Fact]
    public async Task OnPostAsync_WithValidInput_SavesProductAndRedirects()
    {
        await using var scopeContext = await TestServiceScopeContext.CreateAsync();
        var services = scopeContext.Scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        var supplier = new Supplier { Name = "Supplier A" };
        var categoryA = new Category { Name = "Office" };
        var categoryB = new Category { Name = "Electronics" };
        dbContext.Suppliers.Add(supplier);
        dbContext.Categories.AddRange(categoryA, categoryB);
        await dbContext.SaveChangesAsync();

        var model = new CreateModel(dbContext)
        {
            Product = new InventoryManagement.Data.Product
            {
                Name = "Printer Ink",
                UPC = "100000000099",
                SupplierId = supplier.Id,
                Cost = 24.50m,
                Quantity = 15,
                LowStockThreshold = 5
            },
            SelectedCategoryIds = [categoryA.Id, categoryB.Id]
        };

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Index", redirect.PageName);

        var savedProduct = await dbContext.Products
            .Include(p => p.Categories)
            .SingleAsync(p => p.UPC == "100000000099");

        Assert.Equal("Printer Ink", savedProduct.Name);
        Assert.Equal(2, savedProduct.Categories.Count);
    }

    /// <summary>
    /// Verifies that posting without categories returns the page and adds a validation error.
    /// </summary>
    /// <remarks>
    /// Purpose: validate product-category requirement enforcement.
    /// Explanation: posts model data without selected categories and asserts page result with model-state error.
    /// Parameters: none.
    /// Expected output: page result with a category validation message and no persisted product.
    /// Possible errors: no custom exceptions expected beyond EF Core setup failures.
    /// </remarks>
    [Fact]
    public async Task OnPostAsync_WithoutCategories_ReturnsPageWithValidationError()
    {
        await using var scopeContext = await TestServiceScopeContext.CreateAsync();
        var services = scopeContext.Scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        var supplier = new Supplier { Name = "Supplier B" };
        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync();

        var model = new CreateModel(dbContext)
        {
            Product = new InventoryManagement.Data.Product
            {
                Name = "Notebook",
                UPC = "100000000100",
                SupplierId = supplier.Id,
                Cost = 2.20m,
                Quantity = 20,
                LowStockThreshold = 4
            }
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains(model.ModelState, kv => kv.Key == nameof(CreateModel.SelectedCategoryIds));
        Assert.False(await dbContext.Products.AnyAsync(p => p.UPC == "100000000100"));
    }
}
