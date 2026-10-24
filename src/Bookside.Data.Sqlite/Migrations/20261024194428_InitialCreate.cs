using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookside.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Authors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Authors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Books",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Subtitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Isbn13 = table.Column<string>(type: "TEXT", unicode: false, maxLength: 13, nullable: true),
                    Isbn10 = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: true),
                    Publisher = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    PublishedYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: true),
                    MetadataSource = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    MetadataSourceId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Books", x => x.Id);
                    table.CheckConstraint("CK_Books_PageCount", "PageCount IS NULL OR PageCount > 0");
                });

            migrationBuilder.CreateTable(
                name: "DictionaryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NormalizedTerm = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Language = table.Column<string>(type: "TEXT", unicode: false, maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FetchedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Phonetic = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AudioUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RawResponse = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Meanings = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DictionaryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookAuthors",
                columns: table => new
                {
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookAuthors", x => new { x.BookId, x.AuthorId });
                    table.ForeignKey(
                        name: "FK_BookAuthors_Authors_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Authors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookAuthors_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookCovers",
                columns: table => new
                {
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", maxLength: 2097152, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", unicode: false, maxLength: 100, nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookCovers", x => x.BookId);
                    table.ForeignKey(
                        name: "FK_BookCovers_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReadingSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    FinishedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingSessions", x => x.Id);
                    table.CheckConstraint("CK_ReadingSessions_Dates", "StartedOn IS NULL OR FinishedOn IS NULL OR FinishedOn >= StartedOn");
                    table.CheckConstraint("CK_ReadingSessions_Rating", "Rating IS NULL OR Rating BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_ReadingSessions_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyWords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Term = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedTerm = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Language = table.Column<string>(type: "TEXT", unicode: false, maxLength: 16, nullable: false),
                    BookId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Context = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PageNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    DictionaryEntryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyWords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularyWords_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VocabularyWords_DictionaryEntries_DictionaryEntryId",
                        column: x => x.DictionaryEntryId,
                        principalTable: "DictionaryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Authors_Name",
                table: "Authors",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_BookAuthors_AuthorId",
                table: "BookAuthors",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Books_Isbn13",
                table: "Books",
                column: "Isbn13");

            migrationBuilder.CreateIndex(
                name: "IX_Books_Title",
                table: "Books",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_DictionaryEntries_Language_NormalizedTerm",
                table: "DictionaryEntries",
                columns: new[] { "Language", "NormalizedTerm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadingSessions_BookId_StartedOn",
                table: "ReadingSessions",
                columns: new[] { "BookId", "StartedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingSessions_FinishedOn",
                table: "ReadingSessions",
                column: "FinishedOn");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyWords_BookId",
                table: "VocabularyWords",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyWords_DictionaryEntryId",
                table: "VocabularyWords",
                column: "DictionaryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyWords_Language_NormalizedTerm",
                table: "VocabularyWords",
                columns: new[] { "Language", "NormalizedTerm" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookAuthors");

            migrationBuilder.DropTable(
                name: "BookCovers");

            migrationBuilder.DropTable(
                name: "ReadingSessions");

            migrationBuilder.DropTable(
                name: "VocabularyWords");

            migrationBuilder.DropTable(
                name: "Authors");

            migrationBuilder.DropTable(
                name: "Books");

            migrationBuilder.DropTable(
                name: "DictionaryEntries");
        }
    }
}
