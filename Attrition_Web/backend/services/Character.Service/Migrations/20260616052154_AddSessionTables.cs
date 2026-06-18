using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Character.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "character",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsMultiplayer = table.Column<bool>(type: "boolean", nullable: false),
                    PlayTimeSeconds = table.Column<int>(type: "integer", nullable: false),
                    CurrentScene = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastPlayedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "character_session",
                schema: "character",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerRole = table.Column<short>(type: "smallint", nullable: false),
                    CurrentLevel = table.Column<int>(type: "integer", nullable: false),
                    CurrentExp = table.Column<int>(type: "integer", nullable: false),
                    AllocatedPointsJson = table.Column<string>(type: "jsonb", nullable: true),
                    MaxHp = table.Column<int>(type: "integer", nullable: false),
                    CurrentHp = table.Column<int>(type: "integer", nullable: false),
                    MaxMana = table.Column<int>(type: "integer", nullable: false),
                    CurrentMana = table.Column<int>(type: "integer", nullable: false),
                    MaxStamina = table.Column<int>(type: "integer", nullable: false),
                    PotionMaxFlasks = table.Column<int>(type: "integer", nullable: false),
                    AttackSpeed = table.Column<float>(type: "real", nullable: false),
                    PosX = table.Column<float>(type: "real", nullable: false),
                    PosY = table.Column<float>(type: "real", nullable: false),
                    LastRestPointId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InventoryJson = table.Column<string>(type: "jsonb", nullable: true),
                    EquipmentJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_session", x => new { x.CharacterId, x.SessionId });
                    table.ForeignKey(
                        name: "FK_character_session_sessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "character",
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "world_state",
                schema: "character",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StateValue = table.Column<short>(type: "smallint", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_state", x => new { x.SessionId, x.EventId });
                    table.ForeignKey(
                        name: "FK_world_state_sessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "character",
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_character_session_SessionId",
                schema: "character",
                table: "character_session",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_OwnerId",
                schema: "character",
                table: "sessions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_RoomCode",
                schema: "character",
                table: "sessions",
                column: "RoomCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_session",
                schema: "character");

            migrationBuilder.DropTable(
                name: "world_state",
                schema: "character");

            migrationBuilder.DropTable(
                name: "sessions",
                schema: "character");
        }
    }
}
