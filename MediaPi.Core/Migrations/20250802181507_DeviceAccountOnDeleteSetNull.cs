using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaPi.Core.Migrations
{
    /// <inheritdoc />
    public partial class DeviceAccountOnDeleteSetNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_devices_accounts_account_id",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_device_groups_device_group_id",
                table: "devices");

            migrationBuilder.AddForeignKey(
                name: "FK_devices_accounts_account_id",
                table: "devices",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_devices_device_groups_device_group_id",
                table: "devices",
                column: "device_group_id",
                principalTable: "device_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_devices_accounts_account_id",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_device_groups_device_group_id",
                table: "devices");

            migrationBuilder.AddForeignKey(
                name: "FK_devices_accounts_account_id",
                table: "devices",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_devices_device_groups_device_group_id",
                table: "devices",
                column: "device_group_id",
                principalTable: "device_groups",
                principalColumn: "id");
        }
    }
}
