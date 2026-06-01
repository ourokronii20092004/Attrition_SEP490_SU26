using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddPostNesting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Depth",
                schema: "forum",
                table: "ForumPosts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentPostId",
                schema: "forum",
                table: "ForumPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumPosts_ParentPostId",
                schema: "forum",
                table: "ForumPosts",
                column: "ParentPostId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ForumPosts_ParentPostId",
                schema: "forum",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "Depth",
                schema: "forum",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "ParentPostId",
                schema: "forum",
                table: "ForumPosts");
        }
    }
}
