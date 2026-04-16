using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddGeminiConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeminiSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SelectedModel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ModelsLastRefreshedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeminiSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeminiAvailableModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GeminiSettingsId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeminiAvailableModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeminiAvailableModels_GeminiSettings_GeminiSettingsId",
                        column: x => x.GeminiSettingsId,
                        principalTable: "GeminiSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeminiAvailableModels_GeminiSettingsId_ModelName",
                table: "GeminiAvailableModels",
                columns: new[] { "GeminiSettingsId", "ModelName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeminiSettings_Provider",
                table: "GeminiSettings",
                column: "Provider",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeminiAvailableModels");

            migrationBuilder.DropTable(
                name: "GeminiSettings");
        }
    }
}
