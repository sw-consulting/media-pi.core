using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class _0_8_0_ServerKey_Idx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_devices_server_key",
                table: "devices",
                column: "server_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devices_server_key",
                table: "devices");
        }
    }
}
