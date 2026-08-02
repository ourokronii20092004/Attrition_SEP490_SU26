using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Music.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddUnityMusicMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "GameUsages",
                schema: "music",
                table: "MusicTracks",
                type: "text[]",
                nullable: false,
                defaultValue: Array.Empty<string>());

            migrationBuilder.AddColumn<string>(
                name: "UnitySourceKey",
                schema: "music",
                table: "MusicTracks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MusicTracks_UnitySourceKey",
                schema: "music",
                table: "MusicTracks",
                column: "UnitySourceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MusicTracks_UnitySourceKey",
                schema: "music",
                table: "MusicTracks");

            migrationBuilder.DropColumn(
                name: "GameUsages",
                schema: "music",
                table: "MusicTracks");

            migrationBuilder.DropColumn(
                name: "UnitySourceKey",
                schema: "music",
                table: "MusicTracks");
        }
    }
}
