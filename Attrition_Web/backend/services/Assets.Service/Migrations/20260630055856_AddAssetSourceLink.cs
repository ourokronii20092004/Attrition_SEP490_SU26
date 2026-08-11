using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assets.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetSourceLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceId",
                schema: "assets",
                table: "Assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                schema: "assets",
                table: "Assets",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceId",
                schema: "assets",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SourceType",
                schema: "assets",
                table: "Assets");
        }
    }
}