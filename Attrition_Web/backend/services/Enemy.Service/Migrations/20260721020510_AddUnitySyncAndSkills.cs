using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enemy.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitySyncAndSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_enemy_loot_enemies_EnemyId",
                schema: "enemy",
                table: "enemy_loot");
            migrationBuilder.Sql("""
                UPDATE enemy.enemies SET "EnemyId" = 'gollux' WHERE "EnemyId" = 'Gollux';
                UPDATE enemy.enemy_loot SET "EnemyId" = 'gollux' WHERE "EnemyId" = 'Gollux';
                """);
            migrationBuilder.AddForeignKey(
                name: "FK_enemy_loot_enemies_EnemyId",
                schema: "enemy",
                table: "enemy_loot",
                column: "EnemyId",
                principalSchema: "enemy",
                principalTable: "enemies",
                principalColumn: "EnemyId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAt",
                schema: "enemy",
                table: "items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnityBaselineJson",
                schema: "enemy",
                table: "items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "ChaseSpeed",
                schema: "enemy",
                table: "enemies",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImportedAt",
                schema: "enemy",
                table: "enemies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "PatrolSpeed",
                schema: "enemy",
                table: "enemies",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "Poise",
                schema: "enemy",
                table: "enemies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "PoiseRecoveryTime",
                schema: "enemy",
                table: "enemies",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<string>(
                name: "UnityBaselineJson",
                schema: "enemy",
                table: "enemies",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "skills",
                schema: "enemy",
                columns: table => new
                {
                    SkillId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Element = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ManaCost = table.Column<int>(type: "integer", nullable: false),
                    CastTime = table.Column<float>(type: "real", nullable: false),
                    Cooldown = table.Column<float>(type: "real", nullable: false),
                    ActiveStartFrac = table.Column<float>(type: "real", nullable: false),
                    ActiveEndFrac = table.Column<float>(type: "real", nullable: false),
                    DamageType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BaseDamage = table.Column<int>(type: "integer", nullable: false),
                    ApScaling = table.Column<float>(type: "real", nullable: false),
                    KnockbackForce = table.Column<float>(type: "real", nullable: false),
                    TickInterval = table.Column<float>(type: "real", nullable: false),
                    SweetSpotRadius = table.Column<float>(type: "real", nullable: false),
                    SweetSpotMultiplier = table.Column<float>(type: "real", nullable: false),
                    Delivery = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    HitShape = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Range = table.Column<float>(type: "real", nullable: false),
                    Angle = table.Column<float>(type: "real", nullable: false),
                    RectWidth = table.Column<float>(type: "real", nullable: false),
                    RectHeight = table.Column<float>(type: "real", nullable: false),
                    OffsetX = table.Column<float>(type: "real", nullable: false),
                    OffsetY = table.Column<float>(type: "real", nullable: false),
                    ProjectileSpeed = table.Column<float>(type: "real", nullable: false),
                    ProjectileCount = table.Column<int>(type: "integer", nullable: false),
                    SpreadAngle = table.Column<float>(type: "real", nullable: false),
                    VfxLifetime = table.Column<float>(type: "real", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    UnityBaselineJson = table.Column<string>(type: "text", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.SkillId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "skills",
                schema: "enemy");

            migrationBuilder.DropColumn(
                name: "ImportedAt",
                schema: "enemy",
                table: "items");

            migrationBuilder.DropColumn(
                name: "UnityBaselineJson",
                schema: "enemy",
                table: "items");

            migrationBuilder.DropColumn(
                name: "ChaseSpeed",
                schema: "enemy",
                table: "enemies");

            migrationBuilder.DropColumn(
                name: "ImportedAt",
                schema: "enemy",
                table: "enemies");

            migrationBuilder.DropColumn(
                name: "PatrolSpeed",
                schema: "enemy",
                table: "enemies");

            migrationBuilder.DropColumn(
                name: "Poise",
                schema: "enemy",
                table: "enemies");

            migrationBuilder.DropColumn(
                name: "PoiseRecoveryTime",
                schema: "enemy",
                table: "enemies");

            migrationBuilder.DropColumn(
                name: "UnityBaselineJson",
                schema: "enemy",
                table: "enemies");
        }
    }
}
