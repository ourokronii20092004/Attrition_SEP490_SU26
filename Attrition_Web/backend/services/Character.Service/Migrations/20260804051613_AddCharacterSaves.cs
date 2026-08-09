using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Character.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterSaves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "character_saves",
                schema: "character",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CurrentScene = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EventType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PlayerRole = table.Column<short>(type: "smallint", nullable: false),
                    CurrentLevel = table.Column<int>(type: "integer", nullable: false),
                    CurrentExp = table.Column<int>(type: "integer", nullable: false),
                    DeathCount = table.Column<int>(type: "integer", nullable: false),
                    PlaytimeSeconds = table.Column<int>(type: "integer", nullable: false),
                    IsAlive = table.Column<bool>(type: "boolean", nullable: false),
                    AllocatedPointsJson = table.Column<string>(type: "jsonb", nullable: true),
                    MaxHp = table.Column<int>(type: "integer", nullable: false),
                    CurrentHp = table.Column<int>(type: "integer", nullable: false),
                    MaxMana = table.Column<int>(type: "integer", nullable: false),
                    CurrentMana = table.Column<int>(type: "integer", nullable: false),
                    MaxStamina = table.Column<int>(type: "integer", nullable: false),
                    AttackSpeed = table.Column<float>(type: "real", nullable: false),
                    PotionMaxFlasks = table.Column<int>(type: "integer", nullable: false),
                    PotionMaxManaFlasks = table.Column<int>(type: "integer", nullable: false),
                    HealthCharges = table.Column<int>(type: "integer", nullable: false),
                    ManaCharges = table.Column<int>(type: "integer", nullable: false),
                    Ad = table.Column<int>(type: "integer", nullable: false),
                    Ap = table.Column<int>(type: "integer", nullable: false),
                    Def = table.Column<int>(type: "integer", nullable: false),
                    Res = table.Column<int>(type: "integer", nullable: false),
                    PosX = table.Column<float>(type: "real", nullable: false),
                    PosY = table.Column<float>(type: "real", nullable: false),
                    PosZ = table.Column<float>(type: "real", nullable: false),
                    LastRestPointId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InventoryJson = table.Column<string>(type: "jsonb", nullable: true),
                    EquipmentJson = table.Column<string>(type: "jsonb", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_saves", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_character_saves_CharacterId_CapturedAt",
                schema: "character",
                table: "character_saves",
                columns: new[] { "CharacterId", "CapturedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_saves",
                schema: "character");
        }
    }
}