using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Attrition.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUserThemeAndBackground : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundUrl",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThemeAccent",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ThemeMode",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ThemeAccent",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ThemeMode",
                table: "Users");
        }
    }
}
