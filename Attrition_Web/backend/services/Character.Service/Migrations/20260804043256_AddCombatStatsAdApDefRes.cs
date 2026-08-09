using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Character.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddCombatStatsAdApDefRes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Ad",
                schema: "character",
                table: "character_session",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Ap",
                schema: "character",
                table: "character_session",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Def",
                schema: "character",
                table: "character_session",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Res",
                schema: "character",
                table: "character_session",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ad",
                schema: "character",
                table: "character_session");

            migrationBuilder.DropColumn(
                name: "Ap",
                schema: "character",
                table: "character_session");

            migrationBuilder.DropColumn(
                name: "Def",
                schema: "character",
                table: "character_session");

            migrationBuilder.DropColumn(
                name: "Res",
                schema: "character",
                table: "character_session");
        }
    }
}