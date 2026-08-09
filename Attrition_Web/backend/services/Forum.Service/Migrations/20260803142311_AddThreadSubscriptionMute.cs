using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddThreadSubscriptionMute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMuted",
                schema: "forum",
                table: "ThreadSubscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMuted",
                schema: "forum",
                table: "ThreadSubscriptions");
        }
    }
}