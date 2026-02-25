using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_7_0_Extend_DeviceGroup_and_Playlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "video_statuses");

            migrationBuilder.AddColumn<string>(
                name: "sha256",
                table: "videos",
                type: "char(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "playlist_device_group",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    playlist_id = table.Column<int>(type: "integer", nullable: false),
                    device_group_id = table.Column<int>(type: "integer", nullable: false),
                    play = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playlist_device_group", x => x.id);
                    table.ForeignKey(
                        name: "FK_playlist_device_group_device_groups_device_group_id",
                        column: x => x.device_group_id,
                        principalTable: "device_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_playlist_device_group_playlists_playlist_id",
                        column: x => x.playlist_id,
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_playlist_device_group_device_group_id",
                table: "playlist_device_group",
                column: "device_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_playlist_device_group_playlist_id",
                table: "playlist_device_group",
                column: "playlist_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "playlist_device_group");

            migrationBuilder.DropColumn(
                name: "sha256",
                table: "videos");

            migrationBuilder.CreateTable(
                name: "video_statuses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_statuses", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "video_statuses",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 1, "Queued" },
                    { 2, "Loading" },
                    { 3, "Loaded" },
                    { 4, "Playing" }
                });
        }
    }
}
