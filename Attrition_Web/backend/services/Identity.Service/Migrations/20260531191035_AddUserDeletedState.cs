using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDeletedState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "identity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "identity",
                table: "Users");
        }
    }
}
