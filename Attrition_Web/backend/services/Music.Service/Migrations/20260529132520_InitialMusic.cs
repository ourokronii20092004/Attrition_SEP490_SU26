using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Music.Service.Migrations
{
    /// <inheritdoc />
    public partial class InitialMusic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "music");

            migrationBuilder.CreateTable(
                name: "MusicAlbums",
                schema: "music",
                columns: table => new
                {
                    AlbumId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Artists = table.Column<List<string>>(type: "text[]", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CoverPath = table.Column<string>(type: "text", nullable: true),
                    IsCoverUserDefined = table.Column<bool>(type: "boolean", nullable: false),
                    Genre = table.Column<string>(type: "text", nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AlbumType = table.Column<string>(type: "text", nullable: false),
                    TrackCount = table.Column<int>(type: "integer", nullable: false),
                    TotalDuration = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicAlbums", x => x.AlbumId);
                });

            migrationBuilder.CreateTable(
                name: "MusicPlaylists",
                schema: "music",
                columns: table => new
                {
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    TrackCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicPlaylists", x => x.PlaylistId);
                });

            migrationBuilder.CreateTable(
                name: "UserFavorites",
                schema: "music",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackId = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFavorites", x => new { x.UserId, x.TrackId });
                });

            migrationBuilder.CreateTable(
                name: "MusicTracks",
                schema: "music",
                columns: table => new
                {
                    TrackId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlbumId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Artists = table.Column<List<string>>(type: "text[]", nullable: false),
                    CoverPath = table.Column<string>(type: "text", nullable: true),
                    TrackNumber = table.Column<int>(type: "integer", nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    Genre = table.Column<string>(type: "text", nullable: true),
                    Bpm = table.Column<int>(type: "integer", nullable: true),
                    PlayCount = table.Column<int>(type: "integer", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicTracks", x => x.TrackId);
                    table.ForeignKey(
                        name: "FK_MusicTracks_MusicAlbums_AlbumId",
                        column: x => x.AlbumId,
                        principalSchema: "music",
                        principalTable: "MusicAlbums",
                        principalColumn: "AlbumId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistTracks",
                schema: "music",
                columns: table => new
                {
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistTracks", x => new { x.PlaylistId, x.TrackId });
                    table.ForeignKey(
                        name: "FK_PlaylistTracks_MusicPlaylists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalSchema: "music",
                        principalTable: "MusicPlaylists",
                        principalColumn: "PlaylistId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MusicAlbums_Slug",
                schema: "music",
                table: "MusicAlbums",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MusicPlaylists_UserId",
                schema: "music",
                table: "MusicPlaylists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicTracks_AlbumId_TrackNumber",
                schema: "music",
                table: "MusicTracks",
                columns: new[] { "AlbumId", "TrackNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MusicTracks",
                schema: "music");

            migrationBuilder.DropTable(
                name: "PlaylistTracks",
                schema: "music");

            migrationBuilder.DropTable(
                name: "UserFavorites",
                schema: "music");

            migrationBuilder.DropTable(
                name: "MusicAlbums",
                schema: "music");

            migrationBuilder.DropTable(
                name: "MusicPlaylists",
                schema: "music");
        }
    }
}
