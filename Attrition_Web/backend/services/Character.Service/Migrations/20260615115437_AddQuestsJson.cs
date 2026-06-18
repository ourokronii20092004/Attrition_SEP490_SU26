using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Character.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuestsJson",
                schema: "character",
                table: "characters",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuestsJson",
                schema: "character",
                table: "characters");
        }
    }
}
