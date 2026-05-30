using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Forum.Service.Migrations
{
    /// <inheritdoc />
    public partial class InitialForum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "forum");

            migrationBuilder.CreateTable(
                name: "ForumCategories",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ForumPosts",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorName = table.Column<string>(type: "text", nullable: true),
                    AuthorAvatar = table.Column<string>(type: "text", nullable: true),
                    AuthorRole = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Attachments = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    RemovedReason = table.Column<string>(type: "text", nullable: true),
                    RemovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RemovedByName = table.Column<string>(type: "text", nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ForumReactions",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReactionType = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumReactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ForumThreads",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorName = table.Column<string>(type: "text", nullable: true),
                    AuthorAvatar = table.Column<string>(type: "text", nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastReplyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReplyCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumThreads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostReports",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterName = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThreadSubscriptions",
                schema: "forum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscribedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreadSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForumCategories_Slug",
                schema: "forum",
                table: "ForumCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumPosts_ThreadId",
                schema: "forum",
                table: "ForumPosts",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumReactions_PostId_UserId_ReactionType",
                schema: "forum",
                table: "ForumReactions",
                columns: new[] { "PostId", "UserId", "ReactionType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumThreads_CategoryId",
                schema: "forum",
                table: "ForumThreads",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReports_Status",
                schema: "forum",
                table: "PostReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ThreadSubscriptions_ThreadId_UserId",
                schema: "forum",
                table: "ThreadSubscriptions",
                columns: new[] { "ThreadId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForumCategories",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "ForumPosts",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "ForumReactions",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "ForumThreads",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "PostReports",
                schema: "forum");

            migrationBuilder.DropTable(
                name: "ThreadSubscriptions",
                schema: "forum");
        }
    }
}
