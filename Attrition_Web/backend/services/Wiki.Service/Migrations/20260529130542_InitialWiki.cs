using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Wiki.Service.Migrations
{
    /// <inheritdoc />
    public partial class InitialWiki : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "wiki");

            migrationBuilder.CreateTable(
                name: "WikiArticles",
                schema: "wiki",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByName = table.Column<string>(type: "text", nullable: true),
                    LastEditedById = table.Column<Guid>(type: "uuid", nullable: true),
                    LastEditedByName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WikiCategories",
                schema: "wiki",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IconUrl = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WikiContributions",
                schema: "wiki",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributorName = table.Column<string>(type: "text", nullable: true),
                    SuggestedContent = table.Column<string>(type: "text", nullable: false),
                    ChangeNote = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiContributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WikiRevisions",
                schema: "wiki",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    EditedById = table.Column<Guid>(type: "uuid", nullable: true),
                    EditedByName = table.Column<string>(type: "text", nullable: true),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangeNote = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiRevisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WikiArticles_CategoryId",
                schema: "wiki",
                table: "WikiArticles",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_WikiArticles_Slug",
                schema: "wiki",
                table: "WikiArticles",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WikiCategories_Slug",
                schema: "wiki",
                table: "WikiCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WikiContributions_ArticleId",
                schema: "wiki",
                table: "WikiContributions",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_WikiContributions_Status",
                schema: "wiki",
                table: "WikiContributions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WikiRevisions_ArticleId",
                schema: "wiki",
                table: "WikiRevisions",
                column: "ArticleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WikiArticles",
                schema: "wiki");

            migrationBuilder.DropTable(
                name: "WikiCategories",
                schema: "wiki");

            migrationBuilder.DropTable(
                name: "WikiContributions",
                schema: "wiki");

            migrationBuilder.DropTable(
                name: "WikiRevisions",
                schema: "wiki");
        }
    }
}
