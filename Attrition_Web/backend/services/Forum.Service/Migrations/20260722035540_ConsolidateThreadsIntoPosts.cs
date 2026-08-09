using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Service.Migrations
{
    public partial class ConsolidateThreadsIntoPosts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_ForumPosts_ThreadId", schema: "forum", table: "ForumPosts");
            migrationBuilder.AddColumn<int>(name: "CategoryId", schema: "forum", table: "ForumPosts", type: "integer", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "IsLocked", schema: "forum", table: "ForumPosts", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "IsPinned", schema: "forum", table: "ForumPosts", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(name: "LastReplyAt", schema: "forum", table: "ForumPosts", type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP");
            migrationBuilder.AddColumn<int>(name: "ReplyCount", schema: "forum", table: "ForumPosts", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<Guid>(name: "RootPostId", schema: "forum", table: "ForumPosts", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Title", schema: "forum", table: "ForumPosts", type: "text", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "WikiArticleId", schema: "forum", table: "ForumPosts", type: "uuid", nullable: true);

            // Keep every public discussion ID stable by turning the old thread ID into the root post ID.
            // The old opening post body, author, attachments, moderation and reactions move to that root.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM forum."ForumThreads" t
                        JOIN forum."ForumPosts" p ON p."Id" = t."Id"
                    ) THEN
                        RAISE EXCEPTION 'Forum thread/post ID collision prevents safe consolidation';
                    END IF;
                END $$;

                CREATE TEMP TABLE forum_root_map ON COMMIT DROP AS
                SELECT t."Id" AS thread_id,
                       (SELECT p."Id" FROM forum."ForumPosts" p WHERE p."ThreadId" = t."Id" ORDER BY p."CreatedAt", p."Id" LIMIT 1) AS opener_id
                FROM forum."ForumThreads" t;

                INSERT INTO forum."ForumPosts" (
                    "Id", "RootPostId", "ParentPostId", "Depth", "CategoryId", "Title", "WikiArticleId",
                    "IsPinned", "IsLocked", "LastReplyAt", "ReplyCount", "AuthorId", "AuthorName",
                    "AuthorAvatar", "AuthorRole", "Content", "Attachments", "CreatedAt", "UpdatedAt",
                    "IsRemoved", "RemovedReason", "RemovedByUserId", "RemovedByName", "RemovedAt", "ThreadId")
                SELECT t."Id", NULL, NULL, 0, NULLIF(t."CategoryId", 0), t."Title", t."WikiArticleId",
                       t."IsPinned", t."IsLocked", t."LastReplyAt", t."ReplyCount",
                       COALESCE(o."AuthorId", t."AuthorId"), COALESCE(o."AuthorName", t."AuthorName"),
                       COALESCE(o."AuthorAvatar", t."AuthorAvatar"), COALESCE(o."AuthorRole", 'User'),
                       COALESCE(o."Content", ''), o."Attachments", t."CreatedAt", o."UpdatedAt",
                       COALESCE(o."IsRemoved", false), o."RemovedReason", o."RemovedByUserId", o."RemovedByName", o."RemovedAt", t."Id"
                FROM forum."ForumThreads" t
                LEFT JOIN forum_root_map m ON m.thread_id = t."Id"
                LEFT JOIN forum."ForumPosts" o ON o."Id" = m.opener_id;

                UPDATE forum."ForumPosts" p
                SET "RootPostId" = p."ThreadId"
                WHERE p."Id" NOT IN (SELECT thread_id FROM forum_root_map)
                  AND p."Id" NOT IN (SELECT opener_id FROM forum_root_map WHERE opener_id IS NOT NULL);

                UPDATE forum."ForumPosts" p
                SET "ParentPostId" = m.thread_id
                FROM forum_root_map m
                WHERE p."ParentPostId" = m.opener_id;

                UPDATE forum."ForumReactions" r SET "PostId" = m.thread_id
                FROM forum_root_map m WHERE r."PostId" = m.opener_id;
                UPDATE forum."PostReports" r SET "PostId" = m.thread_id
                FROM forum_root_map m WHERE r."PostId" = m.opener_id;
                UPDATE forum."ThreadSubscriptions" s SET "ThreadId" = m.thread_id
                FROM forum_root_map m WHERE s."ThreadId" = m.thread_id;

                DELETE FROM forum."ForumPosts" p USING forum_root_map m WHERE p."Id" = m.opener_id;
                """);

            migrationBuilder.DropTable(name: "ForumThreads", schema: "forum");
            migrationBuilder.DropColumn(name: "ThreadId", schema: "forum", table: "ForumPosts");
            migrationBuilder.CreateIndex(name: "IX_ForumPosts_CategoryId", schema: "forum", table: "ForumPosts", column: "CategoryId");
            migrationBuilder.CreateIndex(name: "IX_ForumPosts_RootPostId", schema: "forum", table: "ForumPosts", column: "RootPostId");
            migrationBuilder.CreateIndex(name: "IX_ForumPosts_WikiArticleId", schema: "forum", table: "ForumPosts", column: "WikiArticleId", unique: true, filter: "\"WikiArticleId\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_ForumPosts_CategoryId", schema: "forum", table: "ForumPosts");
            migrationBuilder.DropIndex(name: "IX_ForumPosts_RootPostId", schema: "forum", table: "ForumPosts");
            migrationBuilder.DropIndex(name: "IX_ForumPosts_WikiArticleId", schema: "forum", table: "ForumPosts");
            migrationBuilder.AddColumn<Guid>(name: "ThreadId", schema: "forum", table: "ForumPosts", type: "uuid", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.CreateTable(
                name: "ForumThreads", schema: "forum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorName = table.Column<string>(type: "text", nullable: true),
                    AuthorAvatar = table.Column<string>(type: "text", nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastReplyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReplyCount = table.Column<int>(type: "integer", nullable: false),
                    WikiArticleId = table.Column<Guid>(type: "uuid", nullable: true)
                }, constraints: table => table.PrimaryKey("PK_ForumThreads", x => x.Id));
            migrationBuilder.Sql("""
                INSERT INTO forum."ForumThreads" ("Id","CategoryId","Title","AuthorId","AuthorName","AuthorAvatar","IsPinned","IsLocked","CreatedAt","LastReplyAt","ReplyCount","WikiArticleId")
                SELECT "Id",COALESCE("CategoryId",0),COALESCE("Title",'Discussion'),"AuthorId","AuthorName","AuthorAvatar","IsPinned","IsLocked","CreatedAt","LastReplyAt","ReplyCount","WikiArticleId"
                FROM forum."ForumPosts" WHERE "RootPostId" IS NULL;
                UPDATE forum."ForumPosts" SET "ThreadId" = COALESCE("RootPostId", "Id");
                """);
            migrationBuilder.DropColumn(name: "CategoryId", schema: "forum", table: "ForumPosts");
            migrationBuilder.DropColumn(name: "IsLocked", schema: "forum", table: "ForumPosts");
            migrationBuilder.DropColumn(name: "IsPinned", schema: "forum", table: "ForumPosts");
            migrationBuilder.DropColumn(name: "LastReplyAt", schema: "forum", table: "ForumPosts");
            migrationBuilder.DropColumn(name: "ReplyCount", schema: "forum", table: "ForumPosts");
            migrationBuilder.DropColumn(name: "RootPostId", schema: "forum", table: "ForumPosts");
            migrationBuilder.DropColumn(name: "Title", schema: "forum", table: "ForumPosts");
            migrationBuilder.DropColumn(name: "WikiArticleId", schema: "forum", table: "ForumPosts");
            migrationBuilder.CreateIndex(name: "IX_ForumPosts_ThreadId", schema: "forum", table: "ForumPosts", column: "ThreadId");
            migrationBuilder.CreateIndex(name: "IX_ForumThreads_CategoryId", schema: "forum", table: "ForumThreads", column: "CategoryId");
            migrationBuilder.CreateIndex(name: "IX_ForumThreads_WikiArticleId", schema: "forum", table: "ForumThreads", column: "WikiArticleId", unique: true, filter: "\"WikiArticleId\" IS NOT NULL");
        }
    }
}