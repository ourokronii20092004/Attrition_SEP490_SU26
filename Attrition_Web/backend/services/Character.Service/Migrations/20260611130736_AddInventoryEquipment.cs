using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Character.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EquipmentJson",
                schema: "character",
                table: "characters",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InventoryJson",
                schema: "character",
                table: "characters",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquipmentJson",
                schema: "character",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "InventoryJson",
                schema: "character",
                table: "characters");
        }
    }
}
