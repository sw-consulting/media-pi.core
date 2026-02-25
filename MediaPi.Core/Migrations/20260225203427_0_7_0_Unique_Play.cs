using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_7_0_Unique_Play : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_playlist_device_group_device_group_id",
                table: "playlist_device_group");

            migrationBuilder.CreateIndex(
                name: "IX_playlist_device_group_device_group_id",
                table: "playlist_device_group",
                column: "device_group_id",
                unique: true,
                filter: "\"play\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_playlist_device_group_device_group_id",
                table: "playlist_device_group");

            migrationBuilder.CreateIndex(
                name: "IX_playlist_device_group_device_group_id",
                table: "playlist_device_group",
                column: "device_group_id");
        }
    }
}
