using InventoryManagement.Data;
using InventoryManagement.Pages.PurchaseOrder;
using InventoryManagement.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InventoryManagement.Tests.Pages.PurchaseOrder;

public class DetailsModelTests
{
    /// <summary>
    /// Verifies that completing an in-process order with inventory update increments product quantities and completes the order.
    /// </summary>
    /// <remarks>
    /// Purpose: validate completion workflow that applies quantities to inventory.
    /// Explanation: creates order lines, executes completion with inventory update, and checks status/message persistence.
    /// Parameters: none.
    /// Expected output: completed order, incremented product quantity, and redirect to details page.
    /// Possible errors: data access exceptions may propagate from EF Core operations.
    /// </remarks>
    [Fact]
    public async Task OnPostCompleteAsync_WithInventoryUpdate_CompletesOrderAndIncrementsStock()
    {
        await using var scopeContext = await TestServiceScopeContext.CreateAsync();
        var services = scopeContext.Scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        var supplier = new Supplier { Name = "Blue Ocean" };
        var product = new InventoryManagement.Data.Product
        {
            Name = "USB-C Cable",
            UPC = "300000000001",
            Supplier = supplier,
            Cost = 3.00m,
            Quantity = 5,
            LowStockThreshold = 2,
            IsDiscontinued = false
        };

        var order = new InventoryManagement.Data.PurchaseOrder
        {
            Supplier = supplier,
            Status = PurchaseOrderStatus.InProcess,
            PurchaseOrderProducts =
            [
                new PurchaseOrderProduct
                {
                    Product = product,
                    QuantityAdded = 9
                }
            ]
        };

        dbContext.PurchaseOrders.Add(order);
        await dbContext.SaveChangesAsync();

        var model = new DetailsModel(dbContext);

        var result = await model.OnPostCompleteAsync(order.Id, addItemsToInventory: true);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(redirect.PageName);

        var updatedOrder = await dbContext.PurchaseOrders.SingleAsync(po => po.Id == order.Id);
        var updatedProduct = await dbContext.Products.SingleAsync(p => p.Id == product.Id);

        Assert.Equal(PurchaseOrderStatus.Completed, updatedOrder.Status);
        Assert.Equal(14, updatedProduct.Quantity);
        Assert.Equal("Purchase order completed and inventory quantities were updated.", model.StatusMessage);
    }

    /// <summary>
    /// Verifies that completing an already completed order does not modify inventory and returns an informational status message.
    /// </summary>
    /// <remarks>
    /// Purpose: validate idempotent completion guard.
    /// Explanation: attempts completion of a completed order and asserts no quantity changes and guard message.
    /// Parameters: none.
    /// Expected output: redirect with already-completed status message and unchanged inventory.
    /// Possible errors: no custom exceptions expected beyond data setup failures.
    /// </remarks>
    [Fact]
    public async Task OnPostCompleteAsync_WhenOrderAlreadyCompleted_ReturnsGuardMessageWithoutStockChange()
    {
        await using var scopeContext = await TestServiceScopeContext.CreateAsync();
        var services = scopeContext.Scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        var supplier = new Supplier { Name = "Urban Goods" };
        var product = new InventoryManagement.Data.Product
        {
            Name = "Desk Organizer",
            UPC = "300000000002",
            Supplier = supplier,
            Cost = 12.00m,
            Quantity = 11,
            LowStockThreshold = 3,
            IsDiscontinued = false
        };

        var order = new InventoryManagement.Data.PurchaseOrder
        {
            Supplier = supplier,
            Status = PurchaseOrderStatus.Completed,
            PurchaseOrderProducts =
            [
                new PurchaseOrderProduct
                {
                    Product = product,
                    QuantityAdded = 5
                }
            ]
        };

        dbContext.PurchaseOrders.Add(order);
        await dbContext.SaveChangesAsync();

        var model = new DetailsModel(dbContext);

        var result = await model.OnPostCompleteAsync(order.Id, addItemsToInventory: true);

        Assert.IsType<RedirectToPageResult>(result);
        var unchangedProduct = await dbContext.Products.SingleAsync(p => p.Id == product.Id);

        Assert.Equal(11, unchangedProduct.Quantity);
        Assert.Equal("Purchase order is already completed.", model.StatusMessage);
    }
}
