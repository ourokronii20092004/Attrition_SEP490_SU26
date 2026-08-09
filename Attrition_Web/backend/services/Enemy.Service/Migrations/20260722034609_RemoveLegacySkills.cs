using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enemy.Service.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacySkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Skill.Service must have copied every legacy row before this destructive cleanup runs.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('enemy.skills') IS NOT NULL THEN
                        IF to_regclass('skill.skills') IS NULL OR
                           EXISTS (SELECT 1 FROM enemy.skills e WHERE NOT EXISTS (SELECT 1 FROM skill.skills s WHERE s."SkillId" = e."SkillId")) THEN
                            RAISE EXCEPTION 'Skill.Service migration must copy all enemy.skills rows before Enemy.Service cleanup';
                        END IF;
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "skills",
                schema: "enemy");
            migrationBuilder.Sql("""
                DELETE FROM enemy.item_modifiers
                WHERE "ItemId" IN (SELECT "ItemId" FROM enemy.items WHERE "Category" = 'Skill');
                DELETE FROM enemy.items WHERE "Category" = 'Skill';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "skills",
                schema: "enemy",
                columns: table => new
                {
                    SkillId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActiveEndFrac = table.Column<float>(type: "real", nullable: false),
                    ActiveStartFrac = table.Column<float>(type: "real", nullable: false),
                    Angle = table.Column<float>(type: "real", nullable: false),
                    ApScaling = table.Column<float>(type: "real", nullable: false),
                    BaseDamage = table.Column<int>(type: "integer", nullable: false),
                    CastTime = table.Column<float>(type: "real", nullable: false),
                    Cooldown = table.Column<float>(type: "real", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DamageType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Delivery = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Element = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    HitShape = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    KnockbackForce = table.Column<float>(type: "real", nullable: false),
                    ManaCost = table.Column<int>(type: "integer", nullable: false),
                    OffsetX = table.Column<float>(type: "real", nullable: false),
                    OffsetY = table.Column<float>(type: "real", nullable: false),
                    ProjectileCount = table.Column<int>(type: "integer", nullable: false),
                    ProjectileSpeed = table.Column<float>(type: "real", nullable: false),
                    Range = table.Column<float>(type: "real", nullable: false),
                    RectHeight = table.Column<float>(type: "real", nullable: false),
                    RectWidth = table.Column<float>(type: "real", nullable: false),
                    SpreadAngle = table.Column<float>(type: "real", nullable: false),
                    SweetSpotMultiplier = table.Column<float>(type: "real", nullable: false),
                    SweetSpotRadius = table.Column<float>(type: "real", nullable: false),
                    TickInterval = table.Column<float>(type: "real", nullable: false),
                    UnityBaselineJson = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VfxLifetime = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.SkillId);
                });
        }
    }
}