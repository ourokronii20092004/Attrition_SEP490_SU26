using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Attrition.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAlbumCoverOverrideTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCoverUserDefined",
                table: "MusicAlbums",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCoverUserDefined",
                table: "MusicAlbums");
        }
    }
}
