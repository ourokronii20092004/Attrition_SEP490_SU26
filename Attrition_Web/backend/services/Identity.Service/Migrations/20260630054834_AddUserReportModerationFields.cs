using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddUserReportModerationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionTaken",
                schema: "identity",
                table: "UserReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModeratorNote",
                schema: "identity",
                table: "UserReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                schema: "identity",
                table: "UserReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedByName",
                schema: "identity",
                table: "UserReports",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionTaken",
                schema: "identity",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ModeratorNote",
                schema: "identity",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                schema: "identity",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ResolvedByName",
                schema: "identity",
                table: "UserReports");
        }
    }
}
