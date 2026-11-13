using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookside.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VocabularyWords_CreatedAtUtc",
                table: "VocabularyWords",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Books_CreatedAtUtc",
                table: "Books",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BookComments_CreatedAtUtc",
                table: "BookComments",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VocabularyWords_CreatedAtUtc",
                table: "VocabularyWords");

            migrationBuilder.DropIndex(
                name: "IX_Books_CreatedAtUtc",
                table: "Books");

            migrationBuilder.DropIndex(
                name: "IX_BookComments_CreatedAtUtc",
                table: "BookComments");
        }
    }
}
