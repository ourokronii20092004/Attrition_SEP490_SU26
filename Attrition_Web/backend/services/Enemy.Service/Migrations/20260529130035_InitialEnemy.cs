using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Enemy.Service.Migrations
{
    /// <inheritdoc />
    public partial class InitialEnemy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "enemy");

            migrationBuilder.CreateTable(
                name: "enemies",
                schema: "enemy",
                columns: table => new
                {
                    EnemyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SpawnBiome = table.Column<string>(type: "text", nullable: true),
                    Hp = table.Column<int>(type: "integer", nullable: false),
                    Ad = table.Column<int>(type: "integer", nullable: false),
                    Ap = table.Column<int>(type: "integer", nullable: false),
                    Def = table.Column<int>(type: "integer", nullable: false),
                    Res = table.Column<int>(type: "integer", nullable: false),
                    AttackSpeed = table.Column<float>(type: "real", nullable: false, defaultValue: 1f),
                    IsRanged = table.Column<bool>(type: "boolean", nullable: false),
                    ExpReward = table.Column<int>(type: "integer", nullable: false),
                    GoldReward = table.Column<int>(type: "integer", nullable: false),
                    Lore = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enemies", x => x.EnemyId);
                });

            migrationBuilder.CreateTable(
                name: "enemy_loot",
                schema: "enemy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rarity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IconKey = table.Column<string>(type: "text", nullable: true),
                    DropChance = table.Column<float>(type: "real", nullable: false),
                    MinQty = table.Column<short>(type: "smallint", nullable: false),
                    MaxQty = table.Column<short>(type: "smallint", nullable: false),
                    EnemyId = table.Column<string>(type: "character varying(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enemy_loot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_enemy_loot_enemies_EnemyId",
                        column: x => x.EnemyId,
                        principalSchema: "enemy",
                        principalTable: "enemies",
                        principalColumn: "EnemyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_enemies_Tier",
                schema: "enemy",
                table: "enemies",
                column: "Tier");

            migrationBuilder.CreateIndex(
                name: "IX_enemy_loot_EnemyId",
                schema: "enemy",
                table: "enemy_loot",
                column: "EnemyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "enemy_loot",
                schema: "enemy");

            migrationBuilder.DropTable(
                name: "enemies",
                schema: "enemy");
        }
    }
}
