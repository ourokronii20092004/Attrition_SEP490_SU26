using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionInvalidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TokensValidAfter",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            // Grandfather every account that already exists when the email-verification login gate
            // ships, so current users (including seeded/demo accounts with placeholder emails) aren't
            // suddenly locked out. Only registrations created AFTER this migration face the gate.
            migrationBuilder.Sql(@"UPDATE identity.""Users"" SET ""IsEmailVerified"" = true WHERE ""IsEmailVerified"" = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TokensValidAfter",
                schema: "identity",
                table: "Users");
        }
    }
}
