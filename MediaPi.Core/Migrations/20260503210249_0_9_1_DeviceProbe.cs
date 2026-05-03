using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_9_1_DeviceProbe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "playback_service_status",
                table: "device_probes",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "playlist_upload_service_status",
                table: "device_probes",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "video_upload_service_status",
                table: "device_probes",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "playback_service_status",
                table: "device_probes");

            migrationBuilder.DropColumn(
                name: "playlist_upload_service_status",
                table: "device_probes");

            migrationBuilder.DropColumn(
                name: "video_upload_service_status",
                table: "device_probes");
        }
    }
}
