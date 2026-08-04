using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skill.Service.Migrations
{
    /// <summary>
    /// Data-only: the game renamed SkillElement.Wood -> Wind and Thrust -> Water. Live rows still
    /// hold the old names, and SkillConfigProvider parses Element with Enum.TryParse — on a miss it
    /// returns the config unmodified, silently dropping every server override for that skill. So the
    /// rows must move in the same deploy as the validator allowlist.
    /// </summary>
    public partial class RenameWoodWindThrustWater : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE skill.skills SET ""Element"" = 'Wind' WHERE ""Element"" = 'Wood';");
            migrationBuilder.Sql(@"UPDATE skill.skills SET ""Element"" = 'Water' WHERE ""Element"" = 'Thrust';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE skill.skills SET ""Element"" = 'Wood' WHERE ""Element"" = 'Wind';");
            migrationBuilder.Sql(@"UPDATE skill.skills SET ""Element"" = 'Thrust' WHERE ""Element"" = 'Water';");
        }
    }
}
