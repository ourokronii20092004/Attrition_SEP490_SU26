using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Attrition.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMusicArtistsAndCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Artist",
                table: "MusicAlbums");

            migrationBuilder.AddColumn<List<string>>(
                name: "Artists",
                table: "MusicTracks",
                type: "text[]",
                nullable: false,
                defaultValue: new List<string>());

            migrationBuilder.AddColumn<string>(
                name: "CoverPath",
                table: "MusicTracks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Artists",
                table: "MusicAlbums",
                type: "text[]",
                nullable: false,
                defaultValue: new List<string>());

            migrationBuilder.AddColumn<string>(
                name: "Genre",
                table: "MusicAlbums",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Artists",
                table: "MusicTracks");

            migrationBuilder.DropColumn(
                name: "CoverPath",
                table: "MusicTracks");

            migrationBuilder.DropColumn(
                name: "Artists",
                table: "MusicAlbums");

            migrationBuilder.DropColumn(
                name: "Genre",
                table: "MusicAlbums");

            migrationBuilder.AddColumn<string>(
                name: "Artist",
                table: "MusicAlbums",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
