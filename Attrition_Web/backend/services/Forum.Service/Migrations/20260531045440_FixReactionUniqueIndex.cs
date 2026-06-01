using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Service.Migrations
{
    /// <inheritdoc />
    public partial class FixReactionUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ForumReactions_PostId_UserId_ReactionType",
                schema: "forum",
                table: "ForumReactions");

            // Defensive: collapse any (PostId, UserId) duplicates before the unique index,
            // keeping the most recent row, so migrate-on-startup can't fail the container boot.
            migrationBuilder.Sql(@"
                DELETE FROM forum.""ForumReactions"" a
                USING forum.""ForumReactions"" b
                WHERE a.""PostId"" = b.""PostId""
                  AND a.""UserId"" = b.""UserId""
                  AND a.""Id"" < b.""Id"";");

            migrationBuilder.CreateIndex(
                name: "IX_ForumReactions_PostId_UserId",
                schema: "forum",
                table: "ForumReactions",
                columns: new[] { "PostId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ForumReactions_PostId_UserId",
                schema: "forum",
                table: "ForumReactions");

            migrationBuilder.CreateIndex(
                name: "IX_ForumReactions_PostId_UserId_ReactionType",
                schema: "forum",
                table: "ForumReactions",
                columns: new[] { "PostId", "UserId", "ReactionType" },
                unique: true);
        }
    }
}
