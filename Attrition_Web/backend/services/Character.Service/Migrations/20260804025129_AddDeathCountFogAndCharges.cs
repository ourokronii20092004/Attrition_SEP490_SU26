using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Character.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddDeathCountFogAndCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FogJson",
                schema: "character",
                table: "sessions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeathCount",
                schema: "character",
                table: "character_session",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HealthCharges",
                schema: "character",
                table: "character_session",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ManaCharges",
                schema: "character",
                table: "character_session",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "PosZ",
                schema: "character",
                table: "character_session",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FogJson",
                schema: "character",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "DeathCount",
                schema: "character",
                table: "character_session");

            migrationBuilder.DropColumn(
                name: "HealthCharges",
                schema: "character",
                table: "character_session");

            migrationBuilder.DropColumn(
                name: "ManaCharges",
                schema: "character",
                table: "character_session");

            migrationBuilder.DropColumn(
                name: "PosZ",
                schema: "character",
                table: "character_session");
        }
    }
}
