using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Character.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomStateSaves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "room_state_saves",
                schema: "character",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentScene = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WorldStatesJson = table.Column<string>(type: "jsonb", nullable: true),
                    FogJson = table.Column<string>(type: "jsonb", nullable: true),
                    PlayTimeSeconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_state_saves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_room_state_saves_sessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "character",
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_room_state_saves_SessionId_CapturedAt",
                schema: "character",
                table: "room_state_saves",
                columns: new[] { "SessionId", "CapturedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_state_saves",
                schema: "character");
        }
    }
}
