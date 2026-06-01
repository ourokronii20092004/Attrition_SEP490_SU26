using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddUserReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserReports",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedUserName = table.Column<string>(type: "text", nullable: true),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterName = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_ReportedUserId",
                schema: "identity",
                table: "UserReports",
                column: "ReportedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_Status_CreatedAt",
                schema: "identity",
                table: "UserReports",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserReports",
                schema: "identity");
        }
    }
}
