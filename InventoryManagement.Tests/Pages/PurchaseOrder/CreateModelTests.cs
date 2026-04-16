using InventoryManagement.Data;
using InventoryManagement.Pages.PurchaseOrder;
using InventoryManagement.Services;
using InventoryManagement.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InventoryManagement.Tests.Pages.PurchaseOrder;

public class CreateModelTests
{
    /// <summary>
    /// Verifies that saving with supplier and order lines creates an in-process purchase order and redirects.
    /// </summary>
    /// <remarks>
    /// Purpose: validate core purchase-order persistence flow.
    /// Explanation: submits prepared order lines for a selected supplier and asserts persisted order and line items.
    /// Parameters: none.
    /// Expected output: redirect to list and one in-process purchase order with expected line quantities.
    /// Possible errors: data access exceptions can propagate from EF Core save operations.
    /// </remarks>
    [Fact]
    public async Task OnPostSaveAsync_WithValidData_SavesOrderAndRedirects()
    {
        await using var scopeContext = await TestServiceScopeContext.CreateAsync();
        var services = scopeContext.Scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        var supplier = new Supplier { Name = "Northwind" };
        var product = new InventoryManagement.Data.Product
        {
            Name = "Packing Tape",
            UPC = "200000000001",
            Supplier = supplier,
            Cost = 1.25m,
            Quantity = 10,
            LowStockThreshold = 3,
            IsDiscontinued = false
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var model = new CreateModel(dbContext, new FakeGeminiInvoiceExtractionService())
        {
            SelectedSupplierId = supplier.Id,
            OrderLines =
            [
                new CreateModel.OrderLine
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Cost = product.Cost,
                    Quantity = 7
                }
            ]
        };

        var result = await model.OnPostSaveAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/PurchaseOrder/Index", redirect.PageName);

        var savedOrder = await dbContext.PurchaseOrders
            .Include(po => po.PurchaseOrderProducts)
            .SingleAsync();

        Assert.Equal(PurchaseOrderStatus.InProcess, savedOrder.Status);
        Assert.Single(savedOrder.PurchaseOrderProducts);
        Assert.Equal(7, savedOrder.PurchaseOrderProducts.First().QuantityAdded);
    }

    /// <summary>
    /// Verifies that saving without supplier and lines returns the page with validation feedback.
    /// </summary>
    /// <remarks>
    /// Purpose: validate required-input safeguards for purchase-order creation.
    /// Explanation: invokes save with empty selection and asserts page result and model-state error.
    /// Parameters: none.
    /// Expected output: page result with validation error and no persisted purchase orders.
    /// Possible errors: no custom exceptions expected beyond test setup failures.
    /// </remarks>
    [Fact]
    public async Task OnPostSaveAsync_WithoutSupplierOrLines_ReturnsPageWithError()
    {
        await using var scopeContext = await TestServiceScopeContext.CreateAsync();
        var services = scopeContext.Scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        var model = new CreateModel(dbContext, new FakeGeminiInvoiceExtractionService())
        {
            SelectedSupplierId = 0,
            OrderLines = []
        };

        var result = await model.OnPostSaveAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains(model.ModelState[string.Empty]!.Errors, e => e.ErrorMessage.Contains("Please select a supplier"));
        Assert.False(await dbContext.PurchaseOrders.AnyAsync());
    }

    /// <summary>
    /// Provides a no-op Gemini extraction service for tests that do not execute extraction workflows.
    /// </summary>
    /// <remarks>
    /// Purpose: satisfy purchase-order page model constructor dependency in unit tests.
    /// Explanation: returns an empty extraction result whenever invoked.
    /// Parameters: request and cancellation token are accepted but ignored.
    /// Expected output: a deterministic empty extraction response.
    /// Possible errors: no custom exceptions are thrown by this fake implementation.
    /// </remarks>
    private sealed class FakeGeminiInvoiceExtractionService : IGeminiInvoiceExtractionService
    {
        /// <summary>
        /// Returns an empty extracted invoice payload for non-extraction test scenarios.
        /// </summary>
        /// <param name="request">Extraction request input.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task containing an empty extracted invoice.</returns>
        /// <remarks>
        /// Purpose: provide a deterministic stub implementation for constructor wiring.
        /// Explanation: avoids outbound API calls during unit tests.
        /// Parameters: request and cancellation token are unused.
        /// Expected output: empty invoice structure.
        /// Possible errors: no custom exceptions are thrown by this method.
        /// </remarks>
        public Task<GeminiExtractedInvoice> ExtractInvoiceAsync(GeminiInvoiceExtractionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new GeminiExtractedInvoice());
    }
}
