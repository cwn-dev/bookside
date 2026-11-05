using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookside.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommentTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BookId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentTags", x => x.Id);
                    table.CheckConstraint("CK_CommentTags_BookId", "(Type = 'General') = (BookId IS NULL)");
                    table.ForeignKey(
                        name: "FK_CommentTags_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookCommentTags",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookCommentTags", x => new { x.CommentId, x.TagId });
                    table.ForeignKey(
                        name: "FK_BookCommentTags_BookComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "BookComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookCommentTags_CommentTags_TagId",
                        column: x => x.TagId,
                        principalTable: "CommentTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookCommentTags_TagId",
                table: "BookCommentTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentTags_BookId_Type_NormalizedName",
                table: "CommentTags",
                columns: new[] { "BookId", "Type", "NormalizedName" },
                unique: true,
                filter: "BookId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CommentTags_NormalizedName",
                table: "CommentTags",
                column: "NormalizedName",
                unique: true,
                filter: "BookId IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookCommentTags");

            migrationBuilder.DropTable(
                name: "CommentTags");
        }
    }
}
