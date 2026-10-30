using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookside.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddWordReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WordReviews",
                columns: table => new
                {
                    WordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IntervalDays = table.Column<int>(type: "INTEGER", nullable: false),
                    Ease = table.Column<double>(type: "REAL", nullable: false),
                    Repetitions = table.Column<int>(type: "INTEGER", nullable: false),
                    Lapses = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstReviewedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    LastReviewedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordReviews", x => x.WordId);
                    table.CheckConstraint("CK_WordReviews_Ease", "Ease >= 1.3");
                    table.CheckConstraint("CK_WordReviews_IntervalDays", "IntervalDays >= 0");
                    table.ForeignKey(
                        name: "FK_WordReviews_VocabularyWords_WordId",
                        column: x => x.WordId,
                        principalTable: "VocabularyWords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WordReviews_DueOn",
                table: "WordReviews",
                column: "DueOn");

            migrationBuilder.CreateIndex(
                name: "IX_WordReviews_FirstReviewedOn",
                table: "WordReviews",
                column: "FirstReviewedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WordReviews");
        }
    }
}
