using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assets.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitySourceImageHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                schema: "assets",
                table: "Assets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_SourceType_SourceId",
                schema: "assets",
                table: "Assets",
                columns: new[] { "SourceType", "SourceId" },
                unique: true,
                filter: "\"SourceType\" IN ('unity-item', 'unity-skill', 'unity-enemy') AND \"SourceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assets_SourceType_SourceId",
                schema: "assets",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                schema: "assets",
                table: "Assets");
        }
    }
}
