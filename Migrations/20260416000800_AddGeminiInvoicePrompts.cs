using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddGeminiInvoicePrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceImageToTextPrompt",
                table: "GeminiSettings",
                type: "TEXT",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceStructuredExtractionPrompt",
                table: "GeminiSettings",
                type: "TEXT",
                maxLength: 8000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoiceImageToTextPrompt",
                table: "GeminiSettings");

            migrationBuilder.DropColumn(
                name: "InvoiceStructuredExtractionPrompt",
                table: "GeminiSettings");
        }
    }
}
