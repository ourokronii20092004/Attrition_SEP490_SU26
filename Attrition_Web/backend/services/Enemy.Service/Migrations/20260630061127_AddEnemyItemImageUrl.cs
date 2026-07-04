using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enemy.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddEnemyItemImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                schema: "enemy",
                table: "items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                schema: "enemy",
                table: "enemies",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                schema: "enemy",
                table: "items");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                schema: "enemy",
                table: "enemies");
        }
    }
}
