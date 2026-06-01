using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddWikiArticleThread : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WikiArticleId",
                schema: "forum",
                table: "ForumThreads",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumThreads_WikiArticleId",
                schema: "forum",
                table: "ForumThreads",
                column: "WikiArticleId",
                unique: true,
                filter: "\"WikiArticleId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ForumThreads_WikiArticleId",
                schema: "forum",
                table: "ForumThreads");

            migrationBuilder.DropColumn(
                name: "WikiArticleId",
                schema: "forum",
                table: "ForumThreads");
        }
    }
}
