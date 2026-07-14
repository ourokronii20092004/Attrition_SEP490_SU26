using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Character.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddPotionMaxManaFlasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PotionMaxManaFlasks",
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
                name: "PotionMaxManaFlasks",
                schema: "character",
                table: "character_session");
        }
    }
}
