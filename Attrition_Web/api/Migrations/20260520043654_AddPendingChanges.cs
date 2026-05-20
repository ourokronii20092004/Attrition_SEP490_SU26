using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Attrition.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRemoved",
                table: "ForumPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAt",
                table: "ForumPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemovedByUserId",
                table: "ForumPosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemovedReason",
                table: "ForumPosts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRemoved",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "RemovedByUserId",
                table: "ForumPosts");

            migrationBuilder.DropColumn(
                name: "RemovedReason",
                table: "ForumPosts");
        }
    }
}
