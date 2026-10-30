using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookside.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReadingGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetBooks = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingGoals", x => x.Id);
                    table.CheckConstraint("CK_ReadingGoals_TargetBooks", "TargetBooks BETWEEN 1 AND 1000");
                    table.CheckConstraint("CK_ReadingGoals_Year", "Year BETWEEN 1 AND 9999");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingGoals_Year",
                table: "ReadingGoals",
                column: "Year",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReadingGoals");
        }
    }
}
