using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skill.Service.Migrations
{
    /// <inheritdoc />
    public partial class InitialSkill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "skill");

            migrationBuilder.CreateTable(
                name: "skills",
                schema: "skill",
                columns: table => new
                {
                    SkillId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IconKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Rarity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_skills_Element",
                schema: "skill",
                table: "skills",
                column: "Element");

            // Preserve the existing Enemy.Service skill catalog during extraction. The guards also
            // keep a fresh install valid when the legacy tables never existed.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('enemy.skills') IS NOT NULL AND to_regclass('enemy.items') IS NOT NULL THEN
                        INSERT INTO skill.skills (
                            "SkillId", "Name", "Description", "IconKey", "Rarity", "Element", "ManaCost",
                            "CastTime", "Cooldown", "ActiveStartFrac", "ActiveEndFrac", "DamageType",
                            "BaseDamage", "ApScaling", "KnockbackForce", "TickInterval", "SweetSpotRadius",
                            "SweetSpotMultiplier", "Delivery", "HitShape", "Range", "Angle", "RectWidth",
                            "RectHeight", "OffsetX", "OffsetY", "ProjectileSpeed", "ProjectileCount",
                            "SpreadAngle", "VfxLifetime", "ImageUrl", "UnityBaselineJson", "ImportedAt",
                            "CreatedAt", "UpdatedAt")
                        SELECT s."SkillId", COALESCE(i."Name", s."SkillId"), i."Description", i."IconKey",
                            COALESCE(i."Rarity", 'Common'), s."Element", s."ManaCost", s."CastTime", s."Cooldown",
                            s."ActiveStartFrac", s."ActiveEndFrac", s."DamageType", s."BaseDamage", s."ApScaling",
                            s."KnockbackForce", s."TickInterval", s."SweetSpotRadius", s."SweetSpotMultiplier",
                            s."Delivery", s."HitShape", s."Range", s."Angle", s."RectWidth", s."RectHeight",
                            s."OffsetX", s."OffsetY", s."ProjectileSpeed", s."ProjectileCount", s."SpreadAngle",
                            s."VfxLifetime", COALESCE(i."ImageUrl", s."ImageUrl"), s."UnityBaselineJson",
                            s."ImportedAt", s."CreatedAt", GREATEST(s."UpdatedAt", i."UpdatedAt")
                        FROM enemy.skills s
                        LEFT JOIN enemy.items i ON i."ItemId" = s."SkillId" AND i."Category" = 'Skill'
                        ON CONFLICT ("SkillId") DO NOTHING;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "skills",
                schema: "skill");
        }
    }
}