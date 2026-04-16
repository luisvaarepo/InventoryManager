using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryManagement.Migrations
{
    /// <inheritdoc />
    public partial class providers2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Providers_SupplierId1",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SupplierId1",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SupplierId1",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SupplierId1",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_SupplierId1",
                table: "Products",
                column: "SupplierId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Providers_SupplierId1",
                table: "Products",
                column: "SupplierId1",
                principalTable: "Providers",
                principalColumn: "Id");
        }
    }
}
